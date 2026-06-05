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

    [Header("Contact Rows (hub)")]
    public TMP_Text momPreview;
    public GameObject momBadge;
    public TMP_Text broPreview;
    public GameObject broBadge;
    public TMP_Text unknownPreview;
    public GameObject unknownBadge;
    public TMP_Text providerPreview;
    public GameObject providerBadge;

    [Header("Photo")]
    public Sprite photoSprite;
    public Sprite screamerPhotoSprite;

    [Header("Voice Note")]
    public Sprite voiceNoteSprite;   
    public AudioClip voiceNoteClip;  
    public AudioClip screamerClip;
    public AudioClip momBadEndingClip;
    public AudioClip virusSoundClip;

    static readonly Color themBubble = new Color(0.118f, 0.118f, 0.180f, 1f);
    static readonly Color meBubble   = new Color(0.102f, 0.227f, 0.431f, 1f);
    static readonly Color themText   = new Color(0.816f, 0.816f, 0.910f, 1f);
    static readonly Color meText     = new Color(0.784f, 0.863f, 1.000f, 1f);
    const float maxBubbleFrac = 0.78f;   
    Sprite bubbleSprite;                 
    RectTransform messagesRT;

    Canvas canvas;
    TMP_FontAsset font;
    TMP_Text timerText, paranoiaText, stateText;
    Image paranoiaFill;
    Image flashOverlay;
    GameObject screamerOverlay;
    RectTransform shakeTarget;
    Vector2 shakeHome;

    AudioSource audioSrc;
    AudioClip chimeClip;

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

    void Start()
    {
        canvas = chatScreen != null ? chatScreen.GetComponentInParent<Canvas>(true) : FindFirstObjectByType<Canvas>();
        if (contactNameText != null) font = contactNameText.font;

        SetupAudio();
        BuildHud();
        BuildOverlays();
        ConfigureChatLayout();

        if (momPreview != null) momPreview.text = "Are you home?";
        if (broPreview != null) broPreview.text = "Left my gym bag";
        if (unknownPreview != null) unknownPreview.text = "Unknown number";
        if (providerPreview != null) providerPreview.text = "⚠ Your connection is unstable...";

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
        if (contactNameText != null) contactNameText.text = "Mom";
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
        if (contactNameText != null) contactNameText.text = "Brother";
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
            contactNameText.fontSize = 16;
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
            contactNameText.fontSize = 16;
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

    void AddLinkMessage()
    {
        if (messagesContent == null) return;
        var row = BuildRow(false);
        
        GameObject linkObj = new GameObject("LinkMessage", typeof(RectTransform), typeof(Image), typeof(Button));
        linkObj.transform.SetParent(row, false);
        var img = linkObj.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.25f, 1f);
        img.sprite = bubbleSprite;
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
        
        if ((currentChat == "mom" && !momFinished) || (currentChat == "bro" && !broFinished))
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
        paranoia = 0; timer = 900f; timerRunning = true; ended = false; locked = false;
        momFinished = false; broFinished = false; broWarned = false; currentChat = null;
        momStarted = false; broStarted = false; broSecondVoiceNoteTriggered = false;
        unknownRead = false; providerFinished = false; providerLinkClicked = false;
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
        if (momBadge != null) momBadge.SetActive(true);
        if (broBadge != null) broBadge.SetActive(true);
        if (unknownBadge != null) unknownBadge.SetActive(true);
        if (providerBadge != null) providerBadge.SetActive(true);
        ClearMessages();
        ClearChoices();
        chatScreen.SetActive(false);
        if (hubScreen != null) hubScreen.SetActive(true);
    }

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

        var vlg = messagesContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = messagesContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;   
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        var csf = messagesContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = messagesContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        if (messagePrefabOther != null)
        {
            var pi = messagePrefabOther.GetComponentInChildren<Image>(true);
            if (pi != null) bubbleSprite = pi.sprite;
        }
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
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = isMe ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        return row.GetComponent<RectTransform>();
    }

    float GetMaxTextWidth()
    {
        float w = 360f;
        if (messagesRT != null && messagesRT.rect.width > 1f) w = messagesRT.rect.width;
        return Mathf.Max(80f, w * maxBubbleFrac - 28f);
    }

    void AddBubble(bool isMe, string text, Color bubbleCol, Color textCol, FontStyles style = FontStyles.Normal)
    {
        if (messagesContent == null) return;
        var row = BuildRow(isMe);

        GameObject bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        bubble.transform.SetParent(row, false);
        var img = bubble.GetComponent<Image>();
        img.color = bubbleCol;
        if (bubbleSprite != null) { img.sprite = bubbleSprite; img.type = Image.Type.Sliced; }
        var bvlg = bubble.AddComponent<VerticalLayoutGroup>();
        bvlg.childControlWidth = true;
        bvlg.childControlHeight = true;
        bvlg.childForceExpandWidth = false;
        bvlg.childForceExpandHeight = false;
        bvlg.padding = new RectOffset(14, 14, 9, 9);

        GameObject t = new GameObject("Text", typeof(RectTransform));
        t.transform.SetParent(bubble.transform, false);
        var tmp = t.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = 18;
        tmp.color = textCol;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        tmp.text = text;

        var le = t.AddComponent<LayoutElement>();
        float natural = tmp.GetPreferredValues(text).x;
        le.preferredWidth = Mathf.Min(natural, GetMaxTextWidth());

        PlayChime();
        ScrollToBottom();
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

        PlayChime();
        ScrollToBottom();
    }

    void AddVoice(bool isMe)
    {
        if (messagesContent == null) return;
        var row = BuildRow(isMe);

        GameObject holder = new GameObject("VoiceNote", typeof(RectTransform), typeof(Image), typeof(Button));
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
            img.color = new Color(0.10f, 0.10f, 0.16f, 1f);
        }
        le.preferredWidth = w;
        le.preferredHeight = h;

        btn.targetGraphic = img;
        btn.onClick.AddListener(PlayVoiceNote);

        PlayChime();
        ScrollToBottom();
    }

    void PlayVoiceNote()
    {
        if (audioSrc == null || voiceNoteClip == null) return;
        audioSrc.PlayOneShot(voiceNoteClip, 1f);
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
                if (c.style == 1) tmp.color = new Color(0.62f, 0.0f, 0.0f);
                else if (c.style == 2) tmp.color = new Color(0.0f, 0.42f, 0.18f);
                else tmp.color = Color.black;
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
            // УБИРАЕМ ЛЮБУЮ КАРТИНКУ, ЧТОБЫ ПОКАЗЫВАТЬ ТОЛЬКО ТЕКСТ
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