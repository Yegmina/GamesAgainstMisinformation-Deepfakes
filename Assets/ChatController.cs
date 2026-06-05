using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────
//  FAMILIAR STRANGERS - branching psychological-horror chat engine
//  Port of the HTML prototype. Two contacts (Mom = impersonator,
//  Brother = the clue). Paranoia meter, session timer, app-state,
//  suspicious photo, good ending (block) and bad ending (screamer).
//  Self-builds HUD + horror overlays + procedural audio at runtime,
//  so it "just plays" with the existing phone UI.
// ─────────────────────────────────────────────────────────────
public class ChatController : MonoBehaviour
{
    [Header("Screens")]
    public GameObject chatScreen;   // Phone1MessagesChat
    public GameObject hubScreen;    // PhoneMessages (contact list)

    [Header("Chat UI")]
    public Transform messagesContent;
    public GameObject messagePrefabMy;     // my message (right)
    public GameObject messagePrefabOther;  // their message (left)
    public GameObject optionsPanel;
    public Transform optionsContent;
    public GameObject optionButtonPrefab;  // OptionButton1.prefab
    public TMP_Text contactNameText;
    public Image contactAvatar;
    public ScrollRect chatScrollRect;

    [Header("Avatars")]
    public Sprite momAvatar;
    public Sprite brotherAvatar;

    [Header("Contact Rows (hub)")]
    public TMP_Text momPreview;
    public GameObject momBadge;
    public TMP_Text broPreview;
    public GameObject broBadge;

    [Header("Photo")]
    public Sprite photoSprite;

    [Header("Voice Note")]
    public Sprite voiceNoteSprite;   // the voice-bubble PNG (looks like a voice note)
    public AudioClip voiceNoteClip;  // replaceable audio - drop any clip here in the Inspector

    // ── bubble styling ──
    static readonly Color themBubble = new Color(0.118f, 0.118f, 0.180f, 1f); // #1e1e2e
    static readonly Color meBubble   = new Color(0.102f, 0.227f, 0.431f, 1f); // #1a3a6e
    static readonly Color themText   = new Color(0.816f, 0.816f, 0.910f, 1f); // #d0d0e8
    static readonly Color meText     = new Color(0.784f, 0.863f, 1.000f, 1f); // #c8dcff
    const float maxBubbleFrac = 0.78f;   // bubbles cap at ~78% of chat width
    Sprite bubbleSprite;                 // rounded sprite reused from the prefab
    RectTransform messagesRT;

    // ── runtime-built UI ──
    Canvas canvas;
    TMP_FontAsset font;
    TMP_Text timerText, paranoiaText, stateText;
    Image paranoiaFill;
    Image flashOverlay;
    GameObject screamerOverlay;
    RectTransform shakeTarget;
    Vector2 shakeHome;

    // ── audio ──
    AudioSource audioSrc;
    AudioClip chimeClip, screamerClip;

    // ── state ──
    int paranoia = 0;
    float timer = 900f;
    bool timerRunning = true;
    bool ended = false;
    bool locked = false;
    string currentChat = null;
    bool momStarted = false;
    bool broStarted = false;
    bool broWarned = false;

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

        SetParanoia(0);
        SetAppState("ACTIVE");
        UpdateTimerLabel();
    }

    void Update()
    {
        if (timerRunning && !ended)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = 0f;
                timerRunning = false;
                UpdateTimerLabel();
                if (currentChat == "mom" && !ended) TriggerBadEnding(true);
            }
            UpdateTimerLabel();
        }
    }

    // ════════════════════════════════════════ NAVIGATION

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
        if (optionsPanel != null) optionsPanel.SetActive(true);

        AddMessage(false, "Alex, defrost the pizza if you want, it's in the freezer. We left.");
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
        if (optionsPanel != null) optionsPanel.SetActive(true);

        AddMessage(false, "Left my gym bag at your place. Don't touch my protein bar, bro");
        if (!broStarted)
        {
            broStarted = true;
            StartCoroutine(BroIntro());
        }
    }

    public void CloseChat()
    {
        if (locked) return;
        chatScreen.SetActive(false);
        if (hubScreen != null) hubScreen.SetActive(true);
        currentChat = null;
    }

    // ════════════════════════════════════════ MOM FLOW (impersonator)

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
        SetParanoia(paranoia + 8);
        AddMessage(true, "Sure, hold on.");
        StartCoroutine(MomPressure());
    }

    IEnumerator MomPressure()
    {
        yield return Wait(0.8f);
        AddMessage(false, "Faster, Alex! Dad is losing his mind! Just type it!");
        yield return Wait(0.3f);
        ShowChoices(
            ("[ SEND ADDRESS ]", 1, () => TriggerBadEnding(false)),
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
        SetParanoia(paranoia + 15);
        yield return Wait(0.6f);
        AddMessage(false, "See? It's me. Now send it.");
        yield return Wait(0.3f);
        ShowChoices(
            ("[ TRUST & SEND ADDRESS ]", 1, () => TriggerBadEnding(false)),
            ("[ BLOCK CONTACT ]", 2, TriggerBlock)
        );
    }

    // ════════════════════════════════════════ BROTHER FLOW (the clue)

    IEnumerator BroIntro()
    {
        yield return Wait(0.6f);
        AddMessage(false, "hold on, listen to this");
        yield return Wait(0.5f);
        AddVoice(false);                       // Brother sends a voice note (tap to play)
        yield return Wait(0.7f);
        AddMessage(false, "yo. did mom text you just now??");
        yield return Wait(0.5f);
        ShowChoices(
            ("\"Yeah, she wants the home address?\"", 0, BroChoiceA),
            ("\"No. Why?\"", 0, BroChoiceB)
        );
    }

    void BroChoiceA()
    {
        ClearChoices();
        AddMessage(true, "Yeah, she wants the home address?");
        StartCoroutine(BroReveal());
    }

    void BroChoiceB()
    {
        ClearChoices();
        AddMessage(true, "No. Why?");
        StartCoroutine(BroReveal());
    }

    IEnumerator BroReveal()
    {
        yield return Wait(0.9f);
        AddMessage(false, "her phone got stolen at the gym tonight.");
        yield return Wait(0.9f);
        AddMessage(false, "someone's been texting EVERYONE from her account. do NOT send them anything.");
        broWarned = true;
        SetParanoia(paranoia + 10);
        yield return Wait(0.3f);
        ShowChoices(
            ("\"Oh my god. Okay.\"", 2, BroAck)
        );
    }

    void BroAck()
    {
        ClearChoices();
        AddMessage(true, "Oh my god. Okay.");
        StartCoroutine(BroEnd());
    }

    IEnumerator BroEnd()
    {
        yield return Wait(0.8f);
        AddMessage(false, "block that number. i'll call the real mom. stay safe bro.");
        if (broPreview != null) broPreview.text = "block that number...";
    }

    // ════════════════════════════════════════ ENDINGS

    void TriggerBlock()
    {
        ClearChoices();
        AddSystem("Contact BLOCKED at 4:18 AM");
        SetParanoia(Mathf.Max(0, paranoia - 40));
        SetAppState("ACTIVE");
        StartCoroutine(BlockSeq());
    }

    IEnumerator BlockSeq()
    {
        yield return Wait(1.0f);
        AddSystem(broWarned
            ? "You were right. That wasn't Mom. Your address is safe."
            : "Something felt wrong. You trusted your gut - your address is safe.");
        timerRunning = false;
        yield return Wait(0.5f);
        ShowChoices(("[ Replay ]", 0, ResetPrototype));
    }

    void TriggerBadEnding(bool timeout)
    {
        if (ended) return;
        ended = true;
        locked = true;
        timerRunning = false;
        ClearChoices();
        if (!timeout) AddMessage(true, "[ home address sent ]");
        StartCoroutine(BadEndingSeq());
    }

    IEnumerator BadEndingSeq()
    {
        string[] spam = {
            "GOT IT.",
            "address received.",
            "we know where you live now, Alex.",
            "we can see your front door.",
            "don't call the police.",
            "we're already outside.",
            "SENDING LOCATION DATA...",
            "CONNECTION HIJACKED",
            "DEVICE COMPROMISED"
        };
        SetParanoia(100);
        SetAppState("CORRUPTED");
        StartCoroutine(ShakeRoutine(6f, 6f));

        foreach (var s in spam)
        {
            AddSpam(s);
            FlashRed();
            yield return Wait(0.35f);
        }

        yield return Wait(0.4f);
        ShowScreamer();
        PlayScreamer();
        StartCoroutine(ShakeRoutine(2.5f, 14f));

        yield return Wait(3.0f);
        // leave the screamer up; allow replay
        var t = screamerOverlay.transform.Find("Replay");
        if (t != null) t.gameObject.SetActive(true);
    }

    public void ResetPrototype()
    {
        StopAllCoroutines();
        paranoia = 0; timer = 900f; timerRunning = true; ended = false; locked = false;
        momStarted = false; broStarted = false; broWarned = false; currentChat = null;
        if (screamerOverlay != null) screamerOverlay.SetActive(false);
        if (flashOverlay != null) { var c = flashOverlay.color; c.a = 0f; flashOverlay.color = c; }
        if (shakeTarget != null) shakeTarget.anchoredPosition = shakeHome;
        SetParanoia(0);
        SetAppState("ACTIVE");
        UpdateTimerLabel();
        if (momPreview != null) momPreview.text = "Are you home?";
        if (broPreview != null) broPreview.text = "Left my gym bag";
        if (momBadge != null) momBadge.SetActive(true);
        if (broBadge != null) broBadge.SetActive(true);
        ClearMessages();
        ClearChoices();
        chatScreen.SetActive(false);
        if (hubScreen != null) hubScreen.SetActive(true);
    }

    // ════════════════════════════════════════ MESSAGE HELPERS

    // Configure the Scroll View Content so message rows stack vertically,
    // never overlap, and each row spans the full chat width.
    void ConfigureChatLayout()
    {
        messagesRT = messagesContent as RectTransform;
        if (messagesContent == null) return;

        var vlg = messagesContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = messagesContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;   // each row = full content width
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        var csf = messagesContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = messagesContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Reuse the rounded bubble sprite from the prefab so bubbles keep their look.
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

    // A full-width row that aligns its single child bubble left (them) or right (me).
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
        return Mathf.Max(80f, w * maxBubbleFrac - 28f); // minus horizontal padding
    }

    void AddBubble(bool isMe, string text, Color bubbleCol, Color textCol, FontStyles style = FontStyles.Normal)
    {
        if (messagesContent == null) return;
        var row = BuildRow(isMe);

        GameObject bubble = new GameObject("Bubble", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        bubble.transform.SetParent(row, false);
        var img = bubble.GetComponent<UnityEngine.UI.Image>();
        img.color = bubbleCol;
        if (bubbleSprite != null) { img.sprite = bubbleSprite; img.type = UnityEngine.UI.Image.Type.Sliced; }
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

        // Cap width so long messages wrap at ~78% of chat width, short ones shrink to fit.
        var le = t.AddComponent<LayoutElement>();
        float natural = tmp.GetPreferredValues(text).x;
        le.preferredWidth = Mathf.Min(natural, GetMaxTextWidth());

        PlayChime();
        ScrollToBottom();
    }

    void AddMessage(bool isMe, string text)
    {
        if (isMe) AddBubble(true, text, meBubble, meText);
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

        GameObject holder = new GameObject("Photo", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        holder.transform.SetParent(row, false);
        var img = holder.GetComponent<UnityEngine.UI.Image>();
        var le = holder.AddComponent<LayoutElement>();

        float w = 220f, h = 165f;
        if (photoSprite != null)
        {
            img.sprite = photoSprite;
            img.color = Color.white;
            img.preserveAspect = true;
            float ar = photoSprite.rect.width > 0 ? photoSprite.rect.height / photoSprite.rect.width : 0.75f;
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

    // Voice note bubble: shows the voice-note PNG and plays a (replaceable) clip on click.
    void AddVoice(bool isMe)
    {
        if (messagesContent == null) return;
        var row = BuildRow(isMe);

        GameObject holder = new GameObject("VoiceNote", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        holder.transform.SetParent(row, false);
        var img = holder.GetComponent<UnityEngine.UI.Image>();
        var btn = holder.GetComponent<UnityEngine.UI.Button>();
        var le = holder.AddComponent<LayoutElement>();

        float w = 230f, h = 60f;
        if (voiceNoteSprite != null)
        {
            img.sprite = voiceNoteSprite;
            img.color = Color.white;
            img.preserveAspect = true;
            if (voiceNoteSprite.rect.width > 0)
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
        if (audioSrc == null) return;
        if (voiceNoteClip != null) audioSrc.PlayOneShot(voiceNoteClip, 1f);
        else PlayChime();
    }

    void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        if (messagesRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(messagesRT);
        if (chatScrollRect != null) chatScrollRect.verticalNormalizedPosition = 0f;
    }

    // ════════════════════════════════════════ CHOICES

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
                // Buttons are white, so keep text dark/bold and readable.
                if (c.style == 1) tmp.color = new Color(0.62f, 0.0f, 0.0f);     // danger - dark red
                else if (c.style == 2) tmp.color = new Color(0.0f, 0.42f, 0.18f); // safe - dark green
                else tmp.color = Color.black;                                    // normal - black
            }
            var btn = b.GetComponent<Button>();
            if (btn != null)
            {
                var act = c.act;
                btn.onClick.AddListener(() => { if (!locked) act(); });
            }
        }
    }

    void ClearChoices()
    {
        if (optionsContent == null) return;
        foreach (Transform child in optionsContent) Destroy(child.gameObject);
    }

    // ════════════════════════════════════════ STATE / HUD

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

    // ════════════════════════════════════════ EFFECTS

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
        if (screamerOverlay != null) screamerOverlay.SetActive(true);
    }

    // ════════════════════════════════════════ AUDIO

    void SetupAudio()
    {
        audioSrc = gameObject.GetComponent<AudioSource>();
        if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;

        int sr = 44100;

        // chime: short decaying sine
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

        // screamer: harsh noise + rising sawtooth
        int sn = (int)(sr * 1.6f);
        var scr = new float[sn];
        float phase = 0f;
        for (int i = 0; i < sn; i++)
        {
            float t = i / (float)sr;
            float freq = Mathf.Lerp(90f, 1600f, t / 1.6f);
            phase += (2f * Mathf.PI * freq) / sr;
            float saw = (phase % (2f * Mathf.PI)) / Mathf.PI - 1f;
            float noise = Random.Range(-1f, 1f);
            float env = t < 0.02f ? t / 0.02f : Mathf.Clamp01(1f - (t - 0.02f) / 1.5f);
            scr[i] = (noise * 0.6f + saw * 0.4f) * env * 0.85f;
        }
        screamerClip = AudioClip.Create("screamer", sn, 1, sr, false);
        screamerClip.SetData(scr, 0);
    }

    void PlayChime() { if (audioSrc != null && chimeClip != null) audioSrc.PlayOneShot(chimeClip, 0.5f); }
    void PlayScreamer() { if (audioSrc != null && screamerClip != null) audioSrc.PlayOneShot(screamerClip, 1f); }

    // ════════════════════════════════════════ RUNTIME UI BUILD

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

        // paranoia bar
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

        // red flash full-screen
        GameObject flash = NewUI("RedFlash", canvas.transform);
        flashOverlay = flash.AddComponent<Image>();
        flashOverlay.color = new Color(1f, 0f, 0f, 0f);
        flashOverlay.raycastTarget = false;
        FullStretch(flash.GetComponent<RectTransform>());

        // shake target = phone chat screen
        if (chatScreen != null) { shakeTarget = chatScreen.GetComponent<RectTransform>(); shakeHome = shakeTarget.anchoredPosition; }

        // screamer overlay
        screamerOverlay = NewUI("Screamer", canvas.transform);
        var simg = screamerOverlay.AddComponent<Image>();
        simg.color = new Color(0f, 0f, 0f, 0.96f);
        FullStretch(screamerOverlay.GetComponent<RectTransform>());

        var face = MakeText(screamerOverlay.transform, "Face", ">_<", 120, TextAlignmentOptions.Center);
        face.color = new Color(1f, 0.05f, 0.05f);
        Anchor(face.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 0.85f), Vector2.zero, Vector2.zero);

        var msg = MakeText(screamerOverlay.transform, "Msg",
            "SIGNAL CORRUPTED\nDO YOU BELIEVE ME NOW, ALEX?\nCONNECTION TERMINATED", 22, TextAlignmentOptions.Center);
        msg.color = new Color(0.61f, 0f, 1f);
        Anchor(msg.rectTransform, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.45f), Vector2.zero, Vector2.zero);

        GameObject replay = NewUI("Replay", screamerOverlay.transform);
        var ri = replay.AddComponent<Image>();
        ri.color = new Color(0.2f, 0.0f, 0.0f, 0.9f);
        Anchor(replay.GetComponent<RectTransform>(), new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.14f), Vector2.zero, Vector2.zero);
        var rbtn = replay.AddComponent<Button>();
        rbtn.onClick.AddListener(ResetPrototype);
        var rtext = MakeText(replay.transform, "T", "REPLAY", 20, TextAlignmentOptions.Center);
        rtext.color = new Color(1f, 0.4f, 0.4f);
        FullStretch(rtext.rectTransform);

        screamerOverlay.SetActive(false);
    }

    // ── tiny UI factory ──
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