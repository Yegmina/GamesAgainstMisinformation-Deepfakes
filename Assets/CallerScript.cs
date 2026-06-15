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
    Microsoft
}

public class CallerScript : MonoBehaviour
{
    // UI элементы
    public TMP_Text callerName;
    public Image callerAvatar;
    public GameObject callerScreen;

    // Аватарки
    public Sprite momAvatar;
    public Sprite dadAvatar;
    public Sprite sarahAvatar;
    public Sprite brotherAvatar;
    public Sprite unknownAvatar;
    public Sprite neighborAvatar;
    public Sprite microsoftAvatar;

    [Header("Story Calls")]
    public CallData neighborCallData;
    public CallData microsoftCallData;

    [Header("Call Dialogue")]
    public TMP_Text dialogueText;
    public GameObject choicesPanel;
    public Transform choicesContent;
    public GameObject choiceButtonPrefab;
    public float autoAdvanceDelay = 1.5f;

    [Header("Managers")]
    public IncomingCallManager incomingCallManager;
    public MissionSidebarManager missionManager;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip outgoingRingingClip;

    public Action OnCallEnded;
    public Action OnMicrosoftCallCompleted;
    public Action OnNeighborCallCompleted;

    CallData activeCallData;
    StoryCallId activeStoryCallId = StoryCallId.None;
    int currentNodeIndex = -1;
    bool dialogueActive;
    bool callActive;
    Coroutine nodeFlowRoutine;
    float callUiActivatedAt;

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

    private void Awake()
    {
        if (incomingCallManager == null)
            incomingCallManager = FindFirstObjectByType<IncomingCallManager>();
        if (missionManager == null)
            missionManager = FindFirstObjectByType<MissionSidebarManager>();
    }

    private void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    /// <summary>
    /// Hides PhoneIncomingCall and shows PhoneCallScreen without touching the parent canvas.
    /// </summary>
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

    private void PlayOutgoingRinging()
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

    // ===== ОТКРЫТЬ ЗВОНКИ =====

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
        // Do not play outgoing ring here, as this is used for incoming calls too.
        return true;
    }

    public bool OpenNeighborCall()
    {
        return OpenStoryCall(neighborCallData, neighborAvatar, "neighborCallData", StoryCallId.Neighbor);
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
        // Keep subtitle on screen for the full voice clip length.
        if (voiceDuration > 0f)
        {
            yield return new WaitForSeconds(voiceDuration);
        }
        else if (!hasChoices && autoAdvanceDelay > 0f)
        {
            // Auto-advance delay applies only to linear nodes without choices.
            yield return new WaitForSeconds(autoAdvanceDelay);
        }

        if (!dialogueActive || !callActive || currentNodeIndex != nodeIndex)
            yield break;

        if (hasChoices)
        {
            SpawnChoiceButtons(node, nodeIndex);
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

    // ===== ДЕЙСТВИЯ СО ЗВОНКОМ =====

    public void CloseCaller()
    {
        if (Time.unscaledTime - callUiActivatedAt < 0.35f)
            return;

        CloseCaller("CloseCaller invoked");
    }

    void CloseCaller(string reason)
    {
        if (!callActive && !dialogueActive)
            return;

        StoryCallId endedStoryCall = activeStoryCallId;
        bool storyConversationFinished = IsStoryConversationFinished(reason);

        AgentLog("E", "CallerScript.CloseCaller", reason,
            "{\"callActive\":" + (callActive ? "true" : "false") + ",\"nodeIndex\":" + currentNodeIndex +
            ",\"storyCallId\":\"" + endedStoryCall + "\",\"storyConversationFinished\":" +
            (storyConversationFinished ? "true" : "false") + "}");

        if (nodeFlowRoutine != null)
        {
            StopCoroutine(nodeFlowRoutine);
            nodeFlowRoutine = null;
        }

        dialogueActive = false;
        activeCallData = null;
        activeStoryCallId = StoryCallId.None;
        currentNodeIndex = -1;
        ClearDialogueChoices();
        SetChoicesContainerVisible(false);

        if (callerScreen != null)
            callerScreen.SetActive(false);

        callActive = false;
        StopOutgoingRinging();

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

    /// <summary>
    /// Optional Inspector wrapper. Forwards to IncomingCallManager.AnswerIncoming().
    /// </summary>
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

// Keeps existing scene references that still point at CallerController.
public class CallerController : CallerScript { }
