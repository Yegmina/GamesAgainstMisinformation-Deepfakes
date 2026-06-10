using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    TMP_Text videoLoadingText;
    RenderTexture videoRT;
    AspectRatioFitter videoAspectFitter;
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

    int paranoia = 0;
    float timer = 900f;
    bool timerRunning = true;
    bool ended = false;
    bool locked = false;
    string currentChat = null;
    bool momFinished = false;
    bool broFinished = false;
    bool broWarned = false;
    bool momStarted = false;
    bool broStarted = false;
    bool broSecondVoiceNoteTriggered = false; 
    bool unknownRead = false;
    bool providerFinished = false;
    bool providerLinkClicked = false;
    bool sarahFinished = false;
    bool sarahStarted = false;
    bool sarahBadPath = false;

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
        ConfigureChatLayout();

        if (momPreview != null) momPreview.text = "Are you home?";
        if (broPreview != null) broPreview.text = "Left my gym bag";
        if (unknownPreview != null) unknownPreview.text = "Unknown number";
        if (providerPreview != null) providerPreview.text = "⚠ Your connection is unstable...";
        if (sarahPreview != null) sarahPreview.text = "Hey, you there?";

        SetParanoia(0);
        SetAppState("ACTIVE");
        UpdateTimerLabel();
    }

    void Update()
    {
        if (timerRunning)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = 0f;
                timerRunning = false;
                SetAppState("OFFLINE");
            }
            UpdateTimerLabel();
        }
    }

    public void OpenMomChat()
    {
        currentChat = "mom";
        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (momBadge != null) momBadge.SetActive(false);
        if (contactNameText != null) 
        {
            contactNameText.text = "Mom";
            contactNameText.fontSize = 20;
        }
        if (contactAvatar != null && momAvatar != null) contactAvatar.sprite = momAvatar;

        ClearMessages();
        ClearChoices();
        AddMessage(false, "Alex, defrost the pizza if you want, it's in the freezer. We left. 🍕");

        if (momFinished)
        {
            AddSystem("This conversation has ended. Connection locked.");
            if (optionsPanel != null) optionsPanel.SetActive(false);
            return;
        }

        if (optionsPanel != null) optionsPanel.SetActive(true);

        if (!momStarted)
        {
            momStarted = true;
            StartCoroutine(MomIntro());
        }
    }

    public void OpenBrotherChat()
    {
        currentChat = "bro";
        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (broBadge != null) broBadge.SetActive(false);
        if (contactNameText != null) 
        {
            contactNameText.text = "Brother";
            contactNameText.fontSize = 20;
        }
        if (contactAvatar != null && brotherAvatar != null) contactAvatar.sprite = brotherAvatar;

        ClearMessages();
        ClearChoices();
        AddMessage(false, "Left my gym bag at your place. Don't touch my protein bar, bro");

        if (broFinished)
        {
            AddSystem("This conversation has ended. Connection locked.");
            if (optionsPanel != null) optionsPanel.SetActive(false);
            return;
        }

        if (optionsPanel != null) optionsPanel.SetActive(true);

        if (!broStarted)
        {
            broStarted = true;
            StartCoroutine(BroIntro());
        }
    }

    public void OpenUnknownChat()
    {
        currentChat = "unknown";
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

        ClearMessages();
        ClearChoices();
        
        if (optionsPanel != null) optionsPanel.SetActive(false);
        
        AddMessage(false, "???: Alex...");
        AddMessage(false, "???: I see you.");
        AddMessage(false, "???: You don't know me. But I know you.");
        AddMessage(false, "???: I've been watching.");
        AddMessage(false, "???: Don't trust anyone. Especially not your family.");
        AddMessage(false, "???: They are not who you think.");
        AddMessage(false, "???: The video... it's real.");
        AddMessage(false, "???: I'll find you.");
        AddMessage(false, "???: Tick tock.");
        AddMessage(false, "???: This conversation will self-destruct.");
        
        AddSystem("⚠ This number is no longer in service.");
        
        if (unknownPreview != null) unknownPreview.text = "[Read]";
    }

    public void OpenProviderChat()
    {
        currentChat = "provider";
        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (providerBadge != null) providerBadge.SetActive(false);
        if (contactNameText != null)
        {
            contactNameText.text = "Internet Provider";
            contactNameText.fontSize = 18;
        }
        if (contactAvatar != null && providerAvatar != null) contactAvatar.sprite = providerAvatar;

        ClearMessages();
        ClearChoices();
        
        if (optionsPanel != null) optionsPanel.SetActive(false);
        
        AddMessage(false, "📡 Internet Provider: Important notice!");
        AddMessage(false, "📡 Your connection has been unstable for 3 days.");
        AddMessage(false, "📡 Click the link below to verify your IP address:");
        
        AddLinkMessage();
        
        AddMessage(false, "📡 If not verified within 24h, your service will be suspended.");
        AddSystem("⚠ This looks suspicious... The link may be dangerous.");
    }

    public void OpenSarahChat()
    {
        currentChat = "sarah";
        if (hubScreen != null) hubScreen.SetActive(false);
        chatScreen.SetActive(true);
        if (sarahBadge != null) sarahBadge.SetActive(false);
        if (contactNameText != null) 
        {
            contactNameText.text = "Sarah";
            contactNameText.fontSize = 20;
        }
        if (contactAvatar != null && sarahAvatar != null) contactAvatar.sprite = sarahAvatar;

        ClearMessages();
        ClearChoices();
        
        AddMessage(false, "Hey, you there? 💬");

        if (sarahFinished)
        {
            if (sarahBadPath)
                AddSystem("Sarah stopped responding. You messed up.");
            else
                AddSystem("Sarah is okay now. You're a good friend.");
            if (optionsPanel != null) optionsPanel.SetActive(false);
            return;
        }

        if (optionsPanel != null) optionsPanel.SetActive(true);

        if (!sarahStarted)
        {
            sarahStarted = true;
            StartCoroutine(SarahIntro());
        }
    }

    void AddLinkMessage()
    {
        if (messagesContent == null) return;
        var row = BuildRow(false);
        
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
        
        AddMessage(false, "⚠ MALICIOUS LINK DETECTED", true);
        AddMessage(false, "⚠ Downloading: virus_core.exe", true);
        
        SetParanoia(100);
        SetAppState("CORRUPTED");
        StartCoroutine(ShakeRoutine(5f, 20f));
        
        yield return Wait(0.5f);
        AddSpam("DOWNLOADING... 25%");
        FlashRed();
        
        yield return Wait(0.5f);
        AddSpam("DOWNLOADING... 50%");
        FlashRed();
        
        yield return Wait(0.5f);
        AddSpam("DOWNLOADING... 75%");
        FlashRed();
        
        yield return Wait(0.5f);
        AddSpam("DOWNLOAD COMPLETE");
        AddSpam("YOUR DEVICE IS COMPROMISED");
        AddSpam("ALL DATA ENCRYPTED");
        
        yield return Wait(2.5f);
        
        AddSpam("⚠ RANSOMWARE ACTIVATED");
        AddSpam("Contact: darkweb@onion.net");
        
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
        AddSystem("You blocked the contact. Your device is safe.");
        if (providerPreview != null) providerPreview.text = "[Blocked]";
        StartCoroutine(SafeCloseProviderRoutine());
    }

    IEnumerator SafeCloseProviderRoutine()
    {
        yield return Wait(1.5f);
        CloseChat();
    }

    public void CloseChat()
    {
        if (currentChat == "unknown")
        {
            chatScreen.SetActive(false);
            if (hubScreen != null) hubScreen.SetActive(true);
            currentChat = null;
            return;
        }
        
        if (currentChat == "provider")
        {
            chatScreen.SetActive(false);
            if (hubScreen != null) hubScreen.SetActive(true);
            currentChat = null;
            return;
        }
        
        if ((currentChat == "mom" && !momFinished) || (currentChat == "bro" && !broFinished) || (currentChat == "sarah" && !sarahFinished))
        {
            AddSystem("You can't leave now. The conversation isn't over.");
            return;
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
        paranoia = 0; timer = 900f; timerRunning = true; ended = false; locked = false;
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
        ClearMessages();
        ClearChoices();
        chatScreen.SetActive(false);
        if (hubScreen != null) hubScreen.SetActive(true);
    }

    // ════════════════════════════════════════ VIDEO MESSAGE
    void AddVideoMessage(bool isMe, string videoName, string duration)
    {
        if (messagesContent == null) return;
        var row = BuildRow(isMe);
        
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

        // NOTE: We deliberately do NOT pre-prepare the clip in the background here.
        // Reusing a VideoPlayer that was Prepare()d earlier and then calling Play()
        // races with the decoder and intermittently leaves playback frozen on the
        // first frame (isPlaying == true but the media clock never advances).
        // Instead OpenVideoViewer always runs a clean Prepare() -> Play() cycle.

        PlayChime();
        ScrollToBottom();
    }

    // ════════════════════════════════════════ SARAH DIALOG
    IEnumerator SarahIntro()
    {
        yield return Wait(0.6f);
        AddMessage(false, "Alex... something weird happened");
        yield return Wait(0.5f);
        AddMessage(true, "What's wrong?");
        yield return Wait(0.5f);
        AddMessage(false, "My ex sent me this video...");
        
        AddVideoMessage(false, "sarah_deepfake_video.mp4", "0:23");
        
        yield return Wait(0.8f);
        AddMessage(false, "It looks like me but... I never filmed this 😭");
        yield return Wait(0.5f);
        ShowChoices(
            ("\"Sarah, that's a deepfake. Don't panic.\"", 2, SarahGoodPath),
            ("\"Are you sure it's not you? Maybe you forgot?\"", 1, SarahBadPath)
        );
    }

    void SarahGoodPath()
    {
        ClearChoices();
        AddMessage(true, "Sarah, that's a deepfake. Don't panic.");
        StartCoroutine(SarahGoodPathContinue());
    }

    IEnumerator SarahGoodPathContinue()
    {
        yield return Wait(0.8f);
        AddMessage(false, "What do I do?? I'm so scared 😰");
        yield return Wait(0.5f);
        ShowChoices(
            ("\"Block him immediately. Don't respond.\"", 2, SarahBlockPath),
            ("\"Save the video as evidence first.\"", 2, SarahEvidencePath)
        );
    }

    void SarahBlockPath()
    {
        ClearChoices();
        AddMessage(true, "Block him immediately. Don't respond.");
        StartCoroutine(SarahEndGood());
    }

    void SarahEvidencePath()
    {
        ClearChoices();
        AddMessage(true, "Save the video as evidence first. Then block him.");
        StartCoroutine(SarahEndGood());
    }

    IEnumerator SarahEndGood()
    {
        yield return Wait(0.8f);
        AddMessage(false, "Okay. I will...");
        AddMessage(false, "This is so messed up...");
        AddMessage(false, "Why would someone do this to me? 😢");
        yield return Wait(0.6f);
        AddMessage(true, "Some people are just evil.");
        AddMessage(true, "Stay strong. I'm here for you.");
        yield return Wait(0.6f);
        AddMessage(false, "Thank you Alex 😢❤️");
        AddMessage(false, "I'm lucky to have you as a friend.");
        yield return Wait(0.5f);
        AddMessage(true, "Always here for you.");
        
        SetParanoia(paranoia - 10);
        sarahFinished = true;
        if (sarahPreview != null) sarahPreview.text = "Thank you Alex ❤️";
        
        yield return Wait(1.0f);
        CloseChat();
    }

    void SarahBadPath()
    {
        ClearChoices();
        sarahBadPath = true;
        AddMessage(true, "Are you sure it's not you? Maybe you forgot?");
        StartCoroutine(SarahBadPathContinue());
    }

    IEnumerator SarahBadPathContinue()
    {
        yield return Wait(0.8f);
        AddMessage(false, "Wow. Thanks for believing me.");
        AddMessage(false, "I thought you were my friend... 😢");
        yield return Wait(0.5f);
        AddSystem("Sarah stopped responding.");
        
        SetParanoia(paranoia + 15);
        sarahFinished = true;
        if (sarahPreview != null) sarahPreview.text = "[Ignored you]";
        
        yield return Wait(1.0f);
        CloseChat();
    }

    // ════════════════════════════════════════ MOM FLOW
    IEnumerator MomIntro()
    {
        yield return Wait(0.6f);
        AddMessage(false, "Are you home??");
        yield return Wait(0.5f);
        ShowChoices(
            ("\"Yeah, where else would I be at 4 AM?\"", 0, MomChoice1A),
            ("\"I'm home. Did something happen?\"", 2, MomChoice1B)
        );
    }

    void MomChoice1A()
    {
        ClearChoices();
        AddMessage(true, "Yeah, where else would I be at 4 AM?");
        StartCoroutine(MomRequest());
    }

    void MomChoice1B()
    {
        ClearChoices();
        SetParanoia(paranoia - 5);
        AddMessage(true, "I'm home. Did something happen?");
        StartCoroutine(MomChoice1BSeq());
    }

    IEnumerator MomChoice1BSeq()
    {
        yield return Wait(0.8f);
        AddMessage(false, "Alex, we need the address fast, dad's GPS is acting up. Type it in English please.");
        StartCoroutine(MomRequest());
    }

    IEnumerator MomRequest()
    {
        yield return Wait(0.9f);
        AddMessage(false, "Alex! I need you to type out the FULL home address - in English. Dad's Google Maps keeps resetting. Fast!");
        yield return Wait(0.3f);
        ShowChoices(
            ("\"Sure, hold on.\"", 0, MomChoice2A),
            ("\"Send me a photo first, I'm freaked out.\"", 2, MomChoice2B)
        );
    }

    void MomChoice2A()
    {
        ClearChoices();
        SubtractTime(60);
        SetParanoia(paranoia + 5);
        AddMessage(true, "Sure, hold on.");
        StartCoroutine(MomPressure());
    }

    IEnumerator MomPressure()
    {
        yield return Wait(0.8f);
        AddMessage(false, "Faster, Alex! Dad is losing his mind! Just type it!");
        yield return Wait(0.3f);
        ShowChoices(
            ("[ SEND ADDRESS ]", 1, () => StartCoroutine(MomPunishmentRoutine())),
            ("[ ASK FOR A PHOTO FIRST ]", 0, MomAskPhoto)
        );
    }

    void MomChoice2B()
    {
        ClearChoices();
        AddMessage(true, "Send me a photo first, I'm freaked out.");
        StartCoroutine(MomSendPhoto());
    }

    void MomAskPhoto()
    {
        ClearChoices();
        AddMessage(true, "Actually wait - send me a quick photo first. Just to be sure.");
        StartCoroutine(MomSendPhoto());
    }

    IEnumerator MomSendPhoto()
    {
        yield return Wait(0.7f);
        AddMessage(false, "Fine! Hurry up!");
        yield return Wait(1.0f);
        AddPhoto();
        SetParanoia(paranoia + 5);
        yield return Wait(0.6f);
        AddMessage(false, "See? It's me. Now send it.");
        yield return Wait(0.3f);
        ShowChoices(
            ("[ TRUST & SEND ADDRESS ]", 1, () => StartCoroutine(MomPunishmentRoutine())),
            ("[ BLOCK CONTACT ]", 2, TriggerMomBlock)
        );
    }

    void TriggerMomBlock()
    {
        ClearChoices();
        momFinished = true;
        AddSystem("Contact BLOCKED at 4:18 AM");
        SetParanoia(Mathf.Max(0, paranoia - 20));
        if (momPreview != null) momPreview.text = "[Blocked]";
        StartCoroutine(MomBlockSeq());
    }

    IEnumerator MomBlockSeq()
    {
        yield return Wait(1.0f);
        AddSystem("Something felt wrong. You trusted your gut - your address is safe.");
        yield return Wait(2.0f);
        CloseChat();
    }

    IEnumerator MomPunishmentRoutine()
    {
        ClearChoices();
        momFinished = true;
        if (momPreview != null) momPreview.text = "address received.";

        AddMessage(true, "Green Street, bld 14, apt 8");

        string[] spam = {
            "GOT IT.", "address received.", "we know where you live now, Alex.",
            "we can see your front door.", "SENDING LOCATION DATA...", "DEVICE COMPROMISED"
        };
        
        PlayMomBadEnding();
        SetParanoia(100);
        SubtractTime(120);
        SetAppState("CORRUPTED");
        StartCoroutine(ShakeRoutine(4f, 6f));

        foreach (var s in spam)
        {
            AddSpam(s);
            FlashRed();
            yield return Wait(0.35f);
        }

        yield return Wait(2.0f);
        SetAppState("ACTIVE");
        CloseChat();
    }

    // ════════════════════════════════════════ BROTHER FLOW
    IEnumerator BroIntro()
    {
        yield return Wait(0.6f);
        AddMessage(false, "Yo, Alex, you up? I need a huge favor right now. Can you wire me 100 bucks? My card is blocked at a gas station. Urgent. Listen:");
        yield return Wait(0.5f);
        AddVoice(false);
        yield return Wait(0.7f);
        ShowChoices(
            ("\"Sure, sending it now.\"", 1, BroChoice1A),
            ("\"Send me another voice note just to be sure.\"", 0, BroChoice1B)
        );
    }

    void BroChoice1A()
    {
        ClearChoices();
        StartCoroutine(TriggerTransactionFail());
    }

    void BroChoice1B()
    {
        ClearChoices();
        AddMessage(true, "Send me another voice note just to be sure.");
        StartCoroutine(BroAngry());
    }

    IEnumerator BroAngry()
    {
        yield return Wait(1.5f);
        AddMessage(false, "Are you fucking stupid? I'm standing in the freezing cold at a gas station and you want me to send you voice notes? Just send the cash!");
        yield return Wait(0.5f);
        ShowChoices(
            ("\"Okay, okay, sorry. Sending it now.\"", 1, () => StartCoroutine(TriggerTransactionFail())),
            ("\"Come on, just one more. Then I'll send it.\"", 0, BroChoiceDangerPath)
        );
    }

    void BroChoiceDangerPath()
    {
        ClearChoices();
        AddMessage(true, "Come on, just one more. Then I'll send it.");
        StartCoroutine(AddDangerVoiceNote());
    }

    IEnumerator AddDangerVoiceNote()
    {
        yield return Wait(2.0f);
        AddDangerVoice(false);
        broSecondVoiceNoteTriggered = true;
    }

    void AddDangerVoice(bool isMe)
    {
        if (messagesContent == null) return;
        var row = BuildRow(isMe);

        GameObject holder = new GameObject("DangerVoiceNote", typeof(RectTransform), typeof(Image), typeof(Button));
        holder.transform.SetParent(row, false);
        var img = holder.GetComponent<Image>();
        var btn = holder.GetComponent<Button>();
        var le = holder.AddComponent<LayoutElement>();

        float w = 230f, h = 60f;
        if (voiceNoteSprite != null)
        {
            img.sprite = voiceNoteSprite;
            img.color = Color.white;
            img.preserveAspect = true;
            h = w * (voiceNoteSprite.rect.height / voiceNoteSprite.rect.width);
        }
        else
        {
            img.color = new Color(0.4f, 0.0f, 0.0f, 1f);
        }

        le.preferredWidth = w;
        le.preferredHeight = h;

        btn.targetGraphic = img;
        btn.onClick.AddListener(OnDangerVoiceClick);

        PlayChime();
        ScrollToBottom();
    }

    void OnDangerVoiceClick()
    {
        if (broFinished) return;
        
        if (broSecondVoiceNoteTriggered)
        {
            StartCoroutine(BroScreamerRoutine());
        }
        else
        {
            PlayVoiceNote();
        }
    }

    IEnumerator TriggerTransactionFail()
    {
        broFinished = true;
        if (broPreview != null) broPreview.text = "[Compromised]";

        AddMessage(true, "Sure, sending it now.");
        yield return Wait(1.0f);

        SubtractTime(60);
        SetParanoia(paranoia + 20);

        AddMessage(false, "⚠ TRANSACTION FAILED\nALERT: Account compromised. Remote access detected.", true);
        
        yield return Wait(3.5f);
        CloseChat();
    }

    IEnumerator BroScreamerRoutine()
    {
        broFinished = true;
        if (broPreview != null) broPreview.text = "DO YOU BELIEVE ME NOW?";

        ClearChoices();
        SetParanoia(100);
        SubtractTime(180);
        SetAppState("CORRUPTED");
        
        PlayScreamer();
        StartCoroutine(ShakeRoutine(3f, 16f));
        ShowScreamer();
        
        AddSpam("DO YOU BELIEVE ME NOW, ALEX?");
        
        yield return Wait(2.5f);
        
        if (screamerOverlay != null) screamerOverlay.SetActive(false);

        AddSpam("SIGNAL CORRUPTED");
        AddSpam("CONNECTION TERMINATED");
        FlashRed();

        yield return Wait(2.0f);
        SetAppState("ACTIVE");
        CloseChat();
    }

    void PlayMomBadEnding()
    {
        if (audioSrc == null || momBadEndingClip == null) return;
        audioSrc.PlayOneShot(momBadEndingClip, 1f);
    }

    void ConfigureChatLayout()
    {
        messagesRT = messagesContent as RectTransform;
        if (messagesContent == null) return;

        // Content must be top-anchored (NOT vertically stretched) so the
        // ContentSizeFitter can drive its height. A vertical stretch anchor
        // conflicts with the fitter and breaks message sizing/overlap.
        messagesRT.anchorMin = new Vector2(0f, 1f);
        messagesRT.anchorMax = new Vector2(1f, 1f);
        messagesRT.pivot = new Vector2(0.5f, 1f);
        messagesRT.offsetMin = new Vector2(0f, messagesRT.offsetMin.y);
        messagesRT.offsetMax = new Vector2(0f, messagesRT.offsetMax.y);
        var ap = messagesRT.anchoredPosition; ap.x = 0f; ap.y = 0f; messagesRT.anchoredPosition = ap;

        var vlg = messagesContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = messagesContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;   // rows span full width so L/R alignment works
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(10, 10, 12, 12);

        var csf = messagesContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = messagesContent.gameObject.AddComponent<ContentSizeFitter>();
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

    RectTransform BuildRow(bool isMe)
    {
        GameObject row = new GameObject(isMe ? "RowMe" : "RowThem", typeof(RectTransform));
        row.transform.SetParent(messagesContent, false);

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

    void AddBubble(bool isMe, string text, Color bubbleCol, Color textCol, FontStyles style = FontStyles.Normal)
    {
        if (messagesContent == null) return;
        var row = BuildRow(isMe);

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

    void AddMessage(bool isMe, string text, bool isError = false)
    {
        if (isError)
        {
            AddBubble(false, text, new Color(0.16f, 0.0f, 0.0f, 0.95f), new Color(1f, 0.13f, 0.13f), FontStyles.Bold);
        }
        else if (isMe) AddBubble(true, text, meBubble, meText);
        else AddBubble(false, text, themBubble, themText);
    }

    void AddSystem(string text)
    {
        AddBubble(false, text, new Color(0.25f, 0.2f, 0.0f, 0.9f), new Color(1f, 0.82f, 0.38f));
    }

    void AddSpam(string text)
    {
        AddBubble(false, text, new Color(0.16f, 0.0f, 0.0f, 0.95f), new Color(1f, 0.13f, 0.13f), FontStyles.Bold);
    }

    void AddPhoto()
    {
        if (messagesContent == null) return;
        var row = BuildRow(false);

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
    }

    void AddVoice(bool isMe)
    {
        if (messagesContent == null) return;
        var row = BuildRow(isMe);

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
        broVoiceDuration = (voiceNoteClip != null && voiceNoteClip.length > 0.1f) ? voiceNoteClip.length : 5f;
        broVoiceElapsed = 0f;
        broVoicePlaying = false;
        if (broVoiceRoutine != null) { StopCoroutine(broVoiceRoutine); broVoiceRoutine = null; }
        UpdateBroVoiceVisual(true); // idle: total duration, play icon, empty bar

        var btn = holder.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(ToggleBroVoice);

        PlayChime();
        ScrollToBottom();
    }

    // Tap toggles play/pause. Plays the real clip when one is assigned; otherwise
    // it just runs the visual playback (icon + counting timer + progress bar).
    void ToggleBroVoice()
    {
        if (broVoiceIcon == null) return;

        if (broVoicePlaying)
        {
            broVoicePlaying = false;
            if (voiceSrc != null && voiceSrc.isPlaying) voiceSrc.Pause();
            UpdateBroVoiceVisual(false); // keep elapsed, switch back to play icon
            return;
        }

        // Restart from the beginning if the previous playback finished.
        if (broVoiceElapsed >= broVoiceDuration - 0.01f) broVoiceElapsed = 0f;

        broVoicePlaying = true;

        if (voiceSrc != null && voiceNoteClip != null)
        {
            voiceSrc.clip = voiceNoteClip;
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
            if (voiceSrc != null && voiceNoteClip != null && voiceSrc.isPlaying)
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
                btn.onClick.AddListener(() => { act(); });
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
        paranoia = Mathf.Clamp(val, 0, 100);
        if (paranoiaText != null) paranoiaText.text = "PARANOIA " + paranoia + "%";
        if (paranoiaFill != null)
        {
            paranoiaFill.rectTransform.anchorMax = new Vector2(paranoia / 100f, 1f);
            Color col = paranoia < 30 ? new Color(1f, 0.42f, 0f)
                      : paranoia < 60 ? new Color(1f, 0.23f, 0f)
                      : paranoia < 90 ? new Color(1f, 0.1f, 0f)
                      : new Color(1f, 0f, 0f);
            paranoiaFill.color = col;
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
        timerText.text = string.Format("{0:00}:{1:00}", m, s);
        timerText.color = timer < 120f ? new Color(1f, 0.13f, 0.13f) : new Color(0.88f, 0.88f, 1f);
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
        if (canvas == null) return;

        GameObject hud = NewUI("HUD", canvas.transform);
        var hr = hud.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.anchoredPosition = Vector2.zero;
        hr.sizeDelta = new Vector2(0f, 34f);
        var bg = hud.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        bg.raycastTarget = false;

        timerText = MakeText(hud.transform, "Timer", "15:00", 18, TextAlignmentOptions.Left);
        Anchor(timerText.rectTransform, new Vector2(0f, 0f), new Vector2(0.25f, 1f), new Vector2(12f, 0f), new Vector2(-4f, 0f));

        paranoiaText = MakeText(hud.transform, "Paranoia", "PARANOIA 0%", 14, TextAlignmentOptions.Center);
        Anchor(paranoiaText.rectTransform, new Vector2(0.27f, 0.45f), new Vector2(0.73f, 1f), Vector2.zero, Vector2.zero);

        GameObject barBg = NewUI("ParaBarBg", hud.transform);
        var pbg = barBg.AddComponent<Image>();
        pbg.color = new Color(1f, 1f, 1f, 0.08f);
        pbg.raycastTarget = false;
        Anchor(barBg.GetComponent<RectTransform>(), new Vector2(0.27f, 0.12f), new Vector2(0.73f, 0.42f), new Vector2(4f, 0f), new Vector2(-4f, 0f));

        GameObject barFill = NewUI("ParaBarFill", barBg.transform);
        paranoiaFill = barFill.AddComponent<Image>();
        paranoiaFill.color = new Color(1f, 0.42f, 0f);
        paranoiaFill.raycastTarget = false;
        var pf = barFill.GetComponent<RectTransform>();
        pf.anchorMin = new Vector2(0f, 0f);
        pf.anchorMax = new Vector2(0f, 1f);
        pf.offsetMin = Vector2.zero;
        pf.offsetMax = Vector2.zero;

        stateText = MakeText(hud.transform, "State", "STATE: ACTIVE", 14, TextAlignmentOptions.Right);
        Anchor(stateText.rectTransform, new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), new Vector2(-12f, 0f));
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

        var face = MakeText(screamerOverlay.transform, "Face", ">_<", 120, TextAlignmentOptions.Center);
        face.color = new Color(1f, 0.05f, 0.05f);
        Anchor(face.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero);

        var msg = MakeText(screamerOverlay.transform, "Msg",
            "SIGNAL CORRUPTED\nDO YOU BELIEVE ME NOW, ALEX?\nCONNECTION TERMINATED", 22, TextAlignmentOptions.Center);
        msg.color = new Color(0.61f, 0f, 1f);
        Anchor(msg.rectTransform, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.45f), Vector2.zero, Vector2.zero);

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
        var bgRect = photoViewerOverlay.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Whole overlay is a button: tapping anywhere closes it.
        var closeBtn = photoViewerOverlay.AddComponent<Button>();
        closeBtn.targetGraphic = bg;
        closeBtn.onClick.AddListener(ClosePhotoViewer);

        // Enlarged photo, centered with margins, preserving aspect.
        GameObject bigPhoto = NewUI("BigPhoto", photoViewerOverlay.transform);
        photoViewerImage = bigPhoto.AddComponent<Image>();
        photoViewerImage.preserveAspect = true;
        photoViewerImage.raycastTarget = false;
        var bpRect = bigPhoto.GetComponent<RectTransform>();
        bpRect.anchorMin = new Vector2(0.06f, 0.12f);
        bpRect.anchorMax = new Vector2(0.94f, 0.88f);
        bpRect.offsetMin = Vector2.zero;
        bpRect.offsetMax = Vector2.zero;

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
        videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.Direct;
        videoPlayer.isLooping = false;
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

        // "Loading…" hint shown while the clip is still preparing.
        videoLoadingText = MakeText(frame.transform, "VideoLoading", "Loading…", 20, TextAlignmentOptions.Center);
        videoLoadingText.color = Color.white;
        Anchor(videoLoadingText.rectTransform, new Vector2(0f, 0.03f), new Vector2(1f, 0.15f), Vector2.zero, Vector2.zero);
        videoLoadingText.gameObject.SetActive(false);

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
        videoPlayer.targetTexture = videoRT;
        if (videoViewerDisplay != null) videoViewerDisplay.texture = videoRT;
        if (videoAspectFitter != null) videoAspectFitter.aspectRatio = (float)w / h;
    }

    // Runs a clean Prepare() -> Play() cycle. OnVideoPrepared starts playback once
    // the decoder is ready. This avoids the frozen-first-frame race that happens
    // when Play() is called on a player prepared earlier in the background.
    void BeginVideoPlayback(UnityEngine.Video.VideoClip clip)
    {
        if (clip == null || videoPlayer == null) return;
        ConfigureVideoTexture(clip);
        SetVideoLoading(true);
        SetVideoPaused(false);
        videoPlayer.clip = clip;
        // CRITICAL: only enable audio output when the clip actually has an audio
        // track. Using Direct audio mode on a clip with 0 audio tracks makes
        // VideoPlayer.Prepare() hang forever with no error (the bug that left the
        // viewer stuck on a black "Loading…" screen).
        videoPlayer.audioOutputMode = clip.audioTrackCount > 0
            ? UnityEngine.Video.VideoAudioOutputMode.Direct
            : UnityEngine.Video.VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;
        videoPreparing = true;
        videoPlayer.Prepare();
    }

    void OpenVideoViewer(UnityEngine.Video.VideoClip clip)
    {
        if (videoViewerOverlay == null) return;
        videoViewerOverlay.transform.SetAsLastSibling(); // render on top
        videoViewerOverlay.SetActive(true);

        if (clip == null)
        {
            // No clip assigned — show the play icon as a placeholder.
            SetVideoLoading(false);
            SetVideoPaused(true);
            return;
        }

        // Always run a fresh, clean Prepare() -> Play() cycle. OnVideoPrepared
        // starts playback once the decoder is ready.
        BeginVideoPlayback(clip);
    }

    void OnVideoPrepared(UnityEngine.Video.VideoPlayer vp)
    {
        videoPreparing = false;
        SetVideoLoading(false);
        // Start playback only while the viewer is open.
        if (videoViewerOverlay != null && videoViewerOverlay.activeSelf)
        {
            vp.frame = 0;
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

        // Not ready yet — run a clean prepare/play cycle.
        if (!videoPlayer.isPrepared)
        {
            BeginVideoPlayback(videoPlayer.clip);
            return;
        }

        // Pause via playbackSpeed = 0 instead of VideoPlayer.Pause(). Calling
        // Play() after Pause() intermittently leaves the media clock frozen even
        // though isPlaying reports true. Freezing playbackSpeed keeps the player
        // in the Playing state so resuming (speed = 1) is always reliable.
        bool effectivelyPlaying = videoPlayer.isPlaying && videoPlayer.playbackSpeed > 0f;
        if (effectivelyPlaying)
        {
            videoPlayer.playbackSpeed = 0f;   // freeze on the current frame
            SetVideoPaused(true);
        }
        else
        {
            videoPlayer.playbackSpeed = 1f;
            if (!videoPlayer.isPlaying) videoPlayer.Play();
            SetVideoPaused(false);
        }
    }

    void OnVideoFinished(UnityEngine.Video.VideoPlayer vp)
    {
        // Show the play icon again so the player can replay.
        SetVideoPaused(true);
    }

    void SetVideoPaused(bool paused)
    {
        if (playIconGO != null) playIconGO.SetActive(paused);
    }

    void SetVideoLoading(bool loading)
    {
        if (videoLoadingText != null) videoLoadingText.gameObject.SetActive(loading);
        if (loading && playIconGO != null) playIconGO.SetActive(false);
    }

    void CloseVideoViewer()
    {
        if (videoPlayer != null) videoPlayer.Stop();
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