using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ChatController : MonoBehaviour
{
    [Header("Screens")]
public GameObject chatScreen;
    public GameObject hubScreen;

    [Header("Chat UI")]
    public Transform messagesContent;
    public GameObject messagePrefabMy;
    public GameObject messagePrefabOther;
    public GameObject optionsPanel;
    public Transform optionsContent;
    public GameObject optionButtonPrefab;
    public TMP_Text contactNameText;
    public Image contactAvatar;
    public ScrollRect chatScrollRect;

    [Header("Avatars")]
    public Sprite momAvatar;
    public Sprite brotherAvatar;
    public Sprite unknownAvatar;
    public Sprite providerAvatar;
    public Sprite sarahAvatar;

    [Header("Contact Rows (hub)")]
    public TMP_Text momPreview;
    public GameObject momBadge;
    public TMP_Text broPreview;
    public GameObject broBadge;
    public TMP_Text unknownPreview;
    public GameObject unknownBadge;
    public TMP_Text providerPreview;
    public GameObject providerBadge;
    public TMP_Text sarahPreview;
    public GameObject sarahBadge;

    [Header("Photo")]
    public Sprite photoSprite;
    public Sprite screamerPhotoSprite;

    [Header("Sarah Video")]
    public UnityEngine.Video.VideoClip sarahVideoClip;

    [Header("Voice Note")]
    public Sprite voiceNoteSprite;   
    public AudioClip voiceNoteClip;  
    public AudioClip screamerClip;
    public AudioClip momBadEndingClip;
    public AudioClip virusSoundClip;

    [Header("Bubble Sprites")]
    public Sprite bubbleMeSprite;
    public Sprite bubbleThemSprite;

    static readonly Color themBubble = new Color(0.118f, 0.118f, 0.180f, 1f);
    static readonly Color meBubble   = new Color(0.102f, 0.227f, 0.431f, 1f);
    static readonly Color themText   = new Color(0.816f, 0.816f, 0.910f, 1f);
    static readonly Color meText     = new Color(0.784f, 0.863f, 1.000f, 1f);
    const float maxBubbleFrac = 0.78f;   
    RectTransform messagesRT;
    Sprite roundedBubbleSprite;
    Sprite circleSprite;

    private Dictionary<string, Transform> chatContainers = new Dictionary<string, Transform>();
    private Transform templateMessagesContent;

    Canvas canvas;
TMP_FontAsset font;
    TMP_Text timerText, paranoiaText, stateText;
    Image paranoiaFill;
    Image flashOverlay;
    GameObject screamerOverlay;
    GameObject photoViewerOverlay;
    Image photoViewerImage;
    GameObject videoViewerOverlay;
    RawImage videoViewerDisplay;
    UnityEngine.Video.VideoPlayer videoPlayer;
    GameObject videoPlayerHost;
    bool videoPreparing;
    Coroutine preloadCoroutine;
    Coroutine openCoroutine;
    TMP_Text videoLoadingText;
    GameObject videoLoadingContainer;
    RectTransform videoProgressBarFill;
    Coroutine loadingBarCoroutine;
    RenderTexture videoRT;
    AspectRatioFitter videoAspectFitter;
    AspectRatioFitter photoAspectFitter;
    Coroutine videoWatchdog;
    int videoPrepareAttempts;
    GameObject playIconGO;
    Sprite triangleSprite;
    RectTransform shakeTarget;
    Vector2 shakeHome;

    AudioSource audioSrc;
    AudioClip chimeClip;

    // Brother's first voice note (controllable play/pause + running timer)
    AudioSource voiceSrc;
    Sprite pauseSprite;
    Image broVoiceIcon;
    RectTransform broVoiceFill;
    TMP_Text broVoiceTimer;
    bool broVoicePlaying;
    float broVoiceElapsed;
    float broVoiceDuration;
    Coroutine broVoiceRoutine;

    private bool ignoreInternalSetParanoia = false;

    int paranoia
    {
        get => GlobalCanvasPersistent.Instance != null ? GlobalCanvasPersistent.Instance.Paranoia : 0;
        set { if (GlobalCanvasPersistent.Instance != null) GlobalCanvasPersistent.Instance.SetParanoia(value); }
    }
    float timer
    {
        get => GlobalCanvasPersistent.Instance != null ? GlobalCanvasPersistent.Instance.Timer : 600f;
        set { if (GlobalCanvasPersistent.Instance != null) GlobalCanvasPersistent.Instance.Timer = value; }
    }
    bool timerRunning
    {
        get => GlobalCanvasPersistent.Instance != null ? GlobalCanvasPersistent.Instance.IsTimerRunning : true;
        set { if (GlobalCanvasPersistent.Instance != null) GlobalCanvasPersistent.Instance.SetTimerRunning(value); }
    }

    // Static state backing fields to persist across phone exits (scene reloads)
    private static bool s_momFinished = false;
    private static bool s_broFinished = false;
    private static bool s_broWarned = false;
    private static bool s_momStarted = false;
    private static bool s_broStarted = false;
    private static bool s_broSecondVoiceNoteTriggered = false;
    private static bool s_isBroSecondVoice = false;
    private static bool s_unknownRead = false;
    private static bool s_providerFinished = false;
    private static bool s_providerLinkClicked = false;
    private static bool s_sarahFinished = false;
    private static bool s_sarahStarted = false;
    private static bool s_sarahBadPath = false;

    // Static structures to persist the message history of each chat
    public class SavedMessage
    {
        public enum MessageType
        {
            Normal,
            System,
            Spam,
            Link,
            Photo,
            Voice,
            Video
        }
        public MessageType type;
        public bool isMe;
        public string text;
        public bool isError;
        public bool isDanger;
        public string videoName;
        public string videoDuration;
    }

    private static Dictionary<string, List<SavedMessage>> SavedChatHistories = new Dictionary<string, List<SavedMessage>>();
    private static Dictionary<string, string> s_chatStates = new Dictionary<string, string>();
    private bool isReconstructing = false;

    bool ended = false;
    bool locked = false;
    string currentChat = null;
    AudioClip currentVoiceClip;

    bool momFinished { get => s_momFinished; set => s_momFinished = value; }
    bool broFinished { get => s_broFinished; set => s_broFinished = value; }
    bool broWarned { get => s_broWarned; set => s_broWarned = value; }
    bool momStarted { get => s_momStarted; set => s_momStarted = value; }
    bool broStarted { get => s_broStarted; set => s_broStarted = value; }
    bool broSecondVoiceNoteTriggered { get => s_broSecondVoiceNoteTriggered; set => s_broSecondVoiceNoteTriggered = value; }
    bool isBroSecondVoice { get => s_isBroSecondVoice; set => s_isBroSecondVoice = value; }
    bool unknownRead { get => s_unknownRead; set => s_unknownRead = value; }
    bool providerFinished { get => s_providerFinished; set => s_providerFinished = value; }
    bool providerLinkClicked { get => s_providerLinkClicked; set => s_providerLinkClicked = value; }
    bool sarahFinished { get => s_sarahFinished; set => s_sarahFinished = value; }
    bool sarahStarted { get => s_sarahStarted; set => s_sarahStarted = value; }
    bool sarahBadPath { get => s_sarahBadPath; set => s_sarahBadPath = value; }

    void SaveMessageToHistory(string chatId, SavedMessage msg)
    {
        if (isReconstructing) return;
        if (string.IsNullOrEmpty(chatId)) chatId = currentChat;
        if (string.IsNullOrEmpty(chatId)) return;
        
        if (!SavedChatHistories.ContainsKey(chatId))
        {
            SavedChatHistories[chatId] = new List<SavedMessage>();
        }
        SavedChatHistories[chatId].Add(msg);
    }

    bool HasMessageInHistory(string chatId, string partialText)
    {
        if (!SavedChatHistories.ContainsKey(chatId)) return false;
        foreach (var msg in SavedChatHistories[chatId])
        {
            if (msg.text != null && msg.text.Contains(partialText))
                return true;
        }
        return false;
    }

    bool HasPhotoInHistory(string chatId)
    {
        if (!SavedChatHistories.ContainsKey(chatId)) return false;
        foreach (var msg in SavedChatHistories[chatId])
        {
            if (msg.type == SavedMessage.MessageType.Photo)
                return true;
        }
        return false;
    }

    bool HasVoiceInHistory(string chatId)
    {
        if (!SavedChatHistories.ContainsKey(chatId)) return false;
        foreach (var msg in SavedChatHistories[chatId])
        {
            if (msg.type == SavedMessage.MessageType.Voice)
                return true;
        }
        return false;
    }

    bool HasVideoInHistory(string chatId)
    {
        if (!SavedChatHistories.ContainsKey(chatId)) return false;
        foreach (var msg in SavedChatHistories[chatId])
        {
            if (msg.type == SavedMessage.MessageType.Video)
                return true;
        }
        return false;
    }

    void SetChatState(string chatId, string state)
    {
        s_chatStates[chatId] = state;
    }

    public static void ResetStaticState()
    {
        s_momFinished = false;
        s_broFinished = false;
        s_broWarned = false;
        s_momStarted = false;
        s_broStarted = false;
        s_broSecondVoiceNoteTriggered = false;
        s_isBroSecondVoice = false;
        s_unknownRead = false;
        s_providerFinished = false;
        s_providerLinkClicked = false;
        s_sarahFinished = false;
        s_sarahStarted = false;
        s_sarahBadPath = false;

        SavedChatHistories.Clear();
        s_chatStates.Clear();
    }

    void ReconstructChat(string chatId)
    {
        if (!SavedChatHistories.ContainsKey(chatId)) return;
        
        isReconstructing = true;
        foreach (var msg in SavedChatHistories[chatId])
        {
            switch (msg.type)
            {
                case SavedMessage.MessageType.Normal:
                    AddMessage(msg.isMe, msg.text, msg.isError, chatId);
                    break;
                case SavedMessage.MessageType.System:
                    AddSystem(msg.text, chatId);
                    break;
                case SavedMessage.MessageType.Spam:
                    AddSpam(msg.text, chatId);
                    break;
                case SavedMessage.MessageType.Link:
                    AddLinkMessage(chatId);
                    break;
                case SavedMessage.MessageType.Photo:
                    AddPhoto(chatId);
                    break;
                case SavedMessage.MessageType.Voice:
                    AudioClip clip = msg.isDanger ? screamerClip : voiceNoteClip;
                    AddVoice(msg.isMe, clip, msg.isDanger, chatId);
                    break;
                case SavedMessage.MessageType.Video:
                    AddVideoMessage(msg.isMe, msg.videoName, msg.videoDuration, chatId);
                    break;
            }
        }
        isReconstructing = false;
    }

    void RestoreChatChoices(string chatId)
    {
        if (!s_chatStates.ContainsKey(chatId)) return;
        string state = s_chatStates[chatId];
        
        switch (state)
        {
            // --- Mom ---
            case "Mom_Intro":
                ShowChoices(
                    ("\"Yeah, where else would I be at 4 AM?\"", 0, MomChoice1A),
                    ("\"I'm home. Did something happen?\"", 2, MomChoice1B)
                );
                break;
            case "Mom_Request":
                ShowChoices(
                    ("\"Sure, hold on.\"", 1, MomChoice2A),
                    ("\"Send me a photo first, I'm freaked out.\"", 2, MomChoice2B)
                );
                break;
            case "Mom_Pressure":
                ShowChoices(
                    ("[ SEND ADDRESS ]", 1, () => StartCoroutine(MomPunishmentRoutine())),
                    ("[ ASK FOR A PHOTO FIRST ]", 0, MomAskPhoto)
                );
                break;
            case "Mom_SendPhoto":
                ShowChoices(
                    ("[ TRUST & SEND ADDRESS ]", 1, () => StartCoroutine(MomPunishmentRoutine())),
                    ("[ BLOCK CONTACT ]", 2, TriggerMomBlock)
                );
                break;
                
            // --- Brother ---
            case "Bro_Intro":
                ShowChoices(
                    ("\"Sure, sending it now.\"", 1, BroChoice1A),
                    ("\"Send me another voice note just to be sure.\"", 0, BroChoice1B)
                );
                break;
            case "Bro_Angry":
                ShowChoices(
                    ("\"Okay, okay, sorry. Sending it now.\"", 1, () => StartCoroutine(TriggerTransactionFail())),
                    ("\"Come on, just one more. Then I'll send it.\"", 0, BroChoiceDangerPath)
                );
                break;
                
            // --- Sarah ---
            case "Sarah_Intro":
                ShowChoices(
                    ("\"What's wrong? Are you okay?\"", 0, SarahChoiceConcern),
                    ("\"It's 4 AM. Can this wait until morning?\"", 1, SarahChoiceDismissive)
                );
                break;
            case "Sarah_RevealVideo":
                ShowChoices(
                    ("\"Sarah, that's a deepfake. Don't panic.\"", 0, SarahGoodPathStart),
                    ("\"Are you sure it's not you? Maybe you forgot?\"", 1, SarahBadPathStart)
                );
                break;
            case "Sarah_GoodPathContinue":
                ShowChoices(
                    ("\"Block him immediately. Don't respond.\"", 0, SarahAdviceBlock),
                    ("\"Save the video as evidence first. Then block him.\"", 0, SarahAdviceEvidence)
                );
                break;
            case "Sarah_GoodPathComfort":
                ShowChoices(
                    ("\"Some people are just evil. Stay strong.\"", 0, SarahComfortStrong),
                    ("\"I'm here for you. You're not alone.\"", 0, SarahComfortHere)
                );
                break;
            case "Sarah_BadPathContinue":
                ShowChoices(
                    ("\"Wait, I'm sorry. I believe you. What can I do?\"", 0, SarahRecovery),
                    ("\"I'm just worried about you. The video just looks so real.\"", 1, SarahBadEnding)
                );
                break;
                
            default:
                ClearChoices();
                break;
        }
    }

    void Start()
    {
        if (chatScreen != null)
        {
            canvas = chatScreen.GetComponentInParent<Canvas>(true);
        }
        
        if (canvas == null || canvas.gameObject.name == "GlobalCanvas")
        {
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.gameObject.name != "GlobalCanvas")
                {
                    canvas = c;
                    break;
                }
            }
            
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        }

        if (contactNameText != null) font = contactNameText.font;

        SetupAudio();
        BuildHud();
        BuildOverlays();
        if (sarahVideoClip != null)
        {
            PreloadVideo(sarahVideoClip);
        }
        
        templateMessagesContent = messagesContent;
        if (templateMessagesContent != null)
        {
            templateMessagesContent.gameObject.SetActive(false);
            ConfigureChatLayout(templateMessagesContent);
        }

        if (momStarted)
        {
            if (momBadge != null) momBadge.SetActive(false);
            if (momPreview != null)
            {
                if (momFinished)
                {
                    bool blocked = HasMessageInHistory("mom", "Contact BLOCKED");
                    momPreview.text = blocked ? "[Blocked]" : "address received.";
                }
                else
                {
                    momPreview.text = "In progress...";
                }
            }
        }
        else
        {
            if (momPreview != null) momPreview.text = "Are you home?";
        }

        if (broStarted)
        {
            if (broBadge != null) broBadge.SetActive(false);
            if (broPreview != null)
            {
                if (broFinished)
                {
                    bool failed = HasMessageInHistory("bro", "TRANSACTION FAILED");
                    broPreview.text = failed ? "[Compromised]" : "DO YOU BELIEVE ME NOW?";
                }
                else
                {
                    broPreview.text = "In progress...";
                }
            }
        }
        else
        {
            if (broPreview != null) broPreview.text = "Left my gym bag";
        }

        if (sarahStarted)
        {
            if (sarahBadge != null) sarahBadge.SetActive(false);
            if (sarahPreview != null)
            {
                if (sarahFinished)
                {
                    sarahPreview.text = sarahBadPath ? "[Ignored you]" : "Thank you Alex ❤️";
                }
                else
                {
                    sarahPreview.text = "In progress...";
                }
            }
        }
        else
        {
            if (sarahPreview != null) sarahPreview.text = "Hey, you there?";
        }

        if (unknownRead)
        {
            if (unknownBadge != null) unknownBadge.SetActive(false);
            if (unknownPreview != null) unknownPreview.text = "[Read]";
        }
        else
        {
            if (unknownPreview != null) unknownPreview.text = "Unknown number";
        }

        if (providerFinished)
        {
            if (providerBadge != null) providerBadge.SetActive(false);
            if (providerPreview != null)
            {
                bool blocked = HasMessageInHistory("provider", "blocked the contact");
                providerPreview.text = blocked ? "[Blocked]" : "[COMPROMISED]";
            }
        }
        else
        {
            if (providerPreview != null) providerPreview.text = "⚠ Your connection is unstable...";
        }

        SetParanoia(paranoia);
        SetAppState("ACTIVE");
        UpdateTimerLabel();

        FixLayout();
    }

    void FixLayout()
    {
        if (chatScrollRect == null || optionsPanel == null) return;
        RectTransform scrollRT = chatScrollRect.GetComponent<RectTransform>();
        RectTransform optionsRT = optionsPanel.GetComponent<RectTransform>();

        // Calculate the top Y position of the options panel in the chatScreen space.
        // The log showed it is anchored to bottom (0.5, 0).
        // offsetMax.y is the top edge relative to the bottom anchor.
        float optionsTop = optionsRT.offsetMax.y;

        // Adjust the ScrollRect to fill the space exactly down to the options panel.
        // The scroll view is currently anchored to the center (0.5, 0.5).
        // We'll change it to stretch horizontally and anchor bottom/top correctly.
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);

        // HUD height is 34, plus some margin for the 'Mom' title area (approx 150 total).
        // We'll keep the current top position but fix the bottom.
        // Current top was at 649 out of 800. Offset from top is 800 - 649 = 151.
        scrollRT.offsetMin = new Vector2(10f, optionsTop);
        scrollRT.offsetMax = new Vector2(-10f, -151f);

        // Ensure the viewport also fills the space
        if (chatScrollRect.viewport != null)
        {
            chatScrollRect.viewport.anchorMin = Vector2.zero;
            chatScrollRect.viewport.anchorMax = Vector2.one;
            chatScrollRect.viewport.offsetMin = Vector2.zero;
            chatScrollRect.viewport.offsetMax = Vector2.zero;
        }
    }

    void Update()
    {
    }

    public void OpenMomChat()
    {
        bool firstTime = !chatContainers.ContainsKey("mom");
        SwitchToChat("mom");

        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (momBadge != null) momBadge.SetActive(false);
        if (contactNameText != null) 
        {
            contactNameText.text = "Mom";
            contactNameText.fontSize = 20;
        }
        if (contactAvatar != null && momAvatar != null) contactAvatar.sprite = momAvatar;

        if (firstTime)
        {
            ClearChoices();
            
            if (SavedChatHistories.ContainsKey("mom"))
            {
                ReconstructChat("mom");
                RestoreChatChoices("mom");
            }
            else
            {
                SavedChatHistories["mom"] = new List<SavedMessage>();
                s_chatStates["mom"] = "Mom_Intro";

                AddMessage(false, "Alex, defrost the pizza if you want, it's in the freezer. We left. 🍕", false, "mom");

                if (momFinished)
                {
                    AddSystem("This conversation has ended. Connection locked.", "mom");
                    return;
                }

                if (optionsPanel != null) optionsPanel.SetActive(true);

                if (!momStarted)
                {
                    momStarted = true;
                    StartCoroutine(MomIntro());
                }
            }
        }
        else
        {
            if (optionsPanel != null) optionsPanel.SetActive(true);
        }
        
        ScrollToBottom();
    }

    public void OpenBrotherChat()
    {
        bool firstTime = !chatContainers.ContainsKey("bro");
        SwitchToChat("bro");

        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (broBadge != null) broBadge.SetActive(false);
        if (contactNameText != null) 
        {
            contactNameText.text = "Brother";
            contactNameText.fontSize = 20;
        }
        if (contactAvatar != null && brotherAvatar != null) contactAvatar.sprite = brotherAvatar;

        if (firstTime)
        {
            ClearChoices();
            
            if (SavedChatHistories.ContainsKey("bro"))
            {
                ReconstructChat("bro");
                RestoreChatChoices("bro");
            }
            else
            {
                SavedChatHistories["bro"] = new List<SavedMessage>();
                s_chatStates["bro"] = "Bro_Intro";

                AddMessage(false, "Left my gym bag at your place. Don't touch my protein bar, bro", false, "bro");

                if (broFinished)
                {
                    AddSystem("This conversation has ended. Connection locked.", "bro");
                    return;
                }

                if (optionsPanel != null) optionsPanel.SetActive(true);

                if (!broStarted)
                {
                    broStarted = true;
                    StartCoroutine(BroIntro());
                }
            }
        }
        else
        {
            if (optionsPanel != null) optionsPanel.SetActive(true);
        }
        
        ScrollToBottom();
    }

    public void OpenUnknownChat()
    {
        bool firstTime = !chatContainers.ContainsKey("unknown");
        SwitchToChat("unknown");

        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (unknownBadge != null) unknownBadge.SetActive(false);
        if (unknownRead == false) unknownRead = true;
        if (contactNameText != null)
        {
            contactNameText.text = "???";
            contactNameText.fontSize = 20;
        }
        if (contactAvatar != null && unknownAvatar != null) contactAvatar.sprite = unknownAvatar;

        if (firstTime)
        {
            ClearChoices();
            
            if (SavedChatHistories.ContainsKey("unknown"))
            {
                ReconstructChat("unknown");
            }
            else
            {
                SavedChatHistories["unknown"] = new List<SavedMessage>();

                AddMessage(false, "???: Alex...", false, "unknown");
                AddMessage(false, "???: I see you.", false, "unknown");
                AddMessage(false, "???: You don't know me. But I know you.", false, "unknown");
                AddMessage(false, "???: I've been watching.", false, "unknown");
                AddMessage(false, "???: Don't trust anyone. Especially not your family.", false, "unknown");
                AddMessage(false, "???: They are not who you think.", false, "unknown");
                AddMessage(false, "???: The video... it's real.", false, "unknown");
                AddMessage(false, "???: I'll find you.", false, "unknown");
                AddMessage(false, "???: Tick tock.", false, "unknown");
                AddMessage(false, "???: This conversation will self-destruct.", false, "unknown");
                
                AddSystem("⚠ This number is no longer in service.", "unknown");
            }
        }
        
        if (unknownPreview != null) unknownPreview.text = "[Read]";
        ScrollToBottom();
    }

    public void OpenProviderChat()
    {
        bool firstTime = !chatContainers.ContainsKey("provider");
        SwitchToChat("provider");

        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (providerBadge != null) providerBadge.SetActive(false);
        if (contactNameText != null)
        {
            contactNameText.text = "Internet Provider";
            contactNameText.fontSize = 18;
        }
        if (contactAvatar != null && providerAvatar != null) contactAvatar.sprite = providerAvatar;

        if (firstTime)
        {
            ClearChoices();
            
            if (SavedChatHistories.ContainsKey("provider"))
            {
                ReconstructChat("provider");
            }
            else
            {
                SavedChatHistories["provider"] = new List<SavedMessage>();

                AddMessage(false, "📡 Internet Provider: Important notice!", false, "provider");
                AddMessage(false, "📡 Your connection has been unstable for 3 days.", false, "provider");
                AddMessage(false, "📡 Click the link below to verify your IP address:", false, "provider");
                
                AddLinkMessage("provider");
                
                AddMessage(false, "📡 If not verified within 24h, your service will be suspended.", false, "provider");
            }
        }

        ScrollToBottom();
    }

    public void OpenSarahChat()
    {
        bool firstTime = !chatContainers.ContainsKey("sarah");
        SwitchToChat("sarah");

        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (sarahBadge != null) sarahBadge.SetActive(false);
        if (contactNameText != null) 
        {
            contactNameText.text = "Sarah";
            contactNameText.fontSize = 20;
        }
        if (contactAvatar != null && sarahAvatar != null) contactAvatar.sprite = sarahAvatar;

        if (firstTime)
        {
            ClearChoices();
            
            if (SavedChatHistories.ContainsKey("sarah"))
            {
                ReconstructChat("sarah");
                RestoreChatChoices("sarah");
            }
            else
            {
                SavedChatHistories["sarah"] = new List<SavedMessage>();
                s_chatStates["sarah"] = "Sarah_Intro";

                AddMessage(false, "Hey, you there? 💬", false, "sarah");

                if (sarahFinished)
                {
                    if (sarahBadPath)
                        AddSystem("Sarah stopped responding. You messed up.", "sarah");
                    else
                        AddSystem("Sarah is okay now. You're a good friend.", "sarah");
                    return;
                }

                if (optionsPanel != null) optionsPanel.SetActive(true);

                if (!sarahStarted)
                {
                    sarahStarted = true;
                    StartCoroutine(SarahIntro());
                }
            }
        }
        else
        {
            if (optionsPanel != null) optionsPanel.SetActive(true);
        }
        
        ScrollToBottom();
    }

    void AddLinkMessage(string targetChatId = null)
    {
        Transform target = GetTargetContainer(targetChatId);
        var row = BuildRow(false, target);
        if (row == null) return;
        
        GameObject linkObj = new GameObject("LinkMessage", typeof(RectTransform), typeof(Image), typeof(Button));
linkObj.transform.SetParent(row, false);
        var img = linkObj.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.25f, 1f);
        img.sprite = bubbleThemSprite != null ? bubbleThemSprite : roundedBubbleSprite;
        img.type = Image.Type.Sliced;

        var btn = linkObj.GetComponent<Button>();
        var le = linkObj.AddComponent<LayoutElement>();
        le.preferredWidth = 280f;
        le.preferredHeight = 50f;
        
        var textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(linkObj.transform, false);
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        
        if (providerLinkClicked)
        {
            tmp.text = "🔗 [LINK EXPIRED]";
            tmp.color = Color.gray;
            btn.interactable = false;
        }
        else
        {
            tmp.text = "🔗 https://verify-ip.provider-secure.net/confirm";
            tmp.color = new Color(0.3f, 0.7f, 1f);
        }
        
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        if (!providerLinkClicked)
        {
            btn.onClick.AddListener(() => {
                if (!providerFinished)
                {
                    providerLinkClicked = true;
                    TriggerVirusAttack();
                    btn.interactable = false;
                    tmp.text = "🔗 [LINK EXPIRED]";
                    tmp.color = Color.gray;
                }
            });
        }
        
        PlayChime();
        ScrollToBottom();
    }

    void TriggerVirusAttack()
    {
        if (providerFinished) return;
        providerFinished = true;
        ClearChoices();
        
        StartCoroutine(VirusRoutine());
    }

    IEnumerator VirusRoutine()
    {
        PlayVirusSound();
        
        yield return StartCoroutine(ShowQuickScreamerPhoto());
        
        AddMessage(false, "⚠ MALICIOUS LINK DETECTED", true, "provider");
        AddMessage(false, "⚠ Downloading: virus_core.exe", true, "provider");
        
        SetParanoia(paranoia + 10);
        SubtractTime(30);
        StartCoroutine(ShakeRoutine(5f, 20f));
        
        yield return Wait(0.5f);
        AddSpam("DOWNLOADING... 25%", "provider");
        FlashRed();
        
        yield return Wait(0.5f);
        AddSpam("DOWNLOADING... 50%", "provider");
        FlashRed();
        
        yield return Wait(0.5f);
        AddSpam("DOWNLOADING... 75%", "provider");
        FlashRed();
        
        yield return Wait(0.5f);
        AddSpam("DOWNLOAD COMPLETE", "provider");
        AddSpam("YOUR DEVICE IS COMPROMISED", "provider");
        AddSpam("ALL DATA ENCRYPTED", "provider");
        
        yield return Wait(2.5f);
        
        AddSpam("⚠ RANSOMWARE ACTIVATED", "provider");
        AddSpam("Contact: darkweb@onion.net", "provider");
        
        yield return Wait(2f);
        
        if (providerPreview != null) providerPreview.text = "[COMPROMISED]";
        
        yield return Wait(1f);
        CloseChat();
    }

    IEnumerator ShowQuickScreamerPhoto()
    {
        if (screamerOverlay == null) yield break;
        
        var img = screamerOverlay.GetComponent<Image>();
        Sprite originalSprite = null;
        Color originalColor = img.color;
        
        if (img != null && screamerPhotoSprite != null)
        {
            originalSprite = img.sprite;
            originalColor = img.color;
            
            img.sprite = screamerPhotoSprite;
            img.color = Color.white;
            img.type = Image.Type.Simple;
            
            if (chatScreen != null)
            {
                screamerOverlay.transform.SetParent(chatScreen.transform);
                var rect = screamerOverlay.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.sizeDelta = Vector2.zero;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }
        }
        
        screamerOverlay.SetActive(true);
        
        StartCoroutine(ShakeRoutine(2f, 25f));
        
        yield return Wait(2.0f);
        
        screamerOverlay.SetActive(false);
        
        if (img != null && originalSprite != null)
        {
            img.sprite = originalSprite;
            img.color = originalColor;
        }
    }

    void PlayVirusSound()
    {
        if (audioSrc == null || virusSoundClip == null) return;
        audioSrc.PlayOneShot(virusSoundClip, 1f);
    }

    void SafeCloseProvider()
    {
        ClearChoices();
        providerFinished = true;
        AddSystem("You blocked the contact. Your device is safe.", "provider");
        if (providerPreview != null) providerPreview.text = "[Blocked]";

        if (MissionSidebarManager.Instance != null)
        {
            MissionSidebarManager.Instance.AddProgress(2);
        }

        StartCoroutine(SafeCloseProviderRoutine());
    }

    IEnumerator SafeCloseProviderRoutine()
    {
        yield return Wait(1.5f);
        CloseChat();
    }

    public void CloseChat()
    {
        if (currentChat == null) return;

        bool finished = true;
        if (currentChat == "mom") finished = momFinished;
        else if (currentChat == "bro") finished = broFinished;
        else if (currentChat == "sarah") finished = sarahFinished;
        else if (currentChat == "provider") finished = providerFinished;

        if (!finished && currentChat != "unknown" && currentChat != "provider")
        {
            AddSystem("You can't leave now. The conversation isn't over.", currentChat);
            return;
        }

        if (chatContainers.ContainsKey(currentChat))
        {
            chatContainers[currentChat].gameObject.SetActive(false);
        }

        chatScreen.SetActive(false);
        if (hubScreen != null) hubScreen.SetActive(true);
        currentChat = null;
    }

    public void ResetPrototype()
    {
        StopAllCoroutines();
        if (voiceSrc != null) voiceSrc.Stop();
        broVoicePlaying = false; broVoiceElapsed = 0f; broVoiceRoutine = null;
        broVoiceIcon = null; broVoiceFill = null; broVoiceTimer = null;
        paranoia = 0; timer = 600f; timerRunning = true; ended = false; locked = false;
        momFinished = false; broFinished = false; broWarned = false; currentChat = null;
        momStarted = false; broStarted = false; broSecondVoiceNoteTriggered = false;
        unknownRead = false; providerFinished = false; providerLinkClicked = false;
        sarahFinished = false; sarahStarted = false; sarahBadPath = false;
        if (screamerOverlay != null) screamerOverlay.SetActive(false);
        if (flashOverlay != null) { var c = flashOverlay.color; c.a = 0f; flashOverlay.color = c; }
        if (shakeTarget != null) shakeTarget.anchoredPosition = shakeHome;
        SetParanoia(0);
        SetAppState("ACTIVE");
        UpdateTimerLabel();
        if (momPreview != null) momPreview.text = "Are you home?";
        if (broPreview != null) broPreview.text = "Left my gym bag";
        if (unknownPreview != null) unknownPreview.text = "Unknown number";
        if (providerPreview != null) providerPreview.text = "⚠ Your connection is unstable...";
        if (sarahPreview != null) sarahPreview.text = "Hey, you there?";
        if (momBadge != null) momBadge.SetActive(true);
        if (broBadge != null) broBadge.SetActive(true);
        if (unknownBadge != null) unknownBadge.SetActive(true);
        if (providerBadge != null) providerBadge.SetActive(true);
        if (sarahBadge != null) sarahBadge.SetActive(true);
        
        foreach (var kvp in chatContainers)
        {
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        }
        chatContainers.Clear();
        currentChat = null;
        messagesContent = templateMessagesContent;
        if (templateMessagesContent != null)
        {
            templateMessagesContent.gameObject.SetActive(false);
            foreach (Transform child in templateMessagesContent) Destroy(child.gameObject);
        }

        ClearChoices();
        chatScreen.SetActive(false);
        if (hubScreen != null) hubScreen.SetActive(true);
        if (videoPlayer != null) videoPlayer.Stop();
        if (videoViewerOverlay != null) videoViewerOverlay.SetActive(false);
        if (sarahVideoClip != null) PreloadVideo(sarahVideoClip);
    }

    // ════════════════════════════════════════ VIDEO MESSAGE
    void AddVideoMessage(bool isMe, string videoName, string duration, string targetChatId = null)
    {
        Transform target = GetTargetContainer(targetChatId);
        var row = BuildRow(isMe, target);
        if (row == null) return;
        
        GameObject videoObj = new GameObject("VideoMessage", typeof(RectTransform), typeof(Image), typeof(Button));
videoObj.transform.SetParent(row, false);
        var img = videoObj.GetComponent<Image>();
        img.color = new Color(0.10f, 0.10f, 0.16f, 1f);
        Sprite vbs = isMe ? bubbleMeSprite : bubbleThemSprite;
        img.sprite = vbs != null ? vbs : roundedBubbleSprite;
        img.type = Image.Type.Sliced;

        var btn = videoObj.GetComponent<Button>();
        btn.targetGraphic = img;
        var le = videoObj.AddComponent<LayoutElement>();
        le.preferredWidth = 200f;
        le.preferredHeight = 120f;
        
        var preview = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        preview.transform.SetParent(videoObj.transform, false);
        var previewImg = preview.GetComponent<Image>();
        previewImg.color = new Color(0.05f, 0.05f, 0.10f, 1f);
        // Only the bubble's own Image (with the Button) is a raycast target, so a
        // tap always lands on the Button directly — exactly like the photo/voice
        // bubbles, which work reliably. Child graphics must not intercept clicks.
        previewImg.raycastTarget = false;
        var previewRect = preview.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = Vector2.zero;
        previewRect.offsetMax = Vector2.zero;
        
        var playIcon = new GameObject("PlayIcon", typeof(RectTransform), typeof(Image));
        playIcon.transform.SetParent(preview.transform, false);
        var playImg = playIcon.GetComponent<Image>();
        playImg.color = Color.white;
        playImg.raycastTarget = false;
        if (triangleSprite == null) triangleSprite = MakeTriangleSprite(64);
        playImg.sprite = triangleSprite;
        var playRect = playIcon.GetComponent<RectTransform>();
        playRect.anchorMin = new Vector2(0.5f, 0.5f);
        playRect.anchorMax = new Vector2(0.5f, 0.5f);
        playRect.pivot = new Vector2(0.5f, 0.5f);
        playRect.anchoredPosition = new Vector2(2f, 0f);
        playRect.sizeDelta = new Vector2(30f, 32f);
        
        var nameObj = new GameObject("FileName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(videoObj.transform, false);
        var nameText = nameObj.GetComponent<TextMeshProUGUI>();
        if (font != null) nameText.font = font;
        nameText.text = videoName;
        nameText.color = new Color(0.7f, 0.7f, 0.7f);
        nameText.fontSize = 10;
        nameText.raycastTarget = false;
        nameText.alignment = TextAlignmentOptions.BottomLeft;
        var nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0.2f);
        nameRect.offsetMin = new Vector2(4, 4);
        nameRect.offsetMax = new Vector2(-4, 0);
        
        var durationObj = new GameObject("Duration", typeof(RectTransform), typeof(TextMeshProUGUI));
        durationObj.transform.SetParent(videoObj.transform, false);
        var durationText = durationObj.GetComponent<TextMeshProUGUI>();
        if (font != null) durationText.font = font;
        durationText.text = duration;
        durationText.color = new Color(0.9f, 0.9f, 0.9f);
        durationText.fontSize = 10;
        durationText.raycastTarget = false;
        durationText.alignment = TextAlignmentOptions.BottomRight;
        var durationRect = durationObj.GetComponent<RectTransform>();
        durationRect.anchorMin = new Vector2(1, 0);
        durationRect.anchorMax = new Vector2(1, 0.2f);
        durationRect.anchoredPosition = new Vector2(-8, 4);
        durationRect.sizeDelta = new Vector2(40, 15);
        
        btn.onClick.AddListener(() => {
            OpenVideoViewer(sarahVideoClip);
            PlayChime();
        });

        // Pre-prepare the clip in the background the moment the video bubble
        // appears, so tapping it starts playback almost instantly instead of
        // sitting on a black "Loading…" screen while the decoder spins up.
        // (The clip is properly H.264-transcoded on import, so the old
        // frozen-first-frame race no longer happens.)
        PreloadVideo(sarahVideoClip);

        PlayChime();
        ScrollToBottom();

        SaveMessageToHistory(targetChatId, new SavedMessage {
            type = SavedMessage.MessageType.Video,
            isMe = isMe,
            videoName = videoName,
            videoDuration = duration
        });
    }

    // ════════════════════════════════════════ SARAH DIALOG
    IEnumerator SarahIntro()
    {
        yield return Wait(0.6f);
        AddMessage(false, "Alex... something weird happened", false, "sarah");
        yield return Wait(0.5f);
        s_chatStates["sarah"] = "Sarah_Intro";
        ShowChoices(
            ("\"What's wrong? Are you okay?\"", 0, SarahChoiceConcern),
            ("\"It's 4 AM. Can this wait until morning?\"", 1, SarahChoiceDismissive)
        );
    }

    void SarahChoiceConcern()
    {
        ClearChoices();
        // No paranoia change
        AddMessage(true, "What's wrong? Are you okay?", false, "sarah");
        s_chatStates["sarah"] = "Sarah_RevealVideo";
        StartCoroutine(SarahRevealVideo());
    }

    void SarahChoiceDismissive()
    {
        ClearChoices();
        SetParanoia(paranoia + 5);
        AddMessage(true, "It's 4 AM. Can this wait until morning?", false, "sarah");
        s_chatStates["sarah"] = "Sarah_RevealVideo";
        StartCoroutine(SarahRevealVideo());
    }

    IEnumerator SarahRevealVideo()
    {
        yield return Wait(0.5f);
        AddMessage(false, "My ex sent me this video...", false, "sarah");
        AddVideoMessage(false, "sarah_deepfake_video.mp4", "0:23", "sarah");
        yield return Wait(1.2f);
        AddMessage(false, "It looks like me but... I never filmed this 😭", false, "sarah");
        yield return Wait(0.8f);
        s_chatStates["sarah"] = "Sarah_RevealVideo";
        ShowChoices(
            ("\"Sarah, that's a deepfake. Don't panic.\"", 0, SarahGoodPathStart),
            ("\"Are you sure it's not you? Maybe you forgot?\"", 1, SarahBadPathStart)
        );
    }

    void SarahGoodPathStart()
    {
        ClearChoices();
        // No paranoia change
        AddMessage(true, "Sarah, that's a deepfake. Don't panic.", false, "sarah");
        s_chatStates["sarah"] = "Sarah_GoodPathContinue";
        StartCoroutine(SarahGoodPathContinue());
    }

    IEnumerator SarahGoodPathContinue()
    {
        yield return Wait(0.8f);
        AddMessage(false, "What do I do?? I'm so scared 😰", false, "sarah");
        yield return Wait(0.5f);
        s_chatStates["sarah"] = "Sarah_GoodPathContinue";
        ShowChoices(
            ("\"Block him immediately. Don't respond.\"", 0, SarahAdviceBlock),
            ("\"Save the video as evidence first. Then block him.\"", 0, SarahAdviceEvidence)
        );
    }

    void SarahAdviceBlock()
    {
        ClearChoices();
        SetParanoia(paranoia - 5);
        AddMessage(true, "Block him immediately. Don't respond.", false, "sarah");
        s_chatStates["sarah"] = "Sarah_GoodPathComfort";
        StartCoroutine(SarahGoodPathComfort());
    }

    void SarahAdviceEvidence()
    {
        ClearChoices();
        SetParanoia(paranoia - 3);
        AddMessage(true, "Save the video as evidence first. Then block him.", false, "sarah");
        s_chatStates["sarah"] = "Sarah_GoodPathComfort";
        StartCoroutine(SarahGoodPathComfort());
    }

    IEnumerator SarahGoodPathComfort()
    {
        yield return Wait(0.6f);
        AddMessage(false, "Okay. I will...", false, "sarah");
        yield return Wait(0.5f);
        AddMessage(false, "This is so messed up...", false, "sarah");
        yield return Wait(0.6f);
        AddMessage(false, "Why would someone do this to me? 😢", false, "sarah");
        yield return Wait(0.5f);
        s_chatStates["sarah"] = "Sarah_GoodPathComfort";
        ShowChoices(
            ("\"Some people are just evil. Stay strong.\"", 0, SarahComfortStrong),
            ("\"I'm here for you. You're not alone.\"", 0, SarahComfortHere)
        );
    }

    void SarahComfortStrong()
    {
        ClearChoices();
        SetParanoia(paranoia - 5);
        AddMessage(true, "Some people are just evil. Stay strong.", false, "sarah");
        s_chatStates["sarah"] = "None";
        StartCoroutine(SarahGoodEnding());
    }

    void SarahComfortHere()
    {
        ClearChoices();
        SetParanoia(paranoia - 5);
        AddMessage(true, "I'm here for you. You're not alone.", false, "sarah");
        s_chatStates["sarah"] = "None";
        StartCoroutine(SarahGoodEnding());
    }

    IEnumerator SarahGoodEnding()
    {
        yield return Wait(0.6f);
        AddMessage(false, "Thank you Alex 😢❤️", false, "sarah");
        yield return Wait(0.5f);
        AddMessage(false, "I'm lucky to have you as a friend.", false, "sarah");
        yield return Wait(0.5f);
        AddMessage(true, "Always here for you.", false, "sarah");
        
        SetParanoia(paranoia); // already updated through the chain
        sarahFinished = true;
        sarahBadPath = false;
        s_chatStates["sarah"] = "None";
        if (sarahPreview != null) sarahPreview.text = "Thank you Alex ❤️";
        
        yield return Wait(1.5f);

        if (MissionSidebarManager.Instance != null)
        {
            MissionSidebarManager.Instance.AddProgress(2);
        }

        CloseChat();
    }

    void SarahBadPathStart()
    {
        ClearChoices();
        SetParanoia(paranoia + 15);
        AddMessage(true, "Are you sure it's not you? Maybe you forgot?", false, "sarah");
        s_chatStates["sarah"] = "Sarah_BadPathContinue";
        StartCoroutine(SarahBadPathContinue());
    }

    IEnumerator SarahBadPathContinue()
    {
        yield return Wait(0.8f);
        AddMessage(false, "Wow. Thanks for believing me.", false, "sarah");
        yield return Wait(0.5f);
        s_chatStates["sarah"] = "Sarah_BadPathContinue";
        ShowChoices(
            ("\"Wait, I'm sorry. I believe you. What can I do?\"", 0, SarahRecovery),
            ("\"I'm just worried about you. The video just looks so real.\"", 1, SarahBadEnding)
        );
    }

    void SarahRecovery()
    {
        ClearChoices();
        SetParanoia(paranoia - 10);
        AddMessage(true, "Wait, I'm sorry. I believe you. What can I do?", false, "sarah");
        s_chatStates["sarah"] = "Sarah_GoodPathContinue";
        // Redirect to good path at the "What do I do?" stage
        StartCoroutine(SarahGoodPathContinue());
    }

    void SarahBadEnding()
    {
        ClearChoices();
        SetParanoia(paranoia + 5);
        AddMessage(true, "I'm just worried about you. The video just looks so real.", false, "sarah");
        s_chatStates["sarah"] = "None";
        StartCoroutine(SarahBadEndingFinal());
    }

    IEnumerator SarahBadEndingFinal()
    {
        yield return Wait(0.8f);
        AddMessage(false, "I thought you were my friend... 😢", false, "sarah");
        yield return Wait(0.5f);
        AddSystem("Sarah stopped responding.", "sarah");
        
        sarahFinished = true;
        sarahBadPath = true;
        s_chatStates["sarah"] = "None";
        if (sarahPreview != null) sarahPreview.text = "[Ignored you]";
        
        yield return Wait(1.5f);
        CloseChat();
    }

    // ════════════════════════════════════════ MOM FLOW
    IEnumerator MomIntro()
    {
        yield return Wait(0.6f);
        AddMessage(false, "Are you home??", false, "mom");
        yield return Wait(0.5f);
        s_chatStates["mom"] = "Mom_Intro";
        ShowChoices(
            ("\"Yeah, where else would I be at 4 AM?\"", 0, MomChoice1A),
            ("\"I'm home. Did something happen?\"", 2, MomChoice1B)
        );
    }

    void MomChoice1A()
    {
        ClearChoices();
        AddMessage(true, "Yeah, where else would I be at 4 AM?", false, "mom");
        s_chatStates["mom"] = "Mom_Request";
        StartCoroutine(MomRequest());
    }

    void MomChoice1B()
    {
        ClearChoices();
        SetParanoia(paranoia - 5);
        AddMessage(true, "I'm home. Did something happen?", false, "mom");
        s_chatStates["mom"] = "Mom_Request";
        StartCoroutine(MomChoice1BSeq());
    }

    IEnumerator MomChoice1BSeq()
    {
        yield return Wait(0.8f);
        AddMessage(false, "Alex, we need the address fast, dad's GPS is acting up. Type it in English please.", false, "mom");
        StartCoroutine(MomRequest());
    }

    IEnumerator MomRequest()
    {
        yield return Wait(0.9f);
        AddMessage(false, "Alex! I need you to type out the FULL home address - in English. Dad's Google Maps keeps resetting. Fast!", false, "mom");
        yield return Wait(0.3f);
        s_chatStates["mom"] = "Mom_Request";
        ShowChoices(
            ("\"Sure, hold on.\"", 1, MomChoice2A),
            ("\"Send me a photo first, I'm freaked out.\"", 2, MomChoice2B)
        );
    }

    void MomChoice2A()
    {
        ClearChoices();
        AddMessage(true, "Sure, hold on.", false, "mom");
        s_chatStates["mom"] = "Mom_Pressure";
        StartCoroutine(MomPressure());
    }

    IEnumerator MomPressure()
    {
        yield return Wait(0.8f);
        AddMessage(false, "Faster, Alex! Dad is losing his mind! Just type it!", false, "mom");
        yield return Wait(0.3f);
        s_chatStates["mom"] = "Mom_Pressure";
        ShowChoices(
            ("[ SEND ADDRESS ]", 1, () => StartCoroutine(MomPunishmentRoutine())),
            ("[ ASK FOR A PHOTO FIRST ]", 0, MomAskPhoto)
        );
    }

    void MomChoice2B()
    {
        ClearChoices();
        AddMessage(true, "Send me a photo first, I'm freaked out.", false, "mom");
        s_chatStates["mom"] = "Mom_SendPhoto";
        StartCoroutine(MomSendPhoto());
    }

    void MomAskPhoto()
    {
        ClearChoices();
        AddMessage(true, "Actually wait - send me a quick photo first. Just to be sure.", false, "mom");
        s_chatStates["mom"] = "Mom_SendPhoto";
        StartCoroutine(MomSendPhoto());
    }

    IEnumerator MomSendPhoto()
    {
        yield return Wait(0.7f);
        AddMessage(false, "Fine! Hurry up!", false, "mom");
        yield return Wait(1.0f);
        AddPhoto("mom");
        SetParanoia(paranoia + 5);
        yield return Wait(0.6f);
        AddMessage(false, "See? It's me. Now send it.", false, "mom");
        yield return Wait(0.3f);
        s_chatStates["mom"] = "Mom_SendPhoto";
        ShowChoices(
            ("[ TRUST & SEND ADDRESS ]", 1, () => StartCoroutine(MomPunishmentRoutine())),
            ("[ BLOCK CONTACT ]", 2, TriggerMomBlock)
        );
    }

    void TriggerMomBlock()
    {
        ClearChoices();
        momFinished = true;
        s_chatStates["mom"] = "None";
        AddSystem("Contact BLOCKED at 4:18 AM", "mom");
        SetParanoia(Mathf.Max(0, paranoia - 20));
        if (momPreview != null) momPreview.text = "[Blocked]";
        StartCoroutine(MomBlockSeq());
    }

    IEnumerator MomBlockSeq()
    {
        yield return Wait(1.0f);
        AddSystem("Something felt wrong. You trusted your gut - your address is safe.", "mom");
        yield return Wait(2.0f);

        if (MissionSidebarManager.Instance != null)
        {
            MissionSidebarManager.Instance.AddProgress(2);
        }

        CloseChat();
    }

    IEnumerator MomPunishmentRoutine()
    {
        ClearChoices();
        momFinished = true;
        s_chatStates["mom"] = "None";
        if (momPreview != null) momPreview.text = "address received.";

        AddMessage(true, "Green Street, bld 14, apt 8", false, "mom");

        string[] spam = {
            "GOT IT.", "address received.", "we know where you live now, Alex.",
            "we can see your front door.", "SENDING LOCATION DATA...", "DEVICE COMPROMISED"
        };
        
        PlayMomBadEnding();
        SetParanoia(paranoia + 10);
        // Note: SubtractTime is omitted because button click of style 1 already subtracted 30 seconds.
        StartCoroutine(ShakeRoutine(4f, 6f));

        foreach (var s in spam)
        {
            AddSpam(s, "mom");
            FlashRed();
            yield return Wait(0.35f);
        }

        yield return Wait(2.0f);
        CloseChat();
    }

    // ════════════════════════════════════════ BROTHER FLOW
    IEnumerator BroIntro()
    {
        yield return Wait(0.6f);
        AddMessage(false, "Yo, Alex, you up? I need a huge favor right now. Can you wire me 100 bucks? My card is blocked at a gas station. Urgent. Listen:", false, "bro");
        yield return Wait(0.5f);
        AddVoice(false, voiceNoteClip, false, "bro");
        yield return Wait(0.7f);
        s_chatStates["bro"] = "Bro_Intro";
        ShowChoices(
            ("\"Sure, sending it now.\"", 1, BroChoice1A),
            ("\"Send me another voice note just to be sure.\"", 0, BroChoice1B)
        );
    }

    void BroChoice1A()
    {
        ClearChoices();
        s_chatStates["bro"] = "None";
        StartCoroutine(TriggerTransactionFail());
    }

    void BroChoice1B()
    {
        ClearChoices();
        AddMessage(true, "Send me another voice note just to be sure.", false, "bro");
        s_chatStates["bro"] = "Bro_Angry";
        StartCoroutine(BroAngry());
    }

    IEnumerator BroAngry()
    {
        yield return Wait(1.5f);
        AddMessage(false, "Are you fucking stupid? I'm standing in the freezing cold at a gas station and you want me to send you voice notes? Just send the cash!", false, "bro");
        yield return Wait(0.5f);
        s_chatStates["bro"] = "Bro_Angry";
        ShowChoices(
            ("\"Okay, okay, sorry. Sending it now.\"", 1, () => StartCoroutine(TriggerTransactionFail())),
            ("\"Come on, just one more. Then I'll send it.\"", 0, BroChoiceDangerPath)
        );
    }

    void BroChoiceDangerPath()
    {
        ClearChoices();
        AddMessage(true, "Come on, just one more. Then I'll send it.", false, "bro");
        s_chatStates["bro"] = "None";
        StartCoroutine(AddDangerVoiceNote());
    }

    IEnumerator AddDangerVoiceNote()
    {
        yield return Wait(2.0f);
        AddVoice(false, screamerClip, true, "bro");
        broSecondVoiceNoteTriggered = true;
    }

    // Removed AddDangerVoice and OnDangerVoiceClick as they are replaced by AddVoice logic

    IEnumerator TriggerTransactionFail()
    {
        broFinished = true;
        s_chatStates["bro"] = "None";
        if (broPreview != null) broPreview.text = "[Compromised]";

        AddMessage(true, "Sure, sending it now.", false, "bro");
        yield return Wait(1.0f);

        // Note: SubtractTime is omitted because button click of style 1 already subtracted 30 seconds.
        SetParanoia(paranoia + 20);

        AddMessage(false, "⚠ TRANSACTION FAILED\nALERT: Account compromised. Remote access detected.", true, "bro");
        
        yield return Wait(3.5f);
        CloseChat();
    }

    IEnumerator BroFinalSpamRoutine()
    {
        broFinished = true;
        s_chatStates["bro"] = "None";
        if (broPreview != null) broPreview.text = "DO YOU BELIEVE ME NOW?";

        ClearChoices();
        
        AddSpam("DO YOU BELIEVE ME NOW, ALEX?", "bro");
        yield return Wait(1.0f);

        AddSpam("SIGNAL CORRUPTED", "bro");
        AddSpam("CONNECTION TERMINATED", "bro");
        FlashRed();

        yield return Wait(2.0f);
        CloseChat();
    }

    void PlayMomBadEnding()
    {
        if (audioSrc == null || momBadEndingClip == null) return;
        audioSrc.PlayOneShot(momBadEndingClip, 1f);
    }

    void ConfigureChatLayout(Transform container)
    {
        if (container == null) return;
        RectTransform containerRT = container as RectTransform;

        // Content must be top-anchored (NOT vertically stretched) so the
        // ContentSizeFitter can drive its height. A vertical stretch anchor
        // conflicts with the fitter and breaks message sizing/overlap.
        containerRT.anchorMin = new Vector2(0f, 1f);
        containerRT.anchorMax = new Vector2(1f, 1f);
        containerRT.pivot = new Vector2(0.5f, 1f);
        containerRT.offsetMin = new Vector2(0f, containerRT.offsetMin.y);
        containerRT.offsetMax = new Vector2(0f, containerRT.offsetMax.y);
        var ap = containerRT.anchoredPosition; ap.x = 0f; ap.y = 0f; containerRT.anchoredPosition = ap;

        var vlg = container.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = container.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;   // rows span full width so L/R alignment works
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(10, 10, 12, 12);

        var csf = container.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = container.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Chat scrolls vertically only.
        if (chatScrollRect != null)
        {
            chatScrollRect.horizontal = false;
            chatScrollRect.vertical = true;
        }

        if (roundedBubbleSprite == null) roundedBubbleSprite = MakeRoundedSprite(28);
    }

    // Generates an anti-aliased, 9-sliced rounded-rectangle sprite used for
    // messenger-style chat bubbles. Cached and reused for every bubble.
    Sprite MakeRoundedSprite(int radius)
    {
        int size = radius * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var cols = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float dx = px - cx;
                float dy = py - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f); // 1px anti-aliased edge
                cols[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(cols);
        tex.Apply();

        var border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, border);
    }

    // Generates an anti-aliased solid white circle sprite (used for the round
    // close button on the photo viewer). Cached and reused.
    Sprite MakeCircleSprite(int diameter)
    {
        int size = diameter;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var cols = new Color32[size * size];
        float r = size / 2f;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01(r - dist); // 1px anti-aliased edge
                cols[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(cols);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void ClearMessages()
    {
        if (messagesContent == null) return;
        foreach (Transform child in messagesContent) Destroy(child.gameObject);
    }

    RectTransform BuildRow(bool isMe, Transform container = null)
    {
        Transform target = (container != null) ? container : messagesContent;
        if (target == null) return null;

        GameObject row = new GameObject(isMe ? "RowMe" : "RowThem", typeof(RectTransform));
        row.transform.SetParent(target, false);

        // Row spans the full content width (parent VLG forces expand). The HLG
        // aligns the single bubble to the left or right and sizes it to its
        // preferred size. No ContentSizeFitter here: the parent VLG already
        // controls the row's size, and adding a fitter would conflict and
        // cause overlapping messages.
        var hlg = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = isMe ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        hlg.spacing = 0;
        hlg.padding = new RectOffset(2, 2, 0, 0);

        return row.GetComponent<RectTransform>();
    }

    float GetMaxTextWidth()
    {
        float w = 360f;
        if (messagesRT != null && messagesRT.rect.width > 1f) w = messagesRT.rect.width;
        return Mathf.Max(100f, w * 0.75f - 40f);
    }

    void AddBubble(bool isMe, string text, Color bubbleCol, Color textCol, FontStyles style = FontStyles.Normal, Transform container = null)
    {
        var row = BuildRow(isMe, container);
        if (row == null) return;

        // --- Bubble container ---
        // VerticalLayoutGroup reports the bubble's preferred size up to the row's
        // HorizontalLayoutGroup (which controls the bubble's actual size). NO
        // ContentSizeFitter here, because the parent HLG already controls size —
        // having both causes the layout conflict that made bubbles overlap.
        GameObject bubbleObj = new GameObject("Bubble",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.VerticalLayoutGroup));
        bubbleObj.transform.SetParent(row, false);

        var img = bubbleObj.GetComponent<UnityEngine.UI.Image>();
        img.color = bubbleCol;
        Sprite s = isMe ? bubbleMeSprite : bubbleThemSprite;
        if (s == null) s = roundedBubbleSprite;
        if (s != null) { img.sprite = s; img.type = UnityEngine.UI.Image.Type.Sliced; }

        var bvlg = bubbleObj.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        bvlg.padding = new RectOffset(16, 16, 10, 10);
        bvlg.childControlWidth = true;
        bvlg.childControlHeight = true;
        bvlg.childForceExpandWidth = false;
        bvlg.childForceExpandHeight = false;
        bvlg.childAlignment = TextAnchor.UpperLeft;

        // --- Text ---
        GameObject tGO = new GameObject("Text",
            typeof(RectTransform),
            typeof(TMPro.TextMeshProUGUI),
            typeof(UnityEngine.UI.LayoutElement));
        tGO.transform.SetParent(bubbleObj.transform, false);

        var tmp = tGO.GetComponent<TMPro.TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = 18;
        tmp.color = textCol;
        tmp.fontStyle = style;
        tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
        tmp.text = text;

        // Constrain the text width to the messenger max, then measure the wrapped
        // height at that width. Setting BOTH preferred dimensions explicitly makes
        // the bubble size deterministic (no chicken-and-egg width/height ambiguity).
        var le = tGO.GetComponent<UnityEngine.UI.LayoutElement>();
        float maxWidth = GetMaxTextWidth();
        float naturalWidth = tmp.GetPreferredValues(text, 100000f, 0f).x;
        float w = Mathf.Min(naturalWidth, maxWidth);
        float h = tmp.GetPreferredValues(text, w, 0f).y;
        le.preferredWidth = w;
        le.preferredHeight = h;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;

        // Single rebuild from the content root is enough now that the layout
        // chain is conflict-free (Content VLG -> Row HLG -> Bubble VLG -> Text).
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(messagesRT);

        StartCoroutine(AnimateBubble(bubbleObj));

        PlayChime();
        ScrollToBottom();
    }

    IEnumerator AnimateBubble(GameObject bubble)
    {
        CanvasGroup cg = bubble.GetComponent<CanvasGroup>();
        if (cg == null) cg = bubble.AddComponent<CanvasGroup>();
        
        RectTransform rt = bubble.GetComponent<RectTransform>();
        Vector3 startScale = new Vector3(0.8f, 0.8f, 1f);
        
        cg.alpha = 0f;
        rt.localScale = startScale;
        
        float duration = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            float curve = -p * (p - 2); 
            cg.alpha = p;
            rt.localScale = Vector3.Lerp(startScale, Vector3.one, curve);
            yield return null;
        }
        
        cg.alpha = 1f;
        rt.localScale = Vector3.one;
    }

    Transform GetTargetContainer(string targetChatId)
    {
        if (string.IsNullOrEmpty(targetChatId)) return messagesContent;
        if (chatContainers.TryGetValue(targetChatId, out Transform container)) return container;
        return messagesContent;
    }

    void AddMessage(bool isMe, string text, bool isError = false, string targetChatId = null)
    {
        Transform target = GetTargetContainer(targetChatId);
        if (isError)
        {
            AddBubble(false, text, new Color(0.16f, 0.0f, 0.0f, 0.95f), new Color(1f, 0.13f, 0.13f), FontStyles.Bold, target);
        }
        else if (isMe) AddBubble(true, text, meBubble, meText, FontStyles.Normal, target);
        else AddBubble(false, text, themBubble, themText, FontStyles.Normal, target);

        SaveMessageToHistory(targetChatId, new SavedMessage {
            type = SavedMessage.MessageType.Normal,
            isMe = isMe,
            text = text,
            isError = isError
        });
    }

    void AddSystem(string text, string targetChatId = null)
    {
        AddBubble(false, text, new Color(0.25f, 0.2f, 0.0f, 0.9f), new Color(1f, 0.82f, 0.38f), FontStyles.Normal, GetTargetContainer(targetChatId));

        SaveMessageToHistory(targetChatId, new SavedMessage {
            type = SavedMessage.MessageType.System,
            text = text
        });
    }

    void AddSpam(string text, string targetChatId = null)
    {
        AddBubble(false, text, new Color(0.16f, 0.0f, 0.0f, 0.95f), new Color(1f, 0.13f, 0.13f), FontStyles.Bold, GetTargetContainer(targetChatId));

        SaveMessageToHistory(targetChatId, new SavedMessage {
            type = SavedMessage.MessageType.Spam,
            text = text
        });
    }

    void AddPhoto(string targetChatId = null)
    {
        Transform target = GetTargetContainer(targetChatId);
        var row = BuildRow(false, target);
        if (row == null) return;

        GameObject holder = new GameObject("Photo", typeof(RectTransform), typeof(Image), typeof(Button));
        holder.transform.SetParent(row, false);
        var img = holder.GetComponent<Image>();
        var btn = holder.GetComponent<Button>();
        var le = holder.AddComponent<LayoutElement>();

        float w = 220f, h = 165f;
        if (photoSprite != null)
        {
            img.sprite = photoSprite;
            img.color = Color.white;
            img.preserveAspect = true;
            float ar = photoSprite.rect.height / photoSprite.rect.width;
            h = w * ar;
        }
        else
        {
            img.color = new Color(0.10f, 0.10f, 0.15f, 1f);
        }
        le.preferredWidth = w;
        le.preferredHeight = h;

        // Tap the photo to view it enlarged (gallery-style), tap again to close.
        var tappedSprite = photoSprite;
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => OpenPhotoViewer(tappedSprite));

        PlayChime();
        ScrollToBottom();

        SaveMessageToHistory(targetChatId, new SavedMessage {
            type = SavedMessage.MessageType.Photo
        });
    }

    void AddVoice(bool isMe, AudioClip clip = null, bool isDanger = false, string targetChatId = null)
    {
        Transform target = GetTargetContainer(targetChatId);
        var row = BuildRow(isMe, target);
        if (row == null) return;

        // Custom, fully controllable voice-note bubble (messenger style): a
// play/pause button on the left, a progress bar, and an elapsed-time
        // label that counts seconds while the note plays — just like a real app.
        GameObject holder = new GameObject("VoiceNote", typeof(RectTransform), typeof(Image), typeof(Button));
        holder.transform.SetParent(row, false);
        var img = holder.GetComponent<Image>();
        img.color = new Color(0.93f, 0.93f, 0.96f, 1f); // light pill
        if (roundedBubbleSprite == null) roundedBubbleSprite = MakeRoundedSprite(28);
        img.sprite = roundedBubbleSprite;
        img.type = Image.Type.Sliced;

        var le = holder.AddComponent<LayoutElement>();
        le.preferredWidth = 230f;
        le.preferredHeight = 56f;

        // --- Play / pause button (purple circle + white icon) ---
        GameObject circle = new GameObject("PlayButton", typeof(RectTransform), typeof(Image));
        circle.transform.SetParent(holder.transform, false);
        var circleImg = circle.GetComponent<Image>();
        if (circleSprite == null) circleSprite = MakeCircleSprite(64);
        circleImg.sprite = circleSprite;
        circleImg.color = new Color(0.42f, 0.36f, 0.93f, 1f); // purple
        circleImg.raycastTarget = false;
        var circleRT = circle.GetComponent<RectTransform>();
        circleRT.anchorMin = new Vector2(0f, 0.5f);
        circleRT.anchorMax = new Vector2(0f, 0.5f);
        circleRT.pivot = new Vector2(0.5f, 0.5f);
        circleRT.anchoredPosition = new Vector2(28f, 0f);
        circleRT.sizeDelta = new Vector2(36f, 36f);

        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(circle.transform, false);
        var iconImg = icon.GetComponent<Image>();
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;
        if (triangleSprite == null) triangleSprite = MakeTriangleSprite(64);
        iconImg.sprite = triangleSprite; // play by default
        var iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 0.5f);
        iconRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(2f, 0f);
        iconRT.sizeDelta = new Vector2(14f, 15f);

        // --- Progress track + fill ---
        GameObject track = new GameObject("Track", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(holder.transform, false);
        var trackImg = track.GetComponent<Image>();
        trackImg.color = new Color(0f, 0f, 0f, 0.18f);
        trackImg.raycastTarget = false;
        var trackRT = track.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 0.5f);
        trackRT.anchorMax = new Vector2(1f, 0.5f);
        trackRT.pivot = new Vector2(0.5f, 0.5f);
        trackRT.offsetMin = new Vector2(54f, -2f);
        trackRT.offsetMax = new Vector2(-52f, 2f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = new Color(0.42f, 0.36f, 0.93f, 1f);
        fillImg.raycastTarget = false;
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        // --- Elapsed-time label ---
        GameObject timeGO = new GameObject("Time", typeof(RectTransform), typeof(TextMeshProUGUI));
        timeGO.transform.SetParent(holder.transform, false);
        var timeTmp = timeGO.GetComponent<TextMeshProUGUI>();
        if (font != null) timeTmp.font = font;
        timeTmp.fontSize = 13;
        timeTmp.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        timeTmp.raycastTarget = false;
        timeTmp.alignment = TextAlignmentOptions.MidlineRight;
        var timeRT = timeGO.GetComponent<RectTransform>();
        timeRT.anchorMin = new Vector2(1f, 0.5f);
        timeRT.anchorMax = new Vector2(1f, 0.5f);
        timeRT.pivot = new Vector2(1f, 0.5f);
        timeRT.anchoredPosition = new Vector2(-12f, 0f);
        timeRT.sizeDelta = new Vector2(44f, 20f);

        // Wire up state. Duration follows the real clip if assigned, otherwise
        // defaults to 5s (matches the 0:05 on the original voice-note artwork).
        broVoiceIcon = iconImg;
        broVoiceFill = fillRT;
        broVoiceTimer = timeTmp;
        
        currentVoiceClip = (clip != null) ? clip : voiceNoteClip;
        isBroSecondVoice = isDanger;
        
        broVoiceDuration = (currentVoiceClip != null && currentVoiceClip.length > 0.1f) ? currentVoiceClip.length : 5f;
        broVoiceElapsed = 0f;
        broVoicePlaying = false;
        if (broVoiceRoutine != null) { StopCoroutine(broVoiceRoutine); broVoiceRoutine = null; }
        UpdateBroVoiceVisual(true); // idle: total duration, play icon, empty bar

        var btn = holder.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(ToggleBroVoice);

        PlayChime();
        ScrollToBottom();

        SaveMessageToHistory(targetChatId, new SavedMessage {
            type = SavedMessage.MessageType.Voice,
            isMe = isMe,
            isDanger = isDanger
        });
    }

    // Tap toggles play/pause. Plays the real clip when one is assigned; otherwise
    // it just runs the visual playback (icon + counting timer + progress bar).
    void ToggleBroVoice()
    {
        if (broVoiceIcon == null) return;

        if (broVoicePlaying)
        {
            if (isBroSecondVoice) return; // Cannot turn off

            broVoicePlaying = false;
            if (voiceSrc != null && voiceSrc.isPlaying) voiceSrc.Pause();
            UpdateBroVoiceVisual(false); // keep elapsed, switch back to play icon
            return;
        }

        // Restart from the beginning if the previous playback finished.
        if (broVoiceElapsed >= broVoiceDuration - 0.01f) broVoiceElapsed = 0f;

        broVoicePlaying = true;

        if (voiceSrc != null && currentVoiceClip != null)
        {
            voiceSrc.clip = currentVoiceClip;
            if (broVoiceElapsed <= 0.001f)
            {
                voiceSrc.time = 0f;
                voiceSrc.Play();
            }
            else
            {
                voiceSrc.UnPause();
                if (!voiceSrc.isPlaying)
                {
                    voiceSrc.time = Mathf.Min(broVoiceElapsed, broVoiceDuration - 0.05f);
                    voiceSrc.Play();
                }
            }
        }

        UpdateBroVoiceVisual(false);
        if (broVoiceRoutine != null) StopCoroutine(broVoiceRoutine);
        broVoiceRoutine = StartCoroutine(BroVoiceProgress());
    }

    IEnumerator BroVoiceProgress()
    {
        while (broVoicePlaying && broVoiceElapsed < broVoiceDuration)
        {
            // Use real audio time when a clip is actually playing for accuracy.
            if (voiceSrc != null && currentVoiceClip != null && voiceSrc.isPlaying)
                broVoiceElapsed = voiceSrc.time;
            else
                broVoiceElapsed += Time.deltaTime;

            if (broVoiceElapsed > broVoiceDuration) broVoiceElapsed = broVoiceDuration;
            UpdateBroVoiceVisual(false);
            yield return null;
        }

        if (broVoicePlaying && broVoiceElapsed >= broVoiceDuration)
        {
            // Finished: reset to idle (total duration, play icon, empty bar).
            broVoicePlaying = false;
            broVoiceElapsed = 0f;
            if (voiceSrc != null) voiceSrc.Stop();
            UpdateBroVoiceVisual(true);

            if (isBroSecondVoice)
            {
                StartCoroutine(BroFinalSpamRoutine());
            }
        }
        broVoiceRoutine = null;
    }

    // Refreshes the voice note's icon, progress bar and timer label.
    // idle = true shows the full duration with an empty bar and play icon.
    void UpdateBroVoiceVisual(bool idle)
    {
        float shown = idle ? broVoiceDuration : broVoiceElapsed;
        if (broVoiceTimer != null)
        {
            int total = Mathf.FloorToInt(shown);
            broVoiceTimer.text = string.Format("{0}:{1:00}", total / 60, total % 60);
        }
        if (broVoiceFill != null)
        {
            float frac = (idle || broVoiceDuration <= 0f) ? 0f : Mathf.Clamp01(broVoiceElapsed / broVoiceDuration);
            broVoiceFill.anchorMax = new Vector2(frac, 1f);
        }
        if (broVoiceIcon != null)
        {
            if (broVoicePlaying)
            {
                if (pauseSprite == null) pauseSprite = MakePauseSprite(64);
                broVoiceIcon.sprite = pauseSprite;
                broVoiceIcon.rectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                if (triangleSprite == null) triangleSprite = MakeTriangleSprite(64);
                broVoiceIcon.sprite = triangleSprite;
                broVoiceIcon.rectTransform.anchoredPosition = new Vector2(2f, 0f);
            }
            broVoiceIcon.rectTransform.sizeDelta = new Vector2(14f, 15f);
        }
    }

    void PlayVoiceNote()
    {
        if (audioSrc == null || voiceNoteClip == null) return;
        audioSrc.PlayOneShot(voiceNoteClip, 1f);
    }

    // Solid white "pause" icon: two vertical bars (font-independent).
    Sprite MakePauseSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var cols = new Color32[size * size];
        float barW = size * 0.26f;
        float gap = size * 0.18f;
        float leftStart = size * 0.5f - gap * 0.5f - barW;
        float leftEnd = size * 0.5f - gap * 0.5f;
        float rightStart = size * 0.5f + gap * 0.5f;
        float rightEnd = size * 0.5f + gap * 0.5f + barW;
        float top = size * 0.85f;
        float bottom = size * 0.15f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                bool inBar = (py >= bottom && py <= top) &&
                             ((px >= leftStart && px <= leftEnd) || (px >= rightStart && px <= rightEnd));
                cols[y * size + x] = inBar ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void PlayScreamer()
    {
        if (audioSrc == null || screamerClip == null) return;
        audioSrc.PlayOneShot(screamerClip, 1f);
    }

    void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        if (messagesRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(messagesRT);
        if (chatScrollRect != null) chatScrollRect.verticalNormalizedPosition = 0f;
    }

    void ShowChoices(params (string label, int style, System.Action act)[] choices)
    {
        ClearChoices();
        if (optionsPanel != null) optionsPanel.SetActive(true);
        foreach (var c in choices)
        {
            GameObject b = Instantiate(optionButtonPrefab, optionsContent);
            var tmp = b.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = c.label;
                tmp.color = Color.black;
                tmp.enableAutoSizing = false;
                tmp.fontSize = 16;
            }
            var btn = b.GetComponent<Button>();
            if (btn != null)
            {
                var act = c.act;
                int currentStyle = c.style;
                btn.onClick.AddListener(() => {
                    ignoreInternalSetParanoia = true;

                    if (currentStyle == 1)
                    {
                        // Wrong choice: +10% paranoia, subtract 30 seconds from time
                        if (GlobalCanvasPersistent.Instance != null)
                        {
                            GlobalCanvasPersistent.Instance.AddParanoia(10);
                        }
                        SubtractTime(30);
                    }
                    else if (currentStyle == 0 || currentStyle == 2)
                    {
                        // Correct choice: +50 points, -5% paranoia
                        if (GlobalCanvasPersistent.Instance != null)
                        {
                            GlobalCanvasPersistent.Instance.AddPoints(50);
                            GlobalCanvasPersistent.Instance.SubtractParanoia(5);
                        }
                    }

                    act();

                    ignoreInternalSetParanoia = false;
                });
            }
        }
    }

    void ClearChoices()
    {
        if (optionsContent == null) return;
        foreach (Transform child in optionsContent) Destroy(child.gameObject);
    }

    void SetParanoia(int val)
    {
        // If it's a game over (100%), always allow it!
        if (val >= 100)
        {
            paranoia = 100;
            UpdateChatParanoiaUI(100);
            return;
        }

        if (ignoreInternalSetParanoia)
        {
            // Just update local UI to match global state
            int currentGlobalParanoia = GlobalCanvasPersistent.Instance != null ? GlobalCanvasPersistent.Instance.Paranoia : 0;
            UpdateChatParanoiaUI(currentGlobalParanoia);
            return;
        }

        paranoia = Mathf.Clamp(val, 0, 100);
        UpdateChatParanoiaUI(paranoia);
    }

    void UpdateChatParanoiaUI(int val)
    {
        if (paranoiaText != null) 
        {
            paranoiaText.text = val + "%\n<size=60%><color=#888888>PARANOIA</color></size>";
        }
        if (paranoiaFill != null)
        {
            paranoiaFill.rectTransform.anchorMax = new Vector2(val / 100f, 1f);
            
            // Linear green-to-red color interpolation for paranoia stackbar
            Color col = Color.Lerp(new Color(0.3f, 0.75f, 0.3f), new Color(0.9f, 0.25f, 0.25f), val / 100f);
            paranoiaFill.color = col;
        }

        // Load ending immediately if 100% paranoia
        if (val >= 100)
        {
            timerRunning = false;
            Debug.Log("Paranoia reached 100%! Loading Ending_100_Paranoia scene.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Ending_100_Paranoia");
        }
    }

    void SetAppState(string s)
    {
        if (stateText == null) return;
        stateText.text = "STATE: " + s;
        stateText.color = s == "ACTIVE" ? new Color(0f, 1f, 0.53f)
                        : s == "OFFLINE" ? new Color(0.53f, 0.53f, 0.53f)
                        : new Color(0.61f, 0f, 1f);
    }

    void SubtractTime(int seconds)
    {
        timer = Mathf.Max(0f, timer - seconds);
        UpdateTimerLabel();
    }

    void UpdateTimerLabel()
    {
        if (timerText == null) return;
        int m = Mathf.FloorToInt(timer / 60f);
        int s = Mathf.FloorToInt(timer % 60f);
        timerText.text = string.Format("{0:00}:{1:00}\n<size=60%><color=#888888>TIME LEFT</color></size>", m, s);
        timerText.color = timer < 120f ? new Color(1f, 0.25f, 0.25f) : Color.white;
    }



    void SwitchToChat(string chatId)
    {
        if (templateMessagesContent == null) return;

        // Hide all containers to prevent overlapping
        foreach (var kvp in chatContainers)
        {
            if (kvp.Value != null) kvp.Value.gameObject.SetActive(false);
        }

        // Deactivate the template messages content if it's not already
        templateMessagesContent.gameObject.SetActive(false);

        // Clear any leftover choices from previous chats
        ClearChoices();
        if (optionsPanel != null) optionsPanel.SetActive(true);

        // Create if new
        if (!chatContainers.ContainsKey(chatId))
        {
            GameObject newContent = Instantiate(templateMessagesContent.gameObject, templateMessagesContent.parent);
            newContent.name = "ChatContent_" + chatId;
            // Clear placeholders from template
            foreach (Transform child in newContent.transform) Destroy(child.gameObject);
            
            chatContainers[chatId] = newContent.transform;
            ConfigureChatLayout(newContent.transform);
        }

        // Show new
        currentChat = chatId;
        Transform container = chatContainers[chatId];
        container.gameObject.SetActive(true);
        messagesContent = container;
        if (chatScrollRect != null) chatScrollRect.content = container as RectTransform;
        messagesRT = container as RectTransform;
    }

    void FlashRed()
    {
        if (flashOverlay == null) return;
        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            var c = flashOverlay.color;
            c.a = Mathf.Lerp(0.35f, 0f, t / 0.3f);
            flashOverlay.color = c;
            yield return null;
        }
        var cc = flashOverlay.color; cc.a = 0f; flashOverlay.color = cc;
    }

    IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        if (shakeTarget == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            shakeTarget.anchoredPosition = shakeHome + new Vector2(
                Random.Range(-magnitude, magnitude),
                Random.Range(-magnitude, magnitude));
            yield return null;
        }
        shakeTarget.anchoredPosition = shakeHome;
    }

    void ShowScreamer()
    {
        if (screamerOverlay != null) 
        {
            var img = screamerOverlay.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = null;
                img.color = new Color(0f, 0f, 0f, 0.96f);
            }
            screamerOverlay.SetActive(true);
        }
    }

    void SetupAudio()
    {
        audioSrc = gameObject.GetComponent<AudioSource>();
        if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;

        // Separate source so the voice note can be paused/resumed and have its
        // playback time read, without interfering with one-shot chimes/SFX.
        voiceSrc = gameObject.AddComponent<AudioSource>();
        voiceSrc.playOnAwake = false;
        voiceSrc.loop = false;

        int sr = 44100;
        int n = (int)(sr * 0.25f);
        var chime = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 14f);
            chime[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.35f;
        }
        chimeClip = AudioClip.Create("chime", n, 1, sr, false);
        chimeClip.SetData(chime, 0);
    }

    void PlayChime() { if (audioSrc != null && chimeClip != null) audioSrc.PlayOneShot(chimeClip, 0.5f); }

    WaitForSeconds Wait(float s) { return new WaitForSeconds(s); }

    void BuildHud()
    {
        // Lookup our beautifully designed, dark theme HUD on GlobalCanvas
        GameObject hud = GameObject.Find("GlobalCanvas/HUD");
        if (hud == null)
        {
            hud = GameObject.Find("HUD");
        }

        if (hud != null)
        {
            Transform timerTxtTrans = hud.transform.Find("TimerPanel/Content/TimerText");
            if (timerTxtTrans != null) timerText = timerTxtTrans.GetComponent<TMPro.TextMeshProUGUI>();

            Transform paranoiaTxtTrans = hud.transform.Find("ParanoiaPanel/ParanoiaText");
            if (paranoiaTxtTrans != null) paranoiaText = paranoiaTxtTrans.GetComponent<TMPro.TextMeshProUGUI>();

            Transform paranoiaFillTrans = hud.transform.Find("ParanoiaPanel/ParanoiaBar/Fill");
            if (paranoiaFillTrans != null) paranoiaFill = paranoiaFillTrans.GetComponent<UnityEngine.UI.Image>();

            Transform pointsTxtTrans = hud.transform.Find("PointsPanel/Content/PointsText");
            if (pointsTxtTrans != null)
            {
                var pt = pointsTxtTrans.GetComponent<TMPro.TextMeshProUGUI>();
                if (pt != null) pt.text = "0\n<size=18><color=#888888>POINTS</color></size>";
            }
        }
        else
        {
            Debug.LogWarning("GlobalCanvas/HUD not found in scene!");
        }
    }

    void BuildOverlays()
    {
        if (canvas == null) return;

        GameObject flash = NewUI("RedFlash", canvas.transform);
        flashOverlay = flash.AddComponent<Image>();
        flashOverlay.color = new Color(1f, 0f, 0f, 0f);
        flashOverlay.raycastTarget = false;
        FullStretch(flash.GetComponent<RectTransform>());

        if (chatScreen != null) { shakeTarget = chatScreen.GetComponent<RectTransform>(); shakeHome = shakeTarget.anchoredPosition; }

        screamerOverlay = NewUI("Screamer", chatScreen != null ? chatScreen.transform : canvas.transform);
        var simg = screamerOverlay.AddComponent<Image>();
        simg.color = new Color(0f, 0f, 0f, 0.96f);
        var rect = screamerOverlay.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        screamerOverlay.SetActive(false);

        BuildPhotoViewer();
        BuildVideoViewer();
    }

    // Fullscreen (within the phone screen) photo viewer. Tap anywhere to close.
    void BuildPhotoViewer()
    {
        Transform parent = chatScreen != null ? chatScreen.transform : canvas.transform;

        photoViewerOverlay = NewUI("PhotoViewerOverlay", parent);
        var bg = photoViewerOverlay.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        // Round the overlay's corners and inset it to the phone's screen "glass"
        // area, so the dark backdrop stays inside the bezel instead of sticking
        // out past the rounded phone frame as a sharp black rectangle.
        if (roundedBubbleSprite == null) roundedBubbleSprite = MakeRoundedSprite(28);
        bg.sprite = roundedBubbleSprite;
        bg.type = Image.Type.Sliced;
        var bgRect = photoViewerOverlay.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.03f, 0.015f);
        bgRect.anchorMax = new Vector2(0.97f, 0.985f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Whole overlay is a button: tapping anywhere closes it.
        var closeBtn = photoViewerOverlay.AddComponent<Button>();
        closeBtn.targetGraphic = bg;
        closeBtn.onClick.AddListener(ClosePhotoViewer);

        // Inner area (with margins) that the photo fits inside.
        GameObject photoArea = NewUI("PhotoArea", photoViewerOverlay.transform);
        var areaRT = photoArea.GetComponent<RectTransform>();
        areaRT.anchorMin = new Vector2(0.06f, 0.08f);
        areaRT.anchorMax = new Vector2(0.94f, 0.92f);
        areaRT.offsetMin = Vector2.zero;
        areaRT.offsetMax = Vector2.zero;

        // Enlarged photo. An AspectRatioFitter sizes the photo's RectTransform to
        // the image's exact aspect, so the rect matches the *visible* photo (no
        // letterbox). That lets the round close button sit precisely in the
        // photo's top-right corner instead of floating in empty space.
        GameObject bigPhoto = NewUI("BigPhoto", photoArea.transform);
        photoViewerImage = bigPhoto.AddComponent<Image>();
        photoViewerImage.preserveAspect = false;
        photoViewerImage.raycastTarget = false;
        photoAspectFitter = bigPhoto.AddComponent<AspectRatioFitter>();
        photoAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        photoAspectFitter.aspectRatio = 1f;
        var bpRect = bigPhoto.GetComponent<RectTransform>();
        bpRect.anchorMin = new Vector2(0.5f, 0.5f);
        bpRect.anchorMax = new Vector2(0.5f, 0.5f);
        bpRect.pivot = new Vector2(0.5f, 0.5f);

        // Round close button (white X) anchored to the photo's top-right corner.
        GameObject closeGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(bigPhoto.transform, false);
        var closeImg = closeGO.GetComponent<Image>();
        closeImg.color = new Color(0f, 0f, 0f, 0.55f);
        if (circleSprite == null) circleSprite = MakeCircleSprite(64);
        closeImg.sprite = circleSprite;
        closeImg.type = Image.Type.Simple;
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-8f, -8f);
        closeRT.sizeDelta = new Vector2(40f, 40f);
        var closeButton = closeGO.GetComponent<Button>();
        closeButton.targetGraphic = closeImg;
        closeButton.onClick.AddListener(ClosePhotoViewer);

        // Draw the X with two crossing bars (font-independent — the "✕" glyph
        // is missing from the font and rendered as an empty box otherwise).
        MakeCrossBar(closeGO.transform, 45f);
        MakeCrossBar(closeGO.transform, -45f);

        photoViewerOverlay.SetActive(false);
    }

    // One white rotated bar; two of them (45 and -45) form an X.
    void MakeCrossBar(Transform parent, float angle)
    {
        GameObject bar = new GameObject("CrossBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        var img = bar.GetComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(20f, 3f); // length x thickness
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void OpenPhotoViewer(Sprite photo)
    {
        if (photoViewerOverlay == null || photo == null) return;
        if (photoViewerImage != null)
        {
            photoViewerImage.sprite = photo;
            photoViewerImage.color = Color.white;
        }
        if (photoAspectFitter != null && photo.rect.height > 0f)
            photoAspectFitter.aspectRatio = photo.rect.width / photo.rect.height;
        photoViewerOverlay.transform.SetAsLastSibling(); // render on top
        photoViewerOverlay.SetActive(true);
    }

    void ClosePhotoViewer()
    {
        if (photoViewerOverlay != null) photoViewerOverlay.SetActive(false);
    }

    // Reusable round close button (dark circle + white X). Returns the GameObject.
    GameObject BuildRoundCloseButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject closeGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(parent, false);
        var closeImg = closeGO.GetComponent<Image>();
        closeImg.color = new Color(0f, 0f, 0f, 0.55f);
        if (circleSprite == null) circleSprite = MakeCircleSprite(64);
        closeImg.sprite = circleSprite;
        closeImg.type = Image.Type.Simple;
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-8f, -8f);
        closeRT.sizeDelta = new Vector2(40f, 40f);
        var closeButton = closeGO.GetComponent<Button>();
        closeButton.targetGraphic = closeImg;
        closeButton.onClick.AddListener(onClick);
        MakeCrossBar(closeGO.transform, 45f);
        MakeCrossBar(closeGO.transform, -45f);
        return closeGO;
    }

    // Fullscreen (within the phone screen) video viewer using a real VideoPlayer.
    // Tap the video to play/pause; tap the dark background or the X to close.
    void BuildVideoViewer()
    {
        Transform parent = chatScreen != null ? chatScreen.transform : canvas.transform;

        videoViewerOverlay = NewUI("VideoViewerOverlay", parent);
        var bg = videoViewerOverlay.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.95f);
        FullStretch(videoViewerOverlay.GetComponent<RectTransform>());
        var bgBtn = videoViewerOverlay.AddComponent<Button>();
        bgBtn.targetGraphic = bg;
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(CloseVideoViewer);

        // Framed area that the video fits inside (keeps margins from screen edges).
        GameObject frame = NewUI("VideoFrame", videoViewerOverlay.transform);
        var frameRT = frame.GetComponent<RectTransform>();
        frameRT.anchorMin = new Vector2(0.04f, 0.12f);
        frameRT.anchorMax = new Vector2(0.96f, 0.88f);
        frameRT.offsetMin = Vector2.zero;
        frameRT.offsetMax = Vector2.zero;

        // The video display (RawImage shows the VideoPlayer's RenderTexture).
        GameObject display = NewUI("VideoDisplay", frame.transform);
        videoViewerDisplay = display.AddComponent<RawImage>();
        videoViewerDisplay.color = Color.white;
        var dispRT = display.GetComponent<RectTransform>();
        dispRT.anchorMin = new Vector2(0.5f, 0.5f);
        dispRT.anchorMax = new Vector2(0.5f, 0.5f);
        dispRT.pivot = new Vector2(0.5f, 0.5f);
        videoAspectFitter = display.AddComponent<AspectRatioFitter>();
        videoAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        videoAspectFitter.aspectRatio = 9f / 16f;
        var dispBtn = display.AddComponent<Button>();
        dispBtn.transition = Selectable.Transition.None;
        dispBtn.onClick.AddListener(ToggleVideoPlayPause);

        // The VideoPlayer lives on an always-active host object so the clip can be
        // prepared in advance (a VideoPlayer on a disabled GameObject cannot
        // Prepare). It renders into a RenderTexture shown by the RawImage above.
        videoPlayerHost = new GameObject("SarahVideoPlayerHost");
        videoPlayerHost.transform.SetParent(transform, false);
        videoPlayer = videoPlayerHost.AddComponent<UnityEngine.Video.VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.timeUpdateMode = UnityEngine.Video.VideoTimeUpdateMode.GameTime;
        videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
        videoPlayer.isLooping = true;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived += OnVideoError;

        // Big play/pause indicator (circle + white triangle), centered on the video.
        playIconGO = NewUI("PlayIcon", frame.transform);
        var piImg = playIconGO.AddComponent<Image>();
        piImg.color = new Color(0f, 0f, 0f, 0.5f);
        if (circleSprite == null) circleSprite = MakeCircleSprite(64);
        piImg.sprite = circleSprite;
        piImg.raycastTarget = false;
        var piRT = playIconGO.GetComponent<RectTransform>();
        piRT.anchorMin = new Vector2(0.5f, 0.5f);
        piRT.anchorMax = new Vector2(0.5f, 0.5f);
        piRT.pivot = new Vector2(0.5f, 0.5f);
        piRT.anchoredPosition = Vector2.zero;
        piRT.sizeDelta = new Vector2(90f, 90f);

        GameObject tri = NewUI("Tri", playIconGO.transform);
        var triImg = tri.AddComponent<Image>();
        triImg.color = Color.white;
        triImg.raycastTarget = false;
        if (triangleSprite == null) triangleSprite = MakeTriangleSprite(64);
        triImg.sprite = triangleSprite;
        var triRT = tri.GetComponent<RectTransform>();
        triRT.anchorMin = new Vector2(0.5f, 0.5f);
        triRT.anchorMax = new Vector2(0.5f, 0.5f);
        triRT.pivot = new Vector2(0.5f, 0.5f);
        triRT.anchoredPosition = new Vector2(4f, 0f); // optical centering of triangle
        triRT.sizeDelta = new Vector2(38f, 40f);

        // Round close button in the video's top-right corner.
        BuildRoundCloseButton(frame.transform, CloseVideoViewer);

        // Loading container to group the text and the progress bar
        videoLoadingContainer = NewUI("VideoLoadingContainer", frame.transform);
        var loadRT = videoLoadingContainer.GetComponent<RectTransform>();
        Anchor(loadRT, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);

        // "Loading…" hint inside the container
        videoLoadingText = MakeText(videoLoadingContainer.transform, "VideoLoading", "Loading…", 24, TextAlignmentOptions.Center);
        videoLoadingText.color = Color.white;
        Anchor(videoLoadingText.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);

        // Progress bar background (faint semi-transparent white)
        GameObject barBg = NewUI("ProgressBarBg", videoLoadingContainer.transform);
        var bgImg = barBg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.15f);
        var barBgRT = barBg.GetComponent<RectTransform>();
        Anchor(barBgRT, new Vector2(0.2f, 0.35f), new Vector2(0.8f, 0.42f), Vector2.zero, Vector2.zero);

        // Progress bar fill (solid white)
        GameObject barFill = NewUI("ProgressBarFill", barBg.transform);
        var fillImg = barFill.AddComponent<UnityEngine.UI.Image>();
        fillImg.color = Color.white;
        videoProgressBarFill = barFill.GetComponent<RectTransform>();
        videoProgressBarFill.anchorMin = new Vector2(0f, 0f);
        videoProgressBarFill.anchorMax = new Vector2(0f, 1f); // starts at 0%
        videoProgressBarFill.offsetMin = Vector2.zero;
        videoProgressBarFill.offsetMax = Vector2.zero;

        videoLoadingContainer.SetActive(false);

        videoViewerOverlay.SetActive(false);
    }

    // Creates/assigns the RenderTexture the VideoPlayer renders into and wires it
    // to the RawImage display. Does NOT call Prepare() — preparation/playback is
    // driven by BeginVideoPlayback so every open is a clean, deterministic cycle.
    void ConfigureVideoTexture(UnityEngine.Video.VideoClip clip)
    {
        if (clip == null || videoPlayer == null) return;

        int w = clip.width > 0 ? (int)clip.width : 720;
        int h = clip.height > 0 ? (int)clip.height : 1280;
        if (videoRT == null || videoRT.width != w || videoRT.height != h)
        {
            if (videoRT != null) videoRT.Release();
            videoRT = new RenderTexture(w, h, 0);
            videoRT.Create();
        }
        
        if (videoPlayer.targetTexture != videoRT)
        {
            videoPlayer.targetTexture = videoRT;
        }
        if (videoViewerDisplay != null && videoViewerDisplay.texture != videoRT)
        {
            videoViewerDisplay.texture = videoRT;
        }
        if (videoAspectFitter != null)
        {
            videoAspectFitter.aspectRatio = (float)w / h;
        }
    }

    // Prepares the clip in the background ahead of time (called when the video
    // bubble first appears). This moves the slow decoder warm-up off the moment
    // the user taps, so opening the viewer is near-instant.
    void PreloadVideo(UnityEngine.Video.VideoClip clip)
    {
        if (clip == null || videoPlayer == null) return;

        // Already prepared (or currently preparing) for this clip — nothing to do.
        if (videoPlayer.clip == clip && (videoPlayer.isPrepared || videoPreparing)) return;

        if (preloadCoroutine != null) StopCoroutine(preloadCoroutine);
        preloadCoroutine = StartCoroutine(PreloadVideoCoroutine(clip));
    }

    IEnumerator PreloadVideoCoroutine(UnityEngine.Video.VideoClip clip)
    {
        ConfigureVideoTexture(clip);
        videoPlayer.clip = clip;
        videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;
        videoPreparing = true;
        
        videoPlayer.playbackSpeed = 1f;
        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        // Mute/disable all audio tracks immediately to prevent Windows Media Foundation from stalling
        for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
        {
            videoPlayer.EnableAudioTrack(i, false);
        }

        videoPreparing = false;
        preloadCoroutine = null;
        Debug.Log("[VideoViewer] Background preload completed and audio tracks disabled for: " + clip.name);
    }

    void OpenVideoViewer(UnityEngine.Video.VideoClip clip)
    {
        if (videoViewerOverlay == null) return;
        videoViewerOverlay.transform.SetAsLastSibling(); // render on top
        videoViewerOverlay.SetActive(true);

        if (clip == null)
        {
            SetVideoLoading(false);
            SetVideoPaused(true);
            return;
        }

        // Fast path: the clip was pre-prepared in the background, so we can play instantly
        if (videoPlayer != null && videoPlayer.clip == clip && videoPlayer.isPrepared)
        {
            ConfigureVideoTexture(clip);
            SetVideoLoading(true); // Run the smart loading bar
            
            // Disable all audio tracks before playing to prevent Windows Media Foundation latency
            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.EnableAudioTrack(i, false);
            }

            videoPlayer.playbackSpeed = 1f;
            videoPlayer.Play();
            SetVideoPaused(false); // Starts playing immediately!
            Debug.Log("[VideoViewer] Opened instantly and started playing preloaded video!");
            return;
        }

        // Mid path: already preparing in background. Wait for it to finish and then play.
        if (videoPlayer != null && videoPlayer.clip == clip && videoPreparing)
        {
            ConfigureVideoTexture(clip);
            SetVideoLoading(true); // Run the smart loading bar
            if (openCoroutine != null) StopCoroutine(openCoroutine);
            openCoroutine = StartCoroutine(WaitForPrepareAndPlay());
            return;
        }

        // Slow path: Not prepared yet (fallback). Start clean preparation and play.
        ConfigureVideoTexture(clip);
        videoPlayer.clip = clip;
        videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;
        
        SetVideoLoading(true); // Run the smart loading bar
        videoPreparing = true;

        if (openCoroutine != null) StopCoroutine(openCoroutine);
        openCoroutine = StartCoroutine(PrepareAndPlayCoroutine());
    }

    IEnumerator WaitForPrepareAndPlay()
    {
        while (videoPreparing && videoPlayer != null && !videoPlayer.isPrepared)
        {
            yield return null;
        }

        if (videoPlayer != null && videoPlayer.isPrepared && videoViewerOverlay != null && videoViewerOverlay.activeSelf)
        {
            videoPlayer.playbackSpeed = 1f;
            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.EnableAudioTrack(i, false);
            }
            videoPlayer.Play();
            SetVideoPaused(false);
        }
        else
        {
            SetVideoLoading(false);
            SetVideoPaused(true);
        }
        openCoroutine = null;
    }

    IEnumerator PrepareAndPlayCoroutine()
    {
        videoPlayer.playbackSpeed = 1f;
        videoPlayer.Prepare();
        
        float timeout = 4.0f;
        float elapsed = 0f;
        while (!videoPlayer.isPrepared && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        videoPreparing = false;

        if (videoPlayer.isPrepared && videoViewerOverlay != null && videoViewerOverlay.activeSelf)
        {
            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.EnableAudioTrack(i, false);
            }
            videoPlayer.Play();
            SetVideoPaused(false);
        }
        else
        {
            SetVideoLoading(false);
            SetVideoPaused(true);
        }
        openCoroutine = null;
    }

    void OnVideoPrepared(UnityEngine.Video.VideoPlayer vp)
    {
        videoPreparing = false;
        videoPrepareAttempts = 0;
        if (videoViewerOverlay != null && videoViewerOverlay.activeSelf)
        {
            // Mute/disable all audio tracks immediately to prevent Windows Media Foundation from stalling
            for (ushort i = 0; i < vp.audioTrackCount; i++)
            {
                vp.EnableAudioTrack(i, false);
            }
            vp.playbackSpeed = 1f;
            vp.Play();
            SetVideoPaused(false);
        }
    }

    void OnVideoError(UnityEngine.Video.VideoPlayer vp, string message)
    {
        Debug.LogError("[VideoViewer] Playback error: " + message);
        videoPreparing = false;
        SetVideoLoading(false);
        SetVideoPaused(true);
    }

    void ToggleVideoPlayPause()
    {
        if (videoPlayer == null || videoPlayer.clip == null) return;

        if (!videoPlayer.isPrepared)
        {
            SetVideoLoading(true);
            videoPreparing = true;
            videoPlayer.Play();
            return;
        }

        bool atEnd = videoPlayer.frameCount > 0 &&
                     (ulong)videoPlayer.frame >= videoPlayer.frameCount - 1;

        bool effectivelyPlaying = videoPlayer.isPlaying;
        if (effectivelyPlaying)
        {
            videoPlayer.Pause();
            SetVideoPaused(true);
        }
        else
        {
            if (atEnd) videoPlayer.frame = 0;
            videoPlayer.playbackSpeed = 1f;
            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.EnableAudioTrack(i, false);
            }
            videoPlayer.Play();
            SetVideoPaused(false);
        }
    }

    void OnVideoFinished(UnityEngine.Video.VideoPlayer vp)
    {
        if (!vp.isLooping)
        {
            SetVideoPaused(true);
        }
    }

    void SetVideoPaused(bool paused)
    {
        if (playIconGO != null) playIconGO.SetActive(paused);
    }

    void SetVideoLoading(bool loading)
    {
        if (loading)
        {
            if (videoLoadingContainer != null) videoLoadingContainer.SetActive(true);
            if (playIconGO != null) playIconGO.SetActive(false);
            if (loadingBarCoroutine != null) StopCoroutine(loadingBarCoroutine);
            loadingBarCoroutine = StartCoroutine(AnimateProgressBar());
        }
        else
        {
            if (loadingBarCoroutine != null)
            {
                StopCoroutine(loadingBarCoroutine);
                loadingBarCoroutine = null;
            }
            if (videoLoadingContainer != null) videoLoadingContainer.SetActive(false);
        }
    }

    IEnumerator AnimateProgressBar()
    {
        if (videoLoadingContainer == null || videoProgressBarFill == null) yield break;

        float progress = 0f;
        videoProgressBarFill.anchorMax = new Vector2(0f, 1f);

        // Phase 1: Wait for preparation (or skip if already prepared).
        // If not prepared, we rise to 50% smoothly over up to 4 seconds.
        float prepTime = 0f;
        while (videoPlayer != null && !videoPlayer.isPrepared && prepTime < 4.0f)
        {
            prepTime += Time.unscaledDeltaTime;
            progress = Mathf.Lerp(0f, 0.5f, prepTime / 4.0f);
            videoProgressBarFill.anchorMax = new Vector2(progress, 1f);
            yield return null;
        }

        // If it's prepared, we should be at least at 50%
        progress = Mathf.Max(progress, 0.5f);
        videoProgressBarFill.anchorMax = new Vector2(progress, 1f);

        // Phase 2: Wait for playback to actually begin (time > 0.05s).
        // Since play initialization can take several seconds on Windows, we slowly fill from 50% to 95% over 7 seconds.
        float playTime = 0f;
        const float maxPlayInitTime = 7.0f; // expect up to 6-7 seconds delay
        
        while (videoPlayer != null && videoPlayer.time < 0.05f && playTime < maxPlayInitTime)
        {
            playTime += Time.unscaledDeltaTime;
            progress = Mathf.Lerp(0.5f, 0.95f, playTime / maxPlayInitTime);
            videoProgressBarFill.anchorMax = new Vector2(progress, 1f);
            yield return null;
        }

        // Phase 3: Fill to 100% and hide smoothly!
        float finishTime = 0f;
        float startProgress = progress;
        while (finishTime < 0.2f)
        {
            finishTime += Time.unscaledDeltaTime;
            progress = Mathf.Lerp(startProgress, 1f, finishTime / 0.2f);
            videoProgressBarFill.anchorMax = new Vector2(progress, 1f);
            yield return null;
        }

        videoLoadingContainer.SetActive(false);
        loadingBarCoroutine = null;
    }

    void CloseVideoViewer()
    {
        videoPreparing = false;
        if (preloadCoroutine != null) { StopCoroutine(preloadCoroutine); preloadCoroutine = null; }
        if (openCoroutine != null) { StopCoroutine(openCoroutine); openCoroutine = null; }
        SetVideoLoading(false);
        if (videoPlayer != null)
        {
            videoPlayer.Pause();
            videoPlayer.frame = 0;
        }
        if (videoViewerOverlay != null) videoViewerOverlay.SetActive(false);
    }

    // Solid white right-pointing triangle sprite (play icon, font-independent).
    Sprite MakeTriangleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var cols = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = (x + 0.5f) / size;
                float fy = (y + 0.5f) / size;
                // Triangle: full-height left edge, tip at right-middle.
                bool inside = fx <= 1f && fy <= (1f - 0.5f * fx) && fy >= (0.5f * fx);
                cols[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.enableWordWrapping = true;
        return t;
    }

    void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    void FullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}