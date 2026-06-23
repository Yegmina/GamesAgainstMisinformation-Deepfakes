using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using UnityEngine.UI;

[Serializable]
public class CallChoice
{
    public string text;
    public int nextNodeIndex = -1;
}

[Serializable]
public class CallNode
{
    public string speechText;
    public AudioClip voiceAudio;
    public CallChoice[] choices;
    public int nextNodeIndex = -1;

    [Tooltip("When true, reaching this node triggers the red horror bad-ending overlay.")]
    public bool isBadEnding;
}

[Serializable]
public class CallData
{
    public string displayName;
    public CallNode[] nodes;
    public int startNodeIndex;
}

[Serializable]
public class OutgoingCallNodeData
{
    public string displayName;
    public CallNode[] nodes;
}

[Serializable]
public class FatherCallBranch
{
    public string choiceText;
    public CallNode[] nodes;
    public AudioClip disconnectSound;
    public bool hardCutAudioOnEnd;
}

[Serializable]
public class FatherOutgoingCallData
{
    public string displayName = "DAD";
    public CallNode[] initialNodes;
    public FatherCallBranch panicBranch = new FatherCallBranch
    {
        choiceText = "Panic"
    };
    public FatherCallBranch calmBranch = new FatherCallBranch
    {
        choiceText = "Stay Calm"
    };
    public float choiceWindowSeconds = 3f;
}

public enum StoryCallId
{
    None,
    Neighbor,
    Mom,
    Microsoft
}

public class CallerScript : MonoBehaviour
{
    public TMP_Text callerName;
    public Image callerAvatar;
    public GameObject callerScreen;

    public Sprite momAvatar;
    public Sprite dadAvatar;
    public Sprite sarahAvatar;
    public Sprite brotherAvatar;
    public Sprite unknownAvatar;
    public Sprite neighborAvatar;
    public Sprite microsoftAvatar;

    [Header("Story Calls")]
    public CallData neighborCallData;
    public CallData momCallData;
    public CallData microsoftCallData;

    [Header("Call Dialogue")]
    public TMP_Text dialogueText;
    public GameObject choicesPanel;
    public Transform choicesContent;
    public GameObject choiceButtonPrefab;
    public float autoAdvanceDelay = 1.5f;

    [Header("Bad Ending")]
    public GameObject badEndingOverlay;
    public TextMeshProUGUI wrongChoiceText;
    [Tooltip("Root UI element to shake during a bad ending (defaults to callerScreen).")]
    public RectTransform callShakeTarget;
    [Tooltip("How fast the red overlay alpha flickers (higher = faster).")]
    public float flashSpeed = 8f;
    [Tooltip("How long the horror overlay flashes before the call ends.")]
    public float badEndingDuration = 3f;
    [Tooltip("Screen-shake magnitude in UI pixels during a bad ending.")]
    public float shakeIntensity = 25f;
    [Tooltip("Horror sting played instantly when a bad ending node is reached.")]
    public AudioClip horrorSound;
    [Tooltip("Door-knocking sound played right BEFORE the screamer when the neighbor (Mr. Henderson) call reaches its scary bad ending (e.g. the 'open the door' wrong answer).")]
    public AudioClip doorKnockingClip;
    [Tooltip("Safety cap (seconds) on how long to wait for the door knock before the screamer fires.")]
    public float doorKnockMaxWait = 3.5f;
    [Tooltip("Volume fade-out duration when the bad-ending sequence ends.")]
    public float horrorSoundFadeOut = 0.35f;

    [Header("Managers")]
    public IncomingCallManager incomingCallManager;
    public MissionSidebarManager missionManager;

    [Header("Audio")]
    public AudioSource audioSource;
    [FormerlySerializedAs("outgoingRingingClip")]
    public AudioClip outgoingRingSound;
    public AudioClip subscriberUnavailableSound;

    [Header("Legacy Outgoing Calls")]
    public float outgoingRingDuration = 3f;
    public float unavailableMessageDuration = 2f;
    public Color unavailableTextColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    [Header("Outgoing Node Calls")]
    public OutgoingCallNodeData outgoingMomCallData = new OutgoingCallNodeData
    {
        displayName = "MOM"
    };
    public OutgoingCallNodeData outgoingBrotherCallData = new OutgoingCallNodeData
    {
        displayName = "BROTHER"
    };
    public float outgoingContactTimeout = 15f;

    [Header("Outgoing Father Branching Call")]
    public FatherOutgoingCallData outgoingFatherCallData = new FatherOutgoingCallData
    {
        displayName = "DAD",
        choiceWindowSeconds = 3f,
        initialNodes = new[]
        {
            new CallNode
            {
                speechText = "Dad?! Oh thank god you answered! Something is seriously wrong. I tried calling Mom and my brother, but... but something is deeply wrong with their lines! The voices, the responses... it's like a distorted glitch! I think I'm being hacked, Dad! What is happening?!"
            },
            new CallNode
            {
                speechText = "Son? Hey, breathe. Calm down. There is a massive spoofing attack on the network right now, they are stealing biometric data. Your screaming is glitching my audio filter, I can barely hear you. Calm your voice and tell me: did they ask you for money or security pins?"
            }
        },
        panicBranch = new FatherCallBranch
        {
            choiceText = "Panic",
            nodes = new[]
            {
                new CallNode
                {
                    speechText = "I don't care about pins! I'm losing my mind here! Just tell me where you are, should I come over?! Are you at home?!"
                },
                new CallNode
                {
                    speechText = "Voice metrics captured. Geolocation pinpointed. Thank you for your cooperation."
                },
                new CallNode
                {
                    speechText = "What?! Wait, you're not my dad! Who is this?!"
                }
            }
        },
        calmBranch = new FatherCallBranch
        {
            choiceText = "Stay Calm",
            hardCutAudioOnEnd = true,
            nodes = new[]
            {
                new CallNode
                {
                    speechText = "No, they didn't. Wait... Dad, why does your voice sound so flat? Tell me our dog's name."
                },
                new CallNode
                {
                    speechText = "Our... dog's name? Context generation error. Query not found... found... please repeat the query..."
                },
                new CallNode
                {
                    speechText = "Oh my god... It's not him. You're a deepfake."
                }
            }
        }
    };

    public Action OnCallEnded;
    public Action OnCallBadEnding;
    public Action OnNeighborCallCompleted;
    public Action OnMomCallCompleted;
    public Action OnMicrosoftCallCompleted;

    CallData activeCallData;
    StoryCallId activeStoryCallId = StoryCallId.None;
    int currentNodeIndex = -1;
    int previousNodeIndex = -1;
    bool dialogueActive;
    bool callActive;
    bool badEndingPlaying;
    Coroutine nodeFlowRoutine;
    Coroutine badEndingRoutine;
    Coroutine shakeRoutine;
    Coroutine legacyOutgoingRoutine;
    Coroutine callingEllipsisRoutine;
    Coroutine outgoingNodeRoutine;
    Coroutine outgoingTimeoutRoutine;
    Coroutine fatherChoiceRoutine;
    float callUiActivatedAt;
    bool legacyOutgoingActive;
    bool outgoingUnavailablePhase;
    bool outgoingNodeCallActive;
    bool fatherChoiceActive;
    Image badEndingOverlayImage;
    RectTransform shakeTarget;
    Vector2 shakeHomePosition;

    public bool IsCallActive => callActive;
    public StoryCallId ActiveStoryCallId => activeStoryCallId;
    public bool IsOutgoingDialingPhase => legacyOutgoingActive && !outgoingUnavailablePhase;

    static readonly Color StandardDialogueTextColor = Color.white;

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
        if (incomingCallManager == null)
            incomingCallManager = FindFirstObjectByType<IncomingCallManager>();
        if (missionManager == null)
            missionManager = FindFirstObjectByType<MissionSidebarManager>();

        HideBadEndingOverlay();
    }

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;

        if (badEndingOverlay != null)
            badEndingOverlayImage = badEndingOverlay.GetComponent<Image>();

        CacheShakeTarget();

        // Remove the semi-transparent panel background behind the call answer
        // buttons so the choices are separated by clean empty space instead of an
        // ugly translucent gray strip showing through the gaps.
        if (choicesContent != null)
        {
            Image choicesBg = choicesContent.GetComponent<Image>();
            if (choicesBg != null) choicesBg.enabled = false;
        }
        if (choicesPanel != null)
        {
            Image panelBg = choicesPanel.GetComponent<Image>();
            if (panelBg != null) panelBg.enabled = false;
        }
    }

    void CacheShakeTarget()
    {
        if (callShakeTarget != null)
            shakeTarget = callShakeTarget;
        else if (callerScreen != null)
            shakeTarget = callerScreen.GetComponent<RectTransform>();

        if (shakeTarget != null)
            shakeHomePosition = shakeTarget.anchoredPosition;
    }

    void ActivateConversationScreen()
    {
        if (incomingCallManager != null && incomingCallManager.incomingCallScreen != null)
            incomingCallManager.incomingCallScreen.SetActive(false);

        if (callerScreen != null)
            callerScreen.SetActive(true);
    }

    Transform GetChoicesContainer()
    {
        if (choicesContent == null)
        {
            Debug.LogError("[CallerScript] choicesContent (ChoicesContainer) is not assigned.");
            return null;
        }

        if (callerScreen == null || !choicesContent.IsChildOf(callerScreen.transform))
        {
            Debug.LogError("[CallerScript] choicesContent must be a child of PhoneCallScreen (callerScreen).");
            return null;
        }

        return choicesContent;
    }

    void ShowCallerScreenForOutgoing()
    {
        if (incomingCallManager != null && incomingCallManager.phoneManager != null)
        {
            PhoneUIManager phoneManager = incomingCallManager.phoneManager;
            if (phoneManager.homeScreen != null) phoneManager.homeScreen.SetActive(false);
            if (phoneManager.galleryScreen != null) phoneManager.galleryScreen.SetActive(false);
            if (phoneManager.messagesScreen != null) phoneManager.messagesScreen.SetActive(false);
            if (phoneManager.callsScreen != null) phoneManager.callsScreen.SetActive(false);
            if (phoneManager.browserScreen != null) phoneManager.browserScreen.SetActive(false);
            if (phoneManager.SocialMediaScreen != null) phoneManager.SocialMediaScreen.SetActive(false);
            if (phoneManager.chatScreen != null) phoneManager.chatScreen.SetActive(false);
            if (phoneManager.callerScreen != null) phoneManager.callerScreen.SetActive(true);
        }
        else if (callerScreen != null)
            callerScreen.SetActive(true);
    }

    void ApplyStandardDialogueTextStyle()
    {
        if (dialogueText == null)
            return;

        dialogueText.fontStyle = FontStyles.Normal;
        dialogueText.color = StandardDialogueTextColor;
    }

    void RestoreDialogueTextDefaults()
    {
        if (dialogueText == null)
            return;

        ApplyStandardDialogueTextStyle();
        dialogueText.text = string.Empty;
    }

    void ShowSubscriberUnavailableText()
    {
        if (dialogueText == null)
            return;

        dialogueText.text = "SUBSCRIBER UNAVAILABLE";
        dialogueText.fontStyle = FontStyles.Bold;
        dialogueText.color = unavailableTextColor;
    }

    void PlayOutgoingRinging()
    {
        if (audioSource == null || outgoingRingSound == null)
            return;

        audioSource.Stop();
        audioSource.loop = true;
        audioSource.volume = 1f;
        audioSource.clip = outgoingRingSound;
        audioSource.Play();
    }

    void PlaySubscriberUnavailableSound()
    {
        if (audioSource == null || subscriberUnavailableSound == null)
            return;

        audioSource.Stop();
        audioSource.loop = false;
        audioSource.volume = 1f;
        audioSource.clip = subscriberUnavailableSound;
        audioSource.Play();
    }

    void StopOutgoingRingSoundIfPlaying()
    {
        if (audioSource == null || outgoingRingSound == null)
            return;

        if (audioSource.isPlaying && audioSource.clip == outgoingRingSound)
            audioSource.Stop();
    }

    void StopSubscriberUnavailableSoundIfPlaying()
    {
        if (audioSource == null || subscriberUnavailableSound == null)
            return;

        if (audioSource.isPlaying && audioSource.clip == subscriberUnavailableSound)
            audioSource.Stop();
    }

    public void StopOutgoingRinging()
    {
        StopOutgoingRingSoundIfPlaying();
    }

    void StopLegacyOutgoingAudio()
    {
        StopOutgoingRingSoundIfPlaying();
        StopSubscriberUnavailableSoundIfPlaying();
    }

    IEnumerator CallingEllipsisRoutine()
    {
        const string baseText = "Calling";
        int dots = 0;

        while (legacyOutgoingActive && !outgoingUnavailablePhase)
        {
            dots = (dots % 3) + 1;
            if (dialogueText != null)
            {
                dialogueText.text = baseText + new string('.', dots);
                ApplyStandardDialogueTextStyle();
            }

            yield return new WaitForSecondsRealtime(0.4f);
        }
    }

    IEnumerator LegacyOutgoingCallRoutine()
    {
        if (outgoingRingDuration > 0f)
            yield return new WaitForSecondsRealtime(outgoingRingDuration);

        if (!callActive || outgoingUnavailablePhase)
            yield break;

        outgoingUnavailablePhase = true;
        legacyOutgoingActive = false;

        if (callingEllipsisRoutine != null)
        {
            StopCoroutine(callingEllipsisRoutine);
            callingEllipsisRoutine = null;
        }

        StopOutgoingRingSoundIfPlaying();
        ShowSubscriberUnavailableText();
        PlaySubscriberUnavailableSound();

        if (unavailableMessageDuration > 0f)
            yield return new WaitForSecondsRealtime(unavailableMessageDuration);

        FinishLegacyOutgoingCall("legacy outgoing ended");
    }

    void StartLegacyOutgoingCall(string displayName, Sprite avatar)
    {
        StopLegacyOutgoingCallEffects();

        ShowCallerScreenForOutgoing();
        HideBadEndingOverlay();
        ClearDialogueChoices();
        SetChoicesContainerVisible(false);
        RestoreDialogueTextDefaults();

        if (callerName != null)
            callerName.text = displayName;
        if (callerAvatar != null && avatar != null)
            callerAvatar.sprite = avatar;

        dialogueActive = false;
        activeCallData = null;
        activeStoryCallId = StoryCallId.None;
        currentNodeIndex = -1;
        callActive = true;
        legacyOutgoingActive = true;
        outgoingUnavailablePhase = false;
        callUiActivatedAt = Time.unscaledTime;

        if (dialogueText != null)
            dialogueText.gameObject.SetActive(true);

        PlayOutgoingRinging();
        callingEllipsisRoutine = StartCoroutine(CallingEllipsisRoutine());
        legacyOutgoingRoutine = StartCoroutine(LegacyOutgoingCallRoutine());
    }

    void StopLegacyOutgoingCallEffects()
    {
        legacyOutgoingActive = false;
        outgoingUnavailablePhase = false;

        if (legacyOutgoingRoutine != null)
        {
            StopCoroutine(legacyOutgoingRoutine);
            legacyOutgoingRoutine = null;
        }

        if (callingEllipsisRoutine != null)
        {
            StopCoroutine(callingEllipsisRoutine);
            callingEllipsisRoutine = null;
        }

        StopLegacyOutgoingAudio();
        RestoreDialogueTextDefaults();
    }

    void FinishLegacyOutgoingCall(string reason)
    {
        legacyOutgoingRoutine = null;
        StopLegacyOutgoingCallEffects();

        CloseCaller(reason);
    }

    void StopOutgoingNodeCallEffects()
    {
        outgoingNodeCallActive = false;
        fatherChoiceActive = false;

        if (outgoingNodeRoutine != null)
        {
            StopCoroutine(outgoingNodeRoutine);
            outgoingNodeRoutine = null;
        }

        if (outgoingTimeoutRoutine != null)
        {
            StopCoroutine(outgoingTimeoutRoutine);
            outgoingTimeoutRoutine = null;
        }

        if (fatherChoiceRoutine != null)
        {
            StopCoroutine(fatherChoiceRoutine);
            fatherChoiceRoutine = null;
        }

        ClearDialogueChoices();
        SetChoicesContainerVisible(false);
        StopLegacyOutgoingAudio();
    }

    void PrepareOutgoingNodeCall(string displayName, Sprite avatar, bool allowManualHangup)
    {
        StopLegacyOutgoingCallEffects();
        StopOutgoingNodeCallEffects();

        ShowCallerScreenForOutgoing();
        HideBadEndingOverlay();
        ClearDialogueChoices();
        SetChoicesContainerVisible(false);
        RestoreDialogueTextDefaults();

        if (callerName != null)
            callerName.text = displayName;
        if (callerAvatar != null && avatar != null)
            callerAvatar.sprite = avatar;

        dialogueActive = false;
        activeCallData = null;
        activeStoryCallId = StoryCallId.None;
        currentNodeIndex = -1;
        previousNodeIndex = -1;
        callActive = true;
        outgoingNodeCallActive = true;
        callUiActivatedAt = Time.unscaledTime;

        if (dialogueText != null)
            dialogueText.gameObject.SetActive(true);

        if (incomingCallManager != null)
            incomingCallManager.SetCallerEndCallAvailable(allowManualHangup);
    }

    void StartOutgoingTimedNodeCall(OutgoingCallNodeData callData, Sprite avatar, string fallbackName)
    {
        string displayName = callData != null && !string.IsNullOrWhiteSpace(callData.displayName)
            ? callData.displayName
            : fallbackName;

        PrepareOutgoingNodeCall(displayName, avatar, true);
        outgoingNodeRoutine = StartCoroutine(OutgoingTimedNodeCallRoutine(callData));
        outgoingTimeoutRoutine = StartCoroutine(OutgoingContactTimeoutRoutine());
    }

    IEnumerator OutgoingTimedNodeCallRoutine(OutgoingCallNodeData callData)
    {
        yield return OutgoingDialingDelay();

        if (!outgoingNodeCallActive || !callActive)
            yield break;

        yield return PlayOutgoingNodeArray(callData != null ? callData.nodes : null);
    }

    IEnumerator OutgoingContactTimeoutRoutine()
    {
        yield return new WaitForSecondsRealtime(outgoingContactTimeout);

        if (outgoingNodeCallActive && callActive)
            FinishOutgoingNodeCall("outgoing contact timed out");
    }

    void StartFatherOutgoingCall()
    {
        string displayName = outgoingFatherCallData != null && !string.IsNullOrWhiteSpace(outgoingFatherCallData.displayName)
            ? outgoingFatherCallData.displayName
            : "DAD";

        PrepareOutgoingNodeCall(displayName, dadAvatar, true);
        outgoingNodeRoutine = StartCoroutine(FatherOutgoingCallRoutine());
    }

    IEnumerator FatherOutgoingCallRoutine()
    {
        yield return OutgoingDialingDelay();

        if (!outgoingNodeCallActive || !callActive)
            yield break;

        yield return PlayOutgoingNodeArray(outgoingFatherCallData != null ? outgoingFatherCallData.initialNodes : null);

        if (!outgoingNodeCallActive || !callActive)
            yield break;

        fatherChoiceRoutine = StartCoroutine(FatherChoiceWindowRoutine());
    }

    IEnumerator OutgoingDialingDelay()
    {
        PlayOutgoingRinging();

        float elapsed = 0f;
        while (elapsed < outgoingRingDuration)
        {
            if (!outgoingNodeCallActive || !callActive)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        StopOutgoingRingSoundIfPlaying();
    }

    IEnumerator PlayOutgoingNodeArray(CallNode[] nodes)
    {
        if (nodes == null)
            yield break;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (!outgoingNodeCallActive || !callActive)
                yield break;

            CallNode node = nodes[i];
            if (dialogueText != null)
            {
                dialogueText.text = node != null ? node.speechText ?? string.Empty : string.Empty;
                ApplyStandardDialogueTextStyle();
            }

            float voiceDuration = node != null ? PlayNodeVoice(node.voiceAudio) : 0f;
            if (voiceDuration > 0f)
                yield return new WaitForSeconds(voiceDuration);
            else if (autoAdvanceDelay > 0f)
                yield return new WaitForSeconds(autoAdvanceDelay);
        }
    }

    IEnumerator FatherChoiceWindowRoutine()
    {
        fatherChoiceActive = true;
        if (incomingCallManager != null)
            incomingCallManager.SetCallerEndCallAvailable(false);

        SpawnFatherChoiceButtons();

        float choiceWindow = outgoingFatherCallData != null && outgoingFatherCallData.choiceWindowSeconds > 0f
            ? outgoingFatherCallData.choiceWindowSeconds
            : 3f;

        float elapsed = 0f;
        while (elapsed < choiceWindow && fatherChoiceActive && outgoingNodeCallActive && callActive)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (fatherChoiceActive && outgoingNodeCallActive && callActive)
            SelectFatherBranch(outgoingFatherCallData != null ? outgoingFatherCallData.panicBranch : null);
    }

    void SpawnFatherChoiceButtons()
    {
        Transform container = GetChoicesContainer();
        if (container == null || choiceButtonPrefab == null)
            return;

        SetChoicesContainerVisible(true);
        ClearDialogueChoices();

        CreateFatherChoiceButton(outgoingFatherCallData != null ? outgoingFatherCallData.panicBranch : null, "Option A / Panic");
        CreateFatherChoiceButton(outgoingFatherCallData != null ? outgoingFatherCallData.calmBranch : null, "Option B / Calm");
    }

    void CreateFatherChoiceButton(FatherCallBranch branch, string fallbackText)
    {
        Transform container = GetChoicesContainer();
        if (container == null || choiceButtonPrefab == null)
            return;

        GameObject buttonObj = Instantiate(choiceButtonPrefab, container);
        buttonObj.transform.SetParent(container, false);

        TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = branch != null && !string.IsNullOrWhiteSpace(branch.choiceText)
                ? branch.choiceText
                : fallbackText;

        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => SelectFatherBranch(branch));
    }

    void SelectFatherBranch(FatherCallBranch branch)
    {
        if (!fatherChoiceActive || !outgoingNodeCallActive || !callActive)
            return;

        fatherChoiceActive = false;
        ClearDialogueChoices();
        SetChoicesContainerVisible(false);

        if (fatherChoiceRoutine != null)
        {
            StopCoroutine(fatherChoiceRoutine);
            fatherChoiceRoutine = null;
        }

        if (branch == (outgoingFatherCallData != null ? outgoingFatherCallData.panicBranch : null))
        {
            if (GlobalCanvasPersistent.Instance != null)
            {
                GlobalCanvasPersistent.Instance.AddParanoia(10);
            }
        }

        outgoingNodeRoutine = StartCoroutine(FatherBranchRoutine(branch));
    }

    IEnumerator FatherBranchRoutine(FatherCallBranch branch)
    {
        yield return PlayOutgoingNodeArray(branch != null ? branch.nodes : null);

        if (branch != null && branch.disconnectSound != null && audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = branch.disconnectSound;
            audioSource.Play();
            yield return new WaitForSeconds(branch.disconnectSound.length);
        }
        else if (branch != null && branch.hardCutAudioOnEnd && audioSource != null)
        {
            audioSource.Stop();
        }

        if (outgoingNodeCallActive && callActive)
            FinishOutgoingNodeCall("father branch ended");
    }

    void FinishOutgoingNodeCall(string reason)
    {
        StopOutgoingNodeCallEffects();
        CloseCaller(reason);
    }

    float PlayNodeVoice(AudioClip clip)
    {
        if (audioSource == null || clip == null) return 0f;

        audioSource.Stop();
        audioSource.loop = false;
        audioSource.clip = clip;
        audioSource.Play();
        return clip.length;
    }

    // Outgoing calls.
    public void OpenMomCall() => StartOutgoingTimedNodeCall(outgoingMomCallData, momAvatar, "MOM");
    public void OpenDadCall() => StartFatherOutgoingCall();
    public void OpenSarahCall() => StartLegacyOutgoingCall("SARAH", sarahAvatar);
    public void OpenBrotherCall() => StartOutgoingTimedNodeCall(outgoingBrotherCallData, brotherAvatar, "BROTHER");

    public bool OpenUnknownCall()
    {
        if (callerScreen == null)
        {
            Debug.LogError("[CallerScript] callerScreen is not assigned.");
            return false;
        }

        ActivateConversationScreen();
        if (callerName != null) callerName.text = "UNKNOWN NUMBER";
        if (callerAvatar != null && unknownAvatar != null) callerAvatar.sprite = unknownAvatar;
        callActive = true;
        callUiActivatedAt = Time.unscaledTime;
        return true;
    }

    public bool OpenNeighborCall()
    {
        return OpenStoryCall(neighborCallData, neighborAvatar, "neighborCallData", StoryCallId.Neighbor);
    }

    public bool OpenMomStoryCall()
    {
        return OpenStoryCall(momCallData, momAvatar, "momCallData", StoryCallId.Mom);
    }

    public bool OpenMicrosoftCall()
    {
        return OpenStoryCall(microsoftCallData, microsoftAvatar, "microsoftCallData", StoryCallId.Microsoft);
    }

    bool ValidateCallData(CallData callData, string label)
    {
        if (callData == null)
        {
            Debug.LogError($"[CallerScript] {label} is null.");
            return false;
        }

        if (callData.nodes == null || callData.nodes.Length == 0)
        {
            Debug.LogError($"[CallerScript] {label}.nodes is empty.");
            return false;
        }

        if (callData.startNodeIndex < 0 || callData.startNodeIndex >= callData.nodes.Length)
        {
            Debug.LogError($"[CallerScript] {label}.startNodeIndex is outside nodes array.");
            return false;
        }

        return true;
    }

    bool ValidateDialogueUi(CallData callData, string label)
    {
        bool hasChoiceNodes = false;
        if (callData?.nodes != null)
        {
            foreach (CallNode node in callData.nodes)
            {
                if (node?.choices != null && node.choices.Length > 0)
                {
                    hasChoiceNodes = true;
                    break;
                }
            }
        }

        if (callerScreen == null)
        {
            Debug.LogError("[CallerScript] callerScreen is not assigned.");
            return false;
        }

        if (dialogueText == null)
            Debug.LogWarning($"[CallerScript] {label}: Dialogue Text is not assigned.");

        if (hasChoiceNodes && (choicesContent == null || choiceButtonPrefab == null))
        {
            Debug.LogError($"[CallerScript] {label}: assign Choices Content (ChoicesContainer) and Choice Button Prefab.");
            return false;
        }

        if (hasChoiceNodes && GetChoicesContainer() == null)
            return false;

        return true;
    }

    bool OpenStoryCall(CallData callData, Sprite avatar, string label, StoryCallId storyCallId)
    {
        if (!ValidateCallData(callData, label))
            return false;

        if (!ValidateDialogueUi(callData, label))
            return false;

        if (dialogueActive && callActive && activeCallData == callData)
        {
            AgentLog("F", "CallerScript.OpenStoryCall", "Ignored duplicate open",
                "{\"storyCallId\":\"" + storyCallId + "\",\"nodeIndex\":" + currentNodeIndex + "}");
            return true;
        }

        ActivateConversationScreen();
        HideBadEndingOverlay();

        if (callerName != null)
            callerName.text = string.IsNullOrEmpty(callData.displayName) ? "UNKNOWN NUMBER" : callData.displayName;
        if (callerAvatar != null && avatar != null) callerAvatar.sprite = avatar;

        activeCallData = callData;
        activeStoryCallId = storyCallId;
        dialogueActive = true;
        callActive = true;
        callUiActivatedAt = Time.unscaledTime;
        currentNodeIndex = callData.startNodeIndex;
        previousNodeIndex = -1;

        AgentLog("E", "CallerScript.OpenStoryCall", "Story call started",
            "{\"storyCallId\":\"" + storyCallId + "\",\"startNodeIndex\":" + currentNodeIndex +
            ",\"nodeCount\":" + callData.nodes.Length + "}");

        ShowDialogueNode(currentNodeIndex);
        return true;
    }

    void ShowDialogueNode(int nodeIndex)
    {
        if (!dialogueActive || activeCallData == null || activeCallData.nodes == null)
        {
            CloseCaller("missing dialogue data");
            return;
        }

        if (nodeIndex < 0 || nodeIndex >= activeCallData.nodes.Length)
        {
            CloseCaller("node index out of range");
            return;
        }

        if (nodeFlowRoutine != null)
        {
            StopCoroutine(nodeFlowRoutine);
            nodeFlowRoutine = null;
        }

        if (nodeIndex != currentNodeIndex)
            previousNodeIndex = currentNodeIndex;
        currentNodeIndex = nodeIndex;
        CallNode node = activeCallData.nodes[nodeIndex];
        bool hasChoices = node.choices != null && node.choices.Length > 0;

        if (dialogueText != null)
            dialogueText.text = node.speechText ?? string.Empty;

        ClearDialogueChoices();
        SetChoicesContainerVisible(false);

        float voiceDuration = PlayNodeVoice(node.voiceAudio);
        nodeFlowRoutine = StartCoroutine(ProcessNodeAfterAudio(node, nodeIndex, voiceDuration, hasChoices));
    }

    IEnumerator ProcessNodeAfterAudio(CallNode node, int nodeIndex, float voiceDuration, bool hasChoices)
    {
        if (voiceDuration > 0f)
            yield return new WaitForSeconds(voiceDuration);
        else if (!hasChoices && !node.isBadEnding && autoAdvanceDelay > 0f)
            yield return new WaitForSeconds(autoAdvanceDelay);

        if (!dialogueActive || !callActive || currentNodeIndex != nodeIndex)
            yield break;

        if (hasChoices)
        {
            SpawnChoiceButtons(node, nodeIndex);
            yield break;
        }

        if (node.isBadEnding)
        {
            StartBadEndingSequence();
            yield break;
        }

        if (node.nextNodeIndex < 0)
        {
            CloseCaller("conversation ended");
            yield break;
        }

        if (ShouldCompleteMicrosoftCallInsteadOfLooping(node, node.nextNodeIndex))
        {
            CloseCaller("microsoft call completed");
            yield break;
        }

        ShowDialogueNode(node.nextNodeIndex);
    }

    bool ShouldCompleteMicrosoftCallInsteadOfLooping(CallNode node, int nextNodeIndex)
    {
        if (activeStoryCallId != StoryCallId.Microsoft || activeCallData == null)
            return false;

        if (nextNodeIndex != activeCallData.startNodeIndex)
            return false;

        AgentLog("F", "CallerScript.ShouldCompleteMicrosoftCallInsteadOfLooping",
            "Blocked Microsoft dialogue loop to start node",
            "{\"fromNodeIndex\":" + currentNodeIndex + ",\"nextNodeIndex\":" + nextNodeIndex + "}");
        return true;
    }

    void SpawnChoiceButtons(CallNode node, int nodeIndex)
    {
        Transform container = GetChoicesContainer();
        if (container == null || choiceButtonPrefab == null)
        {
            CloseCaller("missing choice UI");
            return;
        }

        SetChoicesContainerVisible(true);
        ClearDialogueChoices();

        for (int i = 0; i < node.choices.Length; i++)
        {
            CallChoice choice = node.choices[i];
            GameObject buttonObj = Instantiate(choiceButtonPrefab, container);
            buttonObj.transform.SetParent(container, false);

            RectTransform rt = buttonObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }

            TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = choice.text;

            Button button = buttonObj.GetComponent<Button>();
            if (button == null) continue;

            int nextIndex = choice.nextNodeIndex;
            button.onClick.AddListener(() => OnDialogueChoiceSelected(nextIndex));
        }

        AgentLog("E", "CallerScript.SpawnChoiceButtons", "Choices spawned",
            "{\"nodeIndex\":" + nodeIndex + ",\"choiceCount\":" + node.choices.Length + "}");
    }

    void OnDialogueChoiceSelected(int nextNodeIndex)
    {
        ClearDialogueChoices();
        SetChoicesContainerVisible(false);

        if (nextNodeIndex < 0)
        {
            CloseCaller("choice ended call");
            return;
        }

        if (ShouldCompleteMicrosoftCallInsteadOfLooping(null, nextNodeIndex))
        {
            CloseCaller("microsoft call completed");
            return;
        }

        ShowDialogueNode(nextNodeIndex);
    }

    void StartBadEndingSequence()
    {
        if (badEndingPlaying)
            return;

        if (nodeFlowRoutine != null)
        {
            StopCoroutine(nodeFlowRoutine);
            nodeFlowRoutine = null;
        }

        ClearDialogueChoices();
        SetChoicesContainerVisible(false);
        badEndingPlaying = true;

        AgentLog("H", "CallerScript.StartBadEndingSequence", "Bad ending started",
            "{\"storyCallId\":\"" + activeStoryCallId + "\",\"nodeIndex\":" + currentNodeIndex + "}");

        if (badEndingRoutine != null)
            StopCoroutine(badEndingRoutine);

        badEndingRoutine = StartCoroutine(BadEndingRoutine());
    }

    IEnumerator BadEndingRoutine()
    {
        if (shakeTarget == null)
            CacheShakeTarget();

        // The neighbor (Mr. Henderson) call is the "someone's at your door"
        // scenario. The screamer ending can be reached through several replies,
        // but the door knock should ONLY play on the "open the door" path — i.e.
        // when the node we came from is the one inviting Alex to open the door
        // ("...Open it wide..."). The "call the police" path must NOT knock.
        bool cameFromOpenDoor =
            previousNodeIndex >= 0 &&
            activeCallData != null && activeCallData.nodes != null &&
            previousNodeIndex < activeCallData.nodes.Length &&
            !string.IsNullOrEmpty(activeCallData.nodes[previousNodeIndex].speechText) &&
            activeCallData.nodes[previousNodeIndex].speechText.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0;

        if (activeStoryCallId == StoryCallId.Neighbor && doorKnockingClip != null && cameFromOpenDoor)
        {
            PlayDoorKnocking();
            float wait = doorKnockMaxWait > 0f
                ? Mathf.Min(doorKnockingClip.length, doorKnockMaxWait)
                : doorKnockingClip.length;
            yield return new WaitForSeconds(wait);

            // Bail out if the call was cancelled/closed during the knock.
            if (!badEndingPlaying)
                yield break;
        }

        ShowWrongChoiceText();
        StartCallShake();
        PlayHorrorSound();

        if (badEndingOverlay != null)
        {
            if (badEndingOverlayImage == null)
                badEndingOverlayImage = badEndingOverlay.GetComponent<Image>();

            badEndingOverlay.SetActive(true);

            Color baseColor = badEndingOverlayImage != null
                ? badEndingOverlayImage.color
                : new Color(0.75f, 0f, 0f, 0.45f);

            float peakAlpha = baseColor.a > 0.01f ? baseColor.a : 0.55f;
            float elapsed = 0f;
            float noiseSeed = UnityEngine.Random.Range(0f, 100f);

            while (elapsed < badEndingDuration)
            {
                elapsed += Time.deltaTime;

                float sine = (Mathf.Sin(elapsed * flashSpeed) + 1f) * 0.5f;
                float noise = Mathf.PerlinNoise(elapsed * flashSpeed * 0.35f, noiseSeed);
                float alpha = Mathf.Lerp(0.12f, peakAlpha, sine * 0.55f + noise * 0.45f);

                if (badEndingOverlayImage != null)
                {
                    Color c = baseColor;
                    c.a = alpha;
                    badEndingOverlayImage.color = c;
                }

                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(badEndingDuration);
        }

        yield return FadeOutAndStopHorrorSound();
        StopCallShake();
        HideWrongChoiceText();
        badEndingPlaying = false;
        badEndingRoutine = null;
        HideBadEndingOverlay();
        CloseCaller("bad ending");
    }

    void ShowWrongChoiceText()
    {
        if (wrongChoiceText == null)
            return;

        wrongChoiceText.text = "WRONG CHOICE";
        wrongChoiceText.fontStyle = FontStyles.Bold;
        wrongChoiceText.gameObject.SetActive(true);
    }

    void HideWrongChoiceText()
    {
        if (wrongChoiceText == null)
            return;

        wrongChoiceText.gameObject.SetActive(false);
    }

    void StartCallShake()
    {
        if (shakeTarget == null || shakeIntensity <= 0f)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(badEndingDuration, shakeIntensity));
    }

    void StopCallShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        if (shakeTarget != null)
            shakeTarget.anchoredPosition = shakeHomePosition;
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        if (shakeTarget == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            shakeTarget.anchoredPosition = shakeHomePosition + new Vector2(
                UnityEngine.Random.Range(-magnitude, magnitude),
                UnityEngine.Random.Range(-magnitude, magnitude));
            yield return null;
        }

        shakeTarget.anchoredPosition = shakeHomePosition;
        shakeRoutine = null;
    }

    void EnsureAudioSource()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void PlayDoorKnocking()
    {
        if (doorKnockingClip == null)
            return;

        EnsureAudioSource();
        audioSource.Stop();
        audioSource.loop = false;
        audioSource.volume = 1f;
        audioSource.PlayOneShot(doorKnockingClip, 1f);

        AgentLog("H", "CallerScript.PlayDoorKnocking", "Door knock before screamer",
            "{\"clipLength\":" + doorKnockingClip.length + "}");
    }

    void PlayHorrorSound()
    {
        if (horrorSound == null)
            return;

        EnsureAudioSource();
        audioSource.Stop();
        audioSource.loop = false;
        audioSource.clip = horrorSound;
        audioSource.volume = 1f;
        audioSource.Play();

        AgentLog("H", "CallerScript.PlayHorrorSound", "Horror sound started",
            "{\"clipLength\":" + horrorSound.length + "}");
    }

    IEnumerator FadeOutAndStopHorrorSound()
    {
        if (audioSource == null || !audioSource.isPlaying)
            yield break;

        if (horrorSoundFadeOut <= 0f)
        {
            StopHorrorSoundImmediate();
            yield break;
        }

        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < horrorSoundFadeOut)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / horrorSoundFadeOut);
            yield return null;
        }

        StopHorrorSoundImmediate();
    }

    void StopHorrorSoundImmediate()
    {
        if (audioSource == null)
            return;

        audioSource.Stop();
        audioSource.volume = 1f;
        audioSource.clip = null;
    }

    void HideBadEndingOverlay()
    {
        HideWrongChoiceText();

        if (badEndingOverlay == null)
            return;

        badEndingOverlay.SetActive(false);

        if (badEndingOverlayImage == null)
            badEndingOverlayImage = badEndingOverlay.GetComponent<Image>();

        if (badEndingOverlayImage != null)
        {
            Color c = badEndingOverlayImage.color;
            c.a = 0f;
            badEndingOverlayImage.color = c;
        }
    }

    void SetChoicesContainerVisible(bool visible)
    {
        if (choicesPanel != null)
            choicesPanel.SetActive(visible);
        else if (choicesContent != null)
            choicesContent.gameObject.SetActive(visible);
    }

    void ClearDialogueChoices()
    {
        Transform container = GetChoicesContainer();
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    public void CloseCaller()
    {
        if (Time.unscaledTime - callUiActivatedAt < 0.35f)
            return;

        CloseCaller("CloseCaller invoked");
    }

    void CloseCaller(string reason)
    {
        if (!callActive && !dialogueActive && !badEndingPlaying)
            return;

        StoryCallId endedStoryCall = activeStoryCallId;
        bool storyConversationFinished = IsStoryConversationFinished(reason);
        bool isBadEnding = reason == "bad ending";

        AgentLog("E", "CallerScript.CloseCaller", reason,
            "{\"callActive\":" + (callActive ? "true" : "false") + ",\"nodeIndex\":" + currentNodeIndex +
            ",\"storyCallId\":\"" + endedStoryCall + "\",\"storyConversationFinished\":" +
            (storyConversationFinished ? "true" : "false") + "}");

        StopLegacyOutgoingCallEffects();
        StopOutgoingNodeCallEffects();

        if (nodeFlowRoutine != null)
        {
            StopCoroutine(nodeFlowRoutine);
            nodeFlowRoutine = null;
        }

        if (badEndingRoutine != null)
        {
            StopCoroutine(badEndingRoutine);
            badEndingRoutine = null;
        }

        StopCallShake();
        StopHorrorSoundImmediate();
        badEndingPlaying = false;
        dialogueActive = false;
        activeCallData = null;
        activeStoryCallId = StoryCallId.None;
        currentNodeIndex = -1;
        ClearDialogueChoices();
        SetChoicesContainerVisible(false);
        HideBadEndingOverlay();
        RestoreDialogueTextDefaults();

        if (callerScreen != null)
            callerScreen.SetActive(false);

        callActive = false;
        StopOutgoingRinging();
        StopSubscriberUnavailableSoundIfPlaying();

        if (isBadEnding)
            OnCallBadEnding?.Invoke();

        if (storyConversationFinished)
            NotifyStoryCallCompleted(endedStoryCall);

        OnCallEnded?.Invoke();
    }

    static bool IsStoryConversationFinished(string reason)
    {
        return reason == "conversation ended"
            || reason == "choice ended call"
            || reason == "microsoft call completed";
    }

    void NotifyStoryCallCompleted(StoryCallId storyCallId)
    {
        if (storyCallId == StoryCallId.Microsoft)
        {
            AgentLog("F", "CallerScript.NotifyStoryCallCompleted", "Microsoft story completed", "{}");
            OnMicrosoftCallCompleted?.Invoke();
            ProgressDeepfakeMission();
            return;
        }

        if (storyCallId == StoryCallId.Mom)
        {
            OnMomCallCompleted?.Invoke();
            return;
        }

        if (storyCallId == StoryCallId.Neighbor)
            OnNeighborCallCompleted?.Invoke();
    }

    void ProgressDeepfakeMission()
    {
        if (missionManager == null)
            return;

        for (int i = 0; i < 3; i++)
        {
            string title = missionManager.GetMissionTitle(i);
            if (!string.IsNullOrEmpty(title) && title.IndexOf("Deepfake", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                missionManager.AddProgress(i);
                AgentLog("F", "CallerScript.ProgressDeepfakeMission", "Mission progress added",
                    "{\"missionIndex\":" + i + "}");
                return;
            }
        }
    }

    public void AnswerCall()
    {
        if (incomingCallManager == null)
            incomingCallManager = FindFirstObjectByType<IncomingCallManager>();

        if (incomingCallManager != null)
            incomingCallManager.AnswerIncoming();
        else
            Debug.LogError("[CallerScript] IncomingCallManager not found.");
    }

    public void DeclineCall()
    {
        if (IsOutgoingDialingPhase)
        {
            FinishLegacyOutgoingCall("declined");
            return;
        }

        if (outgoingNodeCallActive && !fatherChoiceActive)
        {
            FinishOutgoingNodeCall("declined");
            return;
        }

        if (fatherChoiceActive)
            return;

        if (outgoingUnavailablePhase)
            return;

        if (callActive && !dialogueActive)
        {
            FinishLegacyOutgoingCall("declined");
            return;
        }

        if (incomingCallManager != null)
            incomingCallManager.DeclineIncoming();
        else
            CloseCaller("declined");
    }
}

public class CallerController : CallerScript { }
