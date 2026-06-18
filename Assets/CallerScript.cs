using System;
using System.Collections;
using System.IO;
using UnityEngine;
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
    [Tooltip("Volume fade-out duration when the bad-ending sequence ends.")]
    public float horrorSoundFadeOut = 0.35f;

    [Header("Managers")]
    public IncomingCallManager incomingCallManager;
    public MissionSidebarManager missionManager;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip outgoingRingingClip;

    public Action OnCallEnded;
    public Action OnCallBadEnding;
    public Action OnNeighborCallCompleted;
    public Action OnMomCallCompleted;
    public Action OnMicrosoftCallCompleted;

    CallData activeCallData;
    StoryCallId activeStoryCallId = StoryCallId.None;
    int currentNodeIndex = -1;
    bool dialogueActive;
    bool callActive;
    bool badEndingPlaying;
    Coroutine nodeFlowRoutine;
    Coroutine badEndingRoutine;
    Coroutine shakeRoutine;
    float callUiActivatedAt;
    Image badEndingOverlayImage;
    RectTransform shakeTarget;
    Vector2 shakeHomePosition;

    public bool IsCallActive => callActive;
    public StoryCallId ActiveStoryCallId => activeStoryCallId;

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

    void PlayOutgoingRinging()
    {
        if (audioSource != null && outgoingRingingClip != null)
        {
            audioSource.clip = outgoingRingingClip;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    public void StopOutgoingRinging()
    {
        if (audioSource != null && audioSource.clip == outgoingRingingClip)
            audioSource.Stop();
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

    // Legacy outgoing calls (no story dialogue).
    public void OpenMomCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "MOM";
        callerAvatar.sprite = momAvatar;
        callActive = true;
        PlayOutgoingRinging();
    }

    public void OpenDadCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "DAD";
        callerAvatar.sprite = dadAvatar;
        callActive = true;
        PlayOutgoingRinging();
    }

    public void OpenSarahCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "SARAH";
        callerAvatar.sprite = sarahAvatar;
        callActive = true;
        PlayOutgoingRinging();
    }

    public void OpenBrotherCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "BROTHER";
        callerAvatar.sprite = brotherAvatar;
        callActive = true;
        PlayOutgoingRinging();
    }

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

        if (callerScreen != null)
            callerScreen.SetActive(false);

        callActive = false;
        StopOutgoingRinging();

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
        if (incomingCallManager != null)
            incomingCallManager.DeclineIncoming();
        else
            CloseCaller("declined");
    }
}

public class CallerController : CallerScript { }
