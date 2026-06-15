using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows an "incoming call" screen a set number of seconds after the game starts.
/// GREEN (Answer) opens the existing Caller Screen with the unknown avatar, plays the
/// ringtone once, and shows Answer/Decline buttons there. RED (Decline) just dismisses.
/// Either button on the Caller Screen stops the audio and returns to the Home Screen.
/// The call is only shown while the player is idle on the Home Screen (never during a chat).
/// </summary>
public class IncomingCallManager : MonoBehaviour
{
    enum StoryCallPhase
    {
        WaitingForNeighbor,
        NeighborActive,
        WaitingForMicrosoft,
        MicrosoftActive,
        Complete
    }

    enum ActiveCallType
    {
        None,
        Neighbor,
        Microsoft,
        Unknown
    }

    [Header("Managers")]
    public PhoneUIManager phoneManager;
    public CallerScript callerController;

    [Header("Incoming Call Screen")]
    public GameObject incomingCallScreen;
    public Button incomingAnswerButton;   // GREEN
    public Button incomingDeclineButton;  // RED
    public TMP_Text incomingCallerName;
    public Image incomingCallerAvatar;

    [Header("Caller Screen buttons")]
    public GameObject callerEndCallButton;   // existing EndCallButton (hidden during this flow)
    public Button callerAnswerButton;        // GREEN (added to Caller Screen)
    public Button callerDeclineButton;       // RED (added to Caller Screen)

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip ringtoneClip;           // getFromTcom sound (plays once on answer)

    [Header("Incoming Ringtone")]
    public AudioSource ringtoneSource;       // dedicated source for the looping ringtone
    public AudioClip incomingRingtoneClip;   // iPhone-style ringtone (loops while ringing)

    [Header("Timing")]
    [Tooltip("Seconds after game start before the Neighbor call can ring (default 180 = 3 minutes).")]
    public float delaySeconds = 180f;

    [Header("Story Call Timing")]
    [Tooltip("Seconds after Neighbor call ends before Microsoft call can ring (default 240 = 4 minutes).")]
    public float delayBeforeMicrosoft = 240f;

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

        // Dedicated source for the looping ringtone so it never fights the answer sound.
        if (ringtoneSource == null) ringtoneSource = gameObject.AddComponent<AudioSource>();
        ringtoneSource.playOnAwake = false;
        ringtoneSource.loop = true;

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

        if (callerAnswerButton != null)
        {
            callerAnswerButton.onClick.RemoveListener(EndCallerScreen);
            callerAnswerButton.onClick.AddListener(EndCallerScreen);
        }

        if (callerDeclineButton != null)
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

        Debug.Log($"[IncomingCallManager] Timers started. Neighbor delay={delaySeconds}s, Microsoft delay={delayBeforeMicrosoft}s, phase={storyPhase}");
        AgentLog("C", "IncomingCallManager.Start", "Timers initialized",
            "{\"delaySeconds\":" + delaySeconds + ",\"delayBeforeMicrosoft\":" + delayBeforeMicrosoft + ",\"phase\":\"" + storyPhase + "\"}");
    }

    void OnDestroy()
    {
        if (callerController != null)
        {
            callerController.OnCallEnded -= HandleCallEnded;
            callerController.OnMicrosoftCallCompleted -= HandleMicrosoftStoryCompleted;
        }
    }

    void Update()
    {
        if (!timersInitialized || storyPhase == StoryCallPhase.Complete || callShown)
            return;

        // Timer always ticks in the background, even while the player is in a chat.
        phaseElapsed += Time.deltaTime;

        float requiredDelay = storyPhase == StoryCallPhase.WaitingForNeighbor
            ? delaySeconds
            : delayBeforeMicrosoft;

        if (requiredDelay <= 0f)
        {
            Debug.LogWarning($"[IncomingCallManager] Required delay is {requiredDelay}. Set delaySeconds / delayBeforeMicrosoft to a positive value.");
            return;
        }

        if (!callPending && phaseElapsed >= requiredDelay)
        {
            callPending = true;
            Debug.Log($"[IncomingCallManager] Call pending for phase {storyPhase} after {phaseElapsed:F1}s (required {requiredDelay}s). Waiting for Home.");
            AgentLog("C", "IncomingCallManager.Update", "Call pending",
                "{\"phase\":\"" + storyPhase + "\",\"phaseElapsed\":" + phaseElapsed + ",\"requiredDelay\":" + requiredDelay + "}");
        }

        // If the timer already finished, ring as soon as the player is idle on Home.
        if (callPending && IsIdleOnHome())
            TriggerPendingCall();
    }

    bool IsIdleOnHome()
    {
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
        EnsureIncomingAnswerButtonVisible();

        // Start the looping ringtone while the phone is ringing.
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

    void ShowIncomingCall()
    {
        activeCallType = ActiveCallType.Unknown;
        callShown = true;

        ApplyIncomingCallPresentation("UNKNOWN NUMBER",
            callerController != null ? callerController.unknownAvatar : null);

        PresentIncomingCallScreen();
    }

    bool IsIncomingUiButton(Button button)
    {
        if (button == null || incomingCallScreen == null)
            return false;

        return button.transform.IsChildOf(incomingCallScreen.transform);
    }

    void EnsureIncomingAnswerButtonVisible()
    {
        if (incomingAnswerButton != null)
            incomingAnswerButton.gameObject.SetActive(true);

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

    void RestoreCallerScreenButtons()
    {
        SetCallerScreenButtonVisible(callerAnswerButton, false);
        SetCallerScreenButtonVisible(callerDeclineButton, false);
        if (callerEndCallButton != null) callerEndCallButton.SetActive(true);
        EnsureIncomingAnswerButtonVisible();
    }

    void ReturnToIdleHome()
    {
        StopRingtone();

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

    // GREEN on incoming call screen — canonical entry point for answering.
    public void AnswerIncoming()
    {
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

        // Swap the single end-call button for the Answer/Decline pair on the caller screen only.
        if (callerEndCallButton != null) callerEndCallButton.SetActive(false);
        SetCallerScreenButtonVisible(callerAnswerButton, true);
        SetCallerScreenButtonVisible(callerDeclineButton, true);

        // Play the ringtone once.
        if (audioSource != null && ringtoneClip != null)
        {
            audioSource.clip = ringtoneClip;
            audioSource.loop = false;
            audioSource.Play();
        }

        answerTransitionInProgress = false;
    }

    // RED on incoming call screen
    public void DeclineIncoming()
    {
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

    // Answer or Decline on the Caller Screen: stop audio, close, go Home.
    public void EndCallerScreen()
    {
        if (answerTransitionInProgress && Time.unscaledTime - answerTransitionStartedAt < 0.35f)
        {
            Debug.LogWarning("[IncomingCallManager] EndCallerScreen ignored during answer transition.");
            AgentLog("A", "IncomingCallManager.EndCallerScreen", "Ignored during transition", "{}");
            return;
        }

        Debug.Log("[IncomingCallManager] EndCallerScreen");
        if (audioSource != null) audioSource.Stop();

        if (callerController != null && callerController.IsCallActive)
            callerController.CloseCaller();
        else
            HandleCallEnded();
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
            storyPhase = StoryCallPhase.WaitingForMicrosoft;
            phaseElapsed = 0f;
            callPending = false;
            Debug.Log($"[IncomingCallManager] Neighbor call finished. Microsoft timer reset ({delayBeforeMicrosoft}s).");
            AgentLog("F", "IncomingCallManager.AdvanceStoryTimelineAfterCall",
                "Neighbor finished",
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
