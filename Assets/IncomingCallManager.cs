using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


/// <summary>
/// Story call timeline: Neighbor -> Mom -> Microsoft.
/// The call is only shown while the player is idle on the Home Screen (never during a chat).
/// </summary>
public class IncomingCallManager : MonoBehaviour
{
    enum StoryCallPhase
    {
        WaitingForNeighbor,
        NeighborActive,
        WaitingForMom,
        MomActive,
        WaitingForMicrosoft,
        MicrosoftActive,
        Complete
    }

    enum ActiveCallType
    {
        None,
        Neighbor,
        Mom,
        Microsoft,
        Unknown
    }

    [Header("Managers")]
    public PhoneUIManager phoneManager;
    public CallerScript callerController;

    [Header("Incoming Call Screen")]
    public GameObject incomingCallScreen;
    public Button incomingAnswerButton;
    public Button incomingDeclineButton;
    public TMP_Text incomingCallerName;
    public Image incomingCallerAvatar;

    [Header("Caller Screen buttons")]
    public GameObject callerEndCallButton;
    public Button callerAnswerButton;
    public Button callerDeclineButton;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip ringtoneClip;

    [Header("Incoming Ringtone")]
    public AudioSource ringtoneSource;
    public AudioClip incomingRingtoneClip;

    [Header("Timing")]
    [Tooltip("Seconds after game start before the Neighbor call can ring (default 60 = 1 minute).")]
    public float delaySeconds = 60f;

    [Header("Story Call Timing")]
    [Tooltip("Seconds after Neighbor call ends before Mom call can ring(default 150s).")]
    public float delayBeforeMom = 150f;

    [Tooltip("Seconds after Mom call ends before Microsoft call can ring (default 150s).")]
    public float delayBeforeMicrosoft = 150f;

    [Header("Button Wiring")]
    [Tooltip("When enabled, IncomingCallManager wires Answer/Decline at runtime. Clear the button's Inspector On Click() list to avoid double-firing.")]
    public bool wireIncomingButtonsAtRuntime = true;

    float phaseElapsed;
    bool callPending;
    bool callShown;
    bool timersInitialized;
    StoryCallPhase storyPhase = StoryCallPhase.WaitingForNeighbor;
    ActiveCallType activeCallType = ActiveCallType.None;
    bool answerTransitionInProgress;
    float answerTransitionStartedAt;
    bool phoneLockedByBadEnding;
    Coroutine vibrationRoutine;

    public bool isPhoneBusy;

    public bool IsIncomingStoryCallInProgress =>
        activeCallType == ActiveCallType.Neighbor
        || activeCallType == ActiveCallType.Mom
        || activeCallType == ActiveCallType.Microsoft;

    // #region agent log
    static void AgentLog(string hypothesisId, string location, string message, string dataJson)
    {
        try
        {
            string path = Path.Combine(Application.dataPath, "..", "debug-164d82.log");
            string line =
                "{\"sessionId\":\"164d82\",\"hypothesisId\":\"" + hypothesisId +
                "\",\"location\":\"" + location + "\",\"message\":\"" + message +
                "\",\"data\":" + dataJson + ",\"timestamp\":" +
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n";
            File.AppendAllText(path, line);
        }
        catch { /* ignore logging failures */ }
    }
    // #endregion

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        if (ringtoneSource == null) ringtoneSource = gameObject.AddComponent<AudioSource>();
        ringtoneSource.playOnAwake = false;
        ringtoneSource.loop = true;

        StopIncomingDeclineVibration();

        WireIncomingButtons();
    }

    void WireIncomingButtons()
    {
        if (!wireIncomingButtonsAtRuntime)
        {
            Debug.Log("[IncomingCallManager] Runtime button wiring disabled. Use Inspector On Click() -> IncomingCallManager.AnswerIncoming / DeclineIncoming.");
            return;
        }

        if (incomingAnswerButton != null)
        {
            incomingAnswerButton.onClick.RemoveListener(AnswerIncoming);
            incomingAnswerButton.onClick.AddListener(AnswerIncoming);
            Debug.Log("[IncomingCallManager] Wired incomingAnswerButton -> AnswerIncoming (clear Inspector On Click() to avoid duplicates).");
        }
        else
        {
            Debug.LogWarning("[IncomingCallManager] incomingAnswerButton is not assigned.");
            AgentLog("D", "IncomingCallManager.Awake", "incomingAnswerButton null", "{}");
        }

        if (incomingDeclineButton != null)
        {
            incomingDeclineButton.onClick.RemoveListener(DeclineIncoming);
            incomingDeclineButton.onClick.AddListener(DeclineIncoming);
        }

        if (callerAnswerButton != null && callerAnswerButton != incomingAnswerButton)
        {
            callerAnswerButton.onClick.RemoveListener(EndCallerScreen);
            callerAnswerButton.onClick.AddListener(EndCallerScreen);
        }

        if (callerDeclineButton != null && callerDeclineButton != incomingDeclineButton)
        {
            callerDeclineButton.onClick.RemoveListener(EndCallerScreen);
            callerDeclineButton.onClick.AddListener(EndCallerScreen);
        }
    }

    void Start()
    {
        if (incomingCallScreen != null) incomingCallScreen.SetActive(false);
        EnsureIncomingAnswerButtonVisible();

        if (callerController != null)
        {
            callerController.OnCallEnded += HandleCallEnded;
            callerController.OnCallBadEnding += HandleBadCallEnding;
            callerController.OnMicrosoftCallCompleted += HandleMicrosoftStoryCompleted;
            if (callerController.incomingCallManager == null)
                callerController.incomingCallManager = this;
        }
        else
        {
            Debug.LogWarning("[IncomingCallManager] callerController is not assigned.");
            AgentLog("D", "IncomingCallManager.Start", "callerController null", "{}");
        }

        phaseElapsed = 0f;
        callPending = false;
        callShown = false;
        timersInitialized = true;

        Debug.Log($"[IncomingCallManager] Timers started. Neighbor={delaySeconds}s, Mom={delayBeforeMom}s, Microsoft={delayBeforeMicrosoft}s, phase={storyPhase}");
        AgentLog("C", "IncomingCallManager.Start", "Timers initialized",
            "{\"delaySeconds\":" + delaySeconds + ",\"delayBeforeMom\":" + delayBeforeMom +
            ",\"delayBeforeMicrosoft\":" + delayBeforeMicrosoft + ",\"phase\":\"" + storyPhase + "\"}");
    }

    void OnDestroy()
    {
        if (callerController != null)
        {
            callerController.OnCallEnded -= HandleCallEnded;
            callerController.OnCallBadEnding -= HandleBadCallEnding;
            callerController.OnMicrosoftCallCompleted -= HandleMicrosoftStoryCompleted;
        }
    }

    void Update()
    {
        if (!timersInitialized || storyPhase == StoryCallPhase.Complete || callShown || isPhoneBusy)
            return;

        phaseElapsed += Time.deltaTime;

        float requiredDelay = GetRequiredDelayForCurrentPhase();
        if (requiredDelay <= 0f)
        {
            Debug.LogWarning($"[IncomingCallManager] Required delay is {requiredDelay}. Set timing fields to positive values.");
            return;
        }

        if (!callPending && phaseElapsed >= requiredDelay)
        {
            callPending = true;
            Debug.Log($"[IncomingCallManager] Call pending for phase {storyPhase} after {phaseElapsed:F1}s (required {requiredDelay}s). Waiting for Home.");
            AgentLog("C", "IncomingCallManager.Update", "Call pending",
                "{\"phase\":\"" + storyPhase + "\",\"phaseElapsed\":" + phaseElapsed + ",\"requiredDelay\":" + requiredDelay + "}");
        }

        if (callPending && IsIdleOnHome())
            TriggerPendingCall();
    }

    float GetRequiredDelayForCurrentPhase()
    {
        switch (storyPhase)
        {
            case StoryCallPhase.WaitingForNeighbor:
                return delaySeconds;
            case StoryCallPhase.WaitingForMom:
                return delayBeforeMom;
            case StoryCallPhase.WaitingForMicrosoft:
                return delayBeforeMicrosoft;
            default:
                return float.MaxValue;
        }
    }

    bool IsIdleOnHome()
    {
        if (isPhoneBusy)
            return false;

        if (phoneManager == null)
        {
            AgentLog("C", "IncomingCallManager.IsIdleOnHome", "phoneManager null treating as idle", "{}");
            return true;
        }

        if (phoneManager.chatScreen != null && phoneManager.chatScreen.activeSelf)
            return false;

        if (incomingCallScreen != null && incomingCallScreen.activeSelf)
            return false;

        if (phoneManager.callerScreen != null && phoneManager.callerScreen.activeSelf)
            return false;

        if (phoneManager.homeScreen == null) return true;
        return phoneManager.homeScreen.activeSelf;
    }

    void TriggerPendingCall()
    {
        callPending = false;

        Debug.Log($"[IncomingCallManager] Triggering call for phase {storyPhase}");
        AgentLog("C", "IncomingCallManager.TriggerPendingCall", "Call triggered",
            "{\"phase\":\"" + storyPhase + "\",\"phaseElapsed\":" + phaseElapsed + "}");

        if (storyPhase == StoryCallPhase.WaitingForNeighbor)
            ShowStoryIncomingCall(StoryCallPhase.NeighborActive, ActiveCallType.Neighbor);
        else if (storyPhase == StoryCallPhase.WaitingForMom)
            ShowStoryIncomingCall(StoryCallPhase.MomActive, ActiveCallType.Mom);
        else if (storyPhase == StoryCallPhase.WaitingForMicrosoft)
            ShowStoryIncomingCall(StoryCallPhase.MicrosoftActive, ActiveCallType.Microsoft);
    }

    void ShowStoryIncomingCall(StoryCallPhase phase, ActiveCallType callType)
    {
        storyPhase = phase;
        activeCallType = callType;
        callShown = true;

        if (callType == ActiveCallType.Neighbor)
            ApplyIncomingCallPresentation(
                callerController != null && callerController.neighborCallData != null
                    ? callerController.neighborCallData.displayName
                    : "NEIGHBOR",
                callerController != null ? callerController.neighborAvatar : null);
        else if (callType == ActiveCallType.Mom)
            ApplyIncomingCallPresentation(
                callerController != null && callerController.momCallData != null
                    ? callerController.momCallData.displayName
                    : "MOM",
                callerController != null ? callerController.momAvatar : null);
        else if (callType == ActiveCallType.Microsoft)
            ApplyIncomingCallPresentation(
                callerController != null && callerController.microsoftCallData != null
                    ? callerController.microsoftCallData.displayName
                    : "MICROSOFT",
                callerController != null ? callerController.microsoftAvatar : null);

        PresentIncomingCallScreen();
    }

    void ApplyIncomingCallPresentation(string displayName, Sprite avatar)
    {
        if (incomingCallerName != null)
            incomingCallerName.text = string.IsNullOrEmpty(displayName) ? "UNKNOWN NUMBER" : displayName;

        if (incomingCallerAvatar != null && avatar != null)
            incomingCallerAvatar.sprite = avatar;
    }

    void PresentIncomingCallScreen()
    {
        if (phoneManager != null && phoneManager.homeScreen != null)
            phoneManager.homeScreen.SetActive(false);

        if (incomingCallScreen != null) incomingCallScreen.SetActive(true);
        PrepareIncomingRingButtons();

        if (ringtoneSource != null && incomingRingtoneClip != null)
        {
            ringtoneSource.clip = incomingRingtoneClip;
            ringtoneSource.loop = true;
            ringtoneSource.Play();
        }

        Debug.Log($"[IncomingCallManager] Incoming call screen shown. activeCallType={activeCallType}");
        AgentLog("C", "IncomingCallManager.PresentIncomingCallScreen", "Incoming screen shown",
            "{\"activeCallType\":\"" + activeCallType + "\"}");
    }

    bool IsIncomingUiButton(Button button)
    {
        if (button == null || incomingCallScreen == null)
            return false;

        return button.transform.IsChildOf(incomingCallScreen.transform);
    }

    void EnsureIncomingAnswerButtonVisible()
    {
        PrepareIncomingRingButtons();
    }

    void PrepareIncomingRingButtons()
    {
        if (incomingAnswerButton != null)
        {
            incomingAnswerButton.gameObject.SetActive(true);
            incomingAnswerButton.interactable = true;
        }

        if (incomingDeclineButton != null)
        {
            incomingDeclineButton.gameObject.SetActive(true);
            incomingDeclineButton.interactable = !IsIncomingStoryCallInProgress;
        }

        AgentLog("G", "IncomingCallManager.EnsureIncomingAnswerButtonVisible",
            "Incoming answer button state",
            "{\"incomingAnswerActive\":" +
            (incomingAnswerButton != null && incomingAnswerButton.gameObject.activeSelf ? "true" : "false") +
            ",\"sharesCallerAnswerReference\":" +
            (incomingAnswerButton != null && incomingAnswerButton == callerAnswerButton ? "true" : "false") + "}");
    }

    void SetCallerScreenButtonVisible(Button button, bool visible)
    {
        if (button == null || IsIncomingUiButton(button))
            return;

        button.gameObject.SetActive(visible);
    }

    void StopRingtone()
    {
        if (ringtoneSource != null) ringtoneSource.Stop();
    }

    void StartIncomingDeclineVibration()
    {
        if (vibrationRoutine != null)
            StopCoroutine(vibrationRoutine);

        vibrationRoutine = StartCoroutine(IncomingDeclineVibrationCoroutine());
    }

    void StopIncomingDeclineVibration()
    {
        if (vibrationRoutine != null)
        {
            StopCoroutine(vibrationRoutine);
            vibrationRoutine = null;
        }
    }

    IEnumerator IncomingDeclineVibrationCoroutine()
    {
        const float duration = 2f;
        const float interval = 0.12f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Handheld.Vibrate();
            yield return new WaitForSecondsRealtime(interval);
            elapsed += interval;
        }

        vibrationRoutine = null;
    }

    void RestoreCallerScreenButtons()
    {
        SetCallerScreenButtonVisible(callerAnswerButton, false);
        SetCallerScreenButtonVisible(callerDeclineButton, false);
        SetCallerEndCallAvailable(true);
        EnsureIncomingAnswerButtonVisible();
    }

    public void SetCallerEndCallAvailable(bool available)
    {
        if (callerEndCallButton != null)
        {
            callerEndCallButton.SetActive(available);
            Button endButton = callerEndCallButton.GetComponent<Button>();
            if (endButton != null)
                endButton.interactable = available;
        }

        SetCallerScreenButtonVisible(callerAnswerButton, false);
        SetCallerScreenButtonVisible(callerDeclineButton, false);
    }

    void ReturnToIdleHome()
    {
        StopRingtone();
        StopIncomingDeclineVibration();
        if (!phoneLockedByBadEnding)
            isPhoneBusy = false;

        if (incomingCallScreen != null) incomingCallScreen.SetActive(false);

        if (phoneManager != null)
        {
            if (phoneManager.galleryScreen != null) phoneManager.galleryScreen.SetActive(false);
            if (phoneManager.messagesScreen != null) phoneManager.messagesScreen.SetActive(false);
            if (phoneManager.callsScreen != null) phoneManager.callsScreen.SetActive(false);
            if (phoneManager.browserScreen != null) phoneManager.browserScreen.SetActive(false);
            if (phoneManager.SocialMediaScreen != null) phoneManager.SocialMediaScreen.SetActive(false);
            if (phoneManager.chatScreen != null) phoneManager.chatScreen.SetActive(false);
            if (phoneManager.callerScreen != null) phoneManager.callerScreen.SetActive(false);
            if (phoneManager.homeScreen != null) phoneManager.homeScreen.SetActive(true);
        }
        else if (callerController != null && callerController.callerScreen != null)
        {
            callerController.callerScreen.SetActive(false);
        }

        RestoreCallerScreenButtons();

        Debug.Log("[IncomingCallManager] Returned to idle Home screen.");
        AgentLog("E", "IncomingCallManager.ReturnToIdleHome", "Returned home", "{}");
    }

    public void AnswerIncoming()
    {
        StopIncomingDeclineVibration();

        if (answerTransitionInProgress && Time.unscaledTime - answerTransitionStartedAt < 0.35f)
        {
            Debug.LogWarning("[IncomingCallManager] AnswerIncoming ignored: transition already in progress.");
            AgentLog("A", "IncomingCallManager.AnswerIncoming", "Ignored duplicate transition", "{}");
            return;
        }

        answerTransitionInProgress = true;
        answerTransitionStartedAt = Time.unscaledTime;

        bool incomingVisible = incomingCallScreen != null && incomingCallScreen.activeSelf;
        if (!callShown && !incomingVisible)
        {
            Debug.LogWarning("[IncomingCallManager] AnswerIncoming ignored: no incoming call is active.");
            AgentLog("A", "IncomingCallManager.AnswerIncoming", "Ignored not ringing", "{}");
            answerTransitionInProgress = false;
            return;
        }

        callShown = true;

        if (callerController == null)
        {
            Debug.LogError("[IncomingCallManager] callerController is not assigned. Drag your CallerScript/CallerController component onto IncomingCallManager.");
            answerTransitionInProgress = false;
            return;
        }

        Debug.Log($"[IncomingCallManager] AnswerIncoming. activeCallType={activeCallType}");
        AgentLog("A", "IncomingCallManager.AnswerIncoming", "Call answered",
            "{\"activeCallType\":\"" + activeCallType + "\"}");

        if (MissionSidebarManager.Instance != null)
        {
            MissionSidebarManager.Instance.AddProgress(1);
        }

        StopRingtone();
        if (incomingCallScreen != null) incomingCallScreen.SetActive(false);

        if (phoneManager != null)
            phoneManager.OpenCaller();
        else if (callerController.callerScreen != null)
            callerController.callerScreen.SetActive(true);
        else
            Debug.LogError("[IncomingCallManager] phoneManager and callerScreen are both missing — cannot show call UI.");

        if (callerController.callerScreen != null && !callerController.callerScreen.activeSelf)
        {
            Debug.LogWarning("[IncomingCallManager] Forcing callerScreen active before dialogue init.");
            callerController.callerScreen.SetActive(true);
        }

        bool storyCallOpened = false;
        if (activeCallType == ActiveCallType.Neighbor)
            storyCallOpened = callerController.OpenNeighborCall();
        else if (activeCallType == ActiveCallType.Mom)
            storyCallOpened = callerController.OpenMomStoryCall();
        else if (activeCallType == ActiveCallType.Microsoft)
            storyCallOpened = callerController.OpenMicrosoftCall();
        else
            storyCallOpened = callerController.OpenUnknownCall();

        if (!storyCallOpened && activeCallType != ActiveCallType.Unknown)
        {
            Debug.LogError("[IncomingCallManager] Story call failed to open. Check Console for CallerScript validation errors (CallData nodes, Dialogue UI refs).");
            ReturnToIdleHome();
            callShown = false;
            answerTransitionInProgress = false;
            return;
        }

        SetCallerEndCallAvailable(false);

        if (audioSource != null && ringtoneClip != null)
        {
            audioSource.clip = ringtoneClip;
            audioSource.loop = false;
            audioSource.Play();
        }

        answerTransitionInProgress = false;
    }

    public void DeclineIncoming()
    {
        if (IsIncomingStoryCallInProgress)
        {
            StartIncomingDeclineVibration();
            return;
        }

        Debug.Log("[IncomingCallManager] DeclineIncoming");
        AgentLog("A", "IncomingCallManager.DeclineIncoming", "Call declined", "{}");
        FinishIncomingCallWithoutConversation();
    }

    void FinishIncomingCallWithoutConversation()
    {
        ReturnToIdleHome();
        callShown = false;
        AdvanceStoryTimelineAfterCall();
    }

    public void EndCallerScreen()
    {
        if (answerTransitionInProgress && Time.unscaledTime - answerTransitionStartedAt < 0.35f)
        {
            Debug.LogWarning("[IncomingCallManager] EndCallerScreen ignored during answer transition.");
            AgentLog("A", "IncomingCallManager.EndCallerScreen", "Ignored during transition", "{}");
            return;
        }

        if (IsIncomingStoryCallInProgress)
        {
            StartIncomingDeclineVibration();
            return;
        }

        Debug.Log("[IncomingCallManager] EndCallerScreen");
        if (audioSource != null) audioSource.Stop();

        if (callerController != null && callerController.IsCallActive)
            callerController.DeclineCall();
        else
            HandleCallEnded();
    }

    void HandleBadCallEnding()
    {
        Debug.Log("[IncomingCallManager] Bad call ending detected.");
        phoneLockedByBadEnding = true;
        isPhoneBusy = true;
        AgentLog("H", "IncomingCallManager.HandleBadCallEnding", "Bad ending event received", "{}");
    }

    void HandleMicrosoftStoryCompleted()
    {
        storyPhase = StoryCallPhase.Complete;
        callPending = false;
        callShown = false;

        AgentLog("F", "IncomingCallManager.HandleMicrosoftStoryCompleted",
            "Microsoft story marked complete",
            "{\"storyPhase\":\"" + storyPhase + "\"}");
    }

    void HandleCallEnded()
    {
        Debug.Log("[IncomingCallManager] HandleCallEnded");
        ReturnToIdleHome();
        callShown = false;
        AdvanceStoryTimelineAfterCall();
    }

    void AdvanceStoryTimelineAfterCall()
    {
        if (activeCallType == ActiveCallType.Neighbor)
        {
            storyPhase = StoryCallPhase.WaitingForMom;
            phaseElapsed = 0f;
            callPending = false;
            Debug.Log($"[IncomingCallManager] Neighbor call finished. Mom timer reset ({delayBeforeMom}s).");
            AgentLog("F", "IncomingCallManager.AdvanceStoryTimelineAfterCall",
                "Neighbor finished",
                "{\"storyPhase\":\"" + storyPhase + "\"}");
        }
        else if (activeCallType == ActiveCallType.Mom)
        {
            storyPhase = StoryCallPhase.WaitingForMicrosoft;
            phaseElapsed = 0f;
            callPending = false;
            Debug.Log($"[IncomingCallManager] Mom call finished. Microsoft timer reset ({delayBeforeMicrosoft}s).");
            AgentLog("F", "IncomingCallManager.AdvanceStoryTimelineAfterCall",
                "Mom finished",
                "{\"storyPhase\":\"" + storyPhase + "\"}");
        }
        else if (activeCallType == ActiveCallType.Microsoft)
        {
            storyPhase = StoryCallPhase.Complete;
            callPending = false;
            Debug.Log("[IncomingCallManager] Microsoft call finished. Story complete.");
            AgentLog("F", "IncomingCallManager.AdvanceStoryTimelineAfterCall",
                "Microsoft finished",
                "{\"storyPhase\":\"" + storyPhase + "\"}");
        }

        activeCallType = ActiveCallType.None;
    }
}
