using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Canvas))]
public class GlobalCanvasPersistent : MonoBehaviour
{
    private static GlobalCanvasPersistent instance;
    public static GlobalCanvasPersistent Instance => instance;

    [Header("Global Game State")]
    [SerializeField] private float timer = 600f; // 10 minutes
    [SerializeField] private bool timerRunning = true;
    [SerializeField] private int paranoia = 0;
    [SerializeField] private int points = 0;

    [Header("Horror Music")]
    [SerializeField] private AudioSource horrorMusicSource;
    [SerializeField, Range(0f, 1f)] private float horrorMusicVolume = 0.25f;
    [SerializeField] private AudioClip clip0To30;
    [SerializeField] private AudioClip clip30To60;
    [SerializeField] private AudioClip clip60To100;

    [Header("Virus Jump-Scare")]
    [SerializeField] private AudioClip virusScreamClip;
    [SerializeField] private AudioSource virusScreamSource;
    [SerializeField, Range(0f, 1f)] private float virusScreamVolume = 1f;

    [Header("HUD UI Elements")]
    private TMP_Text timerText;
    private TMP_Text paranoiaText;
    private TMP_Text pointsText;
    private Image paranoiaFill;

    public enum GlobalCallPhase
    {
        WaitingForNeighbor,
        NeighborRinging,
        WaitingForMom,
        MomRinging,
        WaitingForMicrosoft,
        MicrosoftRinging,
        Complete
    }

    [Header("Global Call Timing")]
    [SerializeField] private float delaySeconds = 60f;
    [SerializeField] private float delayBeforeMom = 150f;
    [SerializeField] private float delayBeforeMicrosoft = 150f;
    [SerializeField] private AudioClip globalIncomingRingtone;
    [SerializeField] private Sprite notificationBgSprite;

    private GlobalCallPhase callPhase = GlobalCallPhase.WaitingForNeighbor;
    private float callPhaseElapsed = 0f;
    private bool isCallRinging = false;
    private string ringingCallerName = "";
    private AudioSource ringtoneSource;

    private GameObject notificationToast;
    private TMP_Text notifTitleText;
    private TMP_Text notifBodyText;

    public GlobalCallPhase CallPhase => callPhase;
    public bool IsCallRinging => isCallRinging;
    public string RingingCallerName => ringingCallerName;

    public float Timer { get => timer; set { timer = value; UpdateUI(); } }
    public bool IsTimerRunning => timerRunning;
    public int Paranoia => paranoia;
    public int Points => points;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.name = "GlobalCanvas";
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        timer = 600f;
        BindUIElements();
    }

    private void Start()
    {
        UpdateUI();
        string currentScene = SceneManager.GetActiveScene().name;
        bool hideHud = currentScene.Contains("Ending_") || currentScene == "StartGame" || currentScene == "IntroScene";
        UpdateHorrorMusic(hideHud);
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }

        string currentScene = SceneManager.GetActiveScene().name;
        bool hideHud = currentScene.Contains("Ending_") || currentScene == "StartGame" || currentScene == "IntroScene";
        ApplyHudVisibility(!hideHud);
        UpdateHorrorMusic(hideHud);

        if (hideHud)
        {
            timerRunning = false;
            return;
        }
        else if (timer > 0f)
        {
            timerRunning = true;
        }

        if (timerRunning)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = 0f;
                timerRunning = false;
                TriggerEnding();
            }
            UpdateUI();
        }

        // Global Call Timing Update
        if (callPhase != GlobalCallPhase.Complete && !isCallRinging && !hideHud)
        {
            callPhaseElapsed += Time.deltaTime;
            float requiredDelay = GetRequiredDelayForCurrentPhase();
            if (callPhaseElapsed >= requiredDelay)
            {
                string callerName = "";
                GlobalCallPhase nextRingPhase = GlobalCallPhase.Complete;
                if (callPhase == GlobalCallPhase.WaitingForNeighbor)
                {
                    callerName = "Neighbor";
                    nextRingPhase = GlobalCallPhase.NeighborRinging;
                }
                else if (callPhase == GlobalCallPhase.WaitingForMom)
                {
                    callerName = "Mom";
                    nextRingPhase = GlobalCallPhase.MomRinging;
                }
                else if (callPhase == GlobalCallPhase.WaitingForMicrosoft)
                {
                    callerName = "Microsoft Support";
                    nextRingPhase = GlobalCallPhase.MicrosoftRinging;
                }

                if (nextRingPhase != GlobalCallPhase.Complete)
                {
                    StartRingingCall(nextRingPhase, callerName);
                }
            }
        }
    }

    private void UpdateHorrorMusic(bool hideHud)
    {
        if (horrorMusicSource == null) return;
        horrorMusicSource.volume = horrorMusicVolume;

        if (hideHud)
        {
            if (horrorMusicSource.isPlaying)
            {
                horrorMusicSource.Stop();
            }
            return;
        }

        AudioClip targetClip = null;
        if (paranoia < 30)
        {
            targetClip = clip0To30;
        }
        else if (paranoia >= 30 && paranoia < 60)
        {
            targetClip = clip30To60;
        }
        else
        {
            targetClip = clip60To100;
        }

        if (targetClip != null)
        {
            if (horrorMusicSource.clip != targetClip)
            {
                horrorMusicSource.clip = targetClip;
                horrorMusicSource.loop = true;
                horrorMusicSource.Play();
            }
            else if (!horrorMusicSource.isPlaying)
            {
                horrorMusicSource.loop = true;
                horrorMusicSource.Play();
            }
        }
        else
        {
            if (horrorMusicSource.isPlaying)
            {
                horrorMusicSource.Stop();
            }
        }
    }

    public void BindUIElements()
    {
        Transform hud = transform.Find("HUD");
        if (hud != null)
        {
            Transform timerTxtTrans = hud.Find("TimerPanel/Content/TimerText");
            if (timerTxtTrans != null) timerText = timerTxtTrans.GetComponent<TMP_Text>();

            Transform paranoiaTxtTrans = hud.Find("ParanoiaPanel/ParanoiaText");
            if (paranoiaTxtTrans != null) paranoiaText = paranoiaTxtTrans.GetComponent<TMP_Text>();

            Transform paranoiaFillTrans = hud.Find("ParanoiaPanel/ParanoiaBar/Fill");
            if (paranoiaFillTrans != null) paranoiaFill = paranoiaFillTrans.GetComponent<Image>();

            Transform pointsTxtTrans = hud.Find("PointsPanel/Content/PointsText");
            if (pointsTxtTrans != null) pointsText = pointsTxtTrans.GetComponent<TMP_Text>();
        }
    }

    private bool _hudVisible = true;
    private Transform _hudTransform;
    private Transform _missionSidebarTransform;
    private Transform _sidebarOpenButtonTransform;

    private void ApplyHudVisibility(bool visible)
    {
        if (_hudTransform == null)
        {
            _hudTransform = transform.Find("HUD");
        }
        if (_missionSidebarTransform == null)
        {
            _missionSidebarTransform = transform.Find("MissionSidebar");
        }
        if (_sidebarOpenButtonTransform == null)
        {
            _sidebarOpenButtonTransform = transform.Find("SidebarOpenButton");
        }

        if (_hudTransform != null && _hudTransform.gameObject.activeSelf != visible)
        {
            _hudTransform.gameObject.SetActive(visible);
        }

        if (_missionSidebarTransform != null && _missionSidebarTransform.gameObject.activeSelf != visible)
        {
            _missionSidebarTransform.gameObject.SetActive(visible);
        }

        if (_sidebarOpenButtonTransform != null)
        {
            if (!visible && _sidebarOpenButtonTransform.gameObject.activeSelf)
            {
                _sidebarOpenButtonTransform.gameObject.SetActive(false);
            }
        }

        _hudVisible = visible;
    }

    public void SetTimerRunning(bool run)
    {
        timerRunning = run;
    }

    public void PlayVirusScream()
    {
        if (virusScreamClip == null) return;
        AudioSource src = virusScreamSource != null ? virusScreamSource : horrorMusicSource;
        if (src != null) src.PlayOneShot(virusScreamClip, virusScreamVolume);
    }

    public void SetParanoia(int val)
    {
        paranoia = Mathf.Clamp(val, 0, 100);
        UpdateUI();

        if (paranoia >= 100)
        {
            timerRunning = false;
            Debug.Log("Paranoia reached 100%! Loading Ending_100_Paranoia scene.");
            SceneManager.LoadScene("Ending_100_Paranoia");
        }
    }

    public void AddParanoia(int val)
    {
        SetParanoia(paranoia + val);
    }

    public void SubtractParanoia(int val)
    {
        SetParanoia(paranoia - val);
    }

    public void SetPoints(int val)
    {
        if (val > points)
        {
            points = val;
            UpdateUI();
        }
    }

    public void AddPoints(int val)
    {
        SetPoints(points + val);
    }

    public void SubtractTime(int seconds)
    {
        timer = Mathf.Max(0f, timer - seconds);
        UpdateUI();
    }

    public void ResetHUD()
    {
        timer = 600f;
        timerRunning = true;
        paranoia = 0;
        points = 0;
        UpdateUI();

        if (MissionSidebarManager.Instance != null)
        {
            MissionSidebarManager.Instance.ResetMissions();
        }
    }

    public void UpdateUI()
    {
        if (timerText == null || paranoiaText == null || paranoiaFill == null || pointsText == null)
        {
            BindUIElements();
        }

        if (timerText != null)
        {
            int m = Mathf.FloorToInt(timer / 60f);
            int s = Mathf.FloorToInt(timer % 60f);
            timerText.text = string.Format("{0:00}:{1:00}\n<size=18><color=#888888>TIME LEFT</color></size>", m, s);
            timerText.color = timer < 120f ? new Color(1f, 0.25f, 0.25f) : Color.white;
        }

        if (paranoiaText != null)
        {
            paranoiaText.text = paranoia + "%\n<size=18><color=#888888>PARANOIA</color></size>";
        }

        if (paranoiaFill != null)
        {
            paranoiaFill.rectTransform.anchorMax = new Vector2(paranoia / 100f, 1f);
            Color col = Color.Lerp(new Color(0.3f, 0.75f, 0.3f), new Color(0.9f, 0.25f, 0.25f), paranoia / 100f);
            paranoiaFill.color = col;
        }

        if (pointsText != null)
        {
            pointsText.text = points + "\n<size=18><color=#888888>POINTS</color></size>";
        }
    }

    private void TriggerEnding()
    {
        string endingScene = "Ending_Under_50_Paranoia";
        if (paranoia == 0)
        {
            endingScene = "Ending_0_Paranoia";
        }
        else if (paranoia < 50)
        {
            endingScene = "Ending_Under_50_Paranoia";
        }
        else if (paranoia < 100)
        {
            endingScene = "Ending_Over_50_Paranoia";
        }
        else
        {
            endingScene = "Ending_100_Paranoia";
        }
        Debug.Log("Timer ended! Loading scene: " + endingScene);
        SceneManager.LoadScene(endingScene);
    }

    private float GetRequiredDelayForCurrentPhase()
    {
        switch (callPhase)
        {
            case GlobalCallPhase.WaitingForNeighbor:
                return delaySeconds;
            case GlobalCallPhase.WaitingForMom:
                return delayBeforeMom;
            case GlobalCallPhase.WaitingForMicrosoft:
                return delayBeforeMicrosoft;
            default:
                return float.MaxValue;
        }
    }

    public void StartRingingCall(GlobalCallPhase ringPhase, string callerName)
    {
        callPhase = ringPhase;
        isCallRinging = true;
        ringingCallerName = callerName;

        CreateNotificationToast();
        if (notificationToast != null)
        {
            if (notifBodyText != null)
            {
                notifBodyText.text = $"{callerName} is calling! Go to the phone to answer the call.";
            }
            notificationToast.SetActive(true);
        }

        PlayGlobalRingtone();
    }

    public void OnCallEnded(GlobalCallPhase nextPhase)
    {
        callPhase = nextPhase;
        callPhaseElapsed = 0f;
        StopGlobalRingtoneAndNotification();
    }

    public void PlayGlobalRingtone()
    {
        if (ringtoneSource == null)
        {
            ringtoneSource = gameObject.AddComponent<AudioSource>();
            ringtoneSource.playOnAwake = false;
            ringtoneSource.loop = true;
        }

        if (globalIncomingRingtone != null)
        {
            ringtoneSource.clip = globalIncomingRingtone;
            ringtoneSource.volume = 0.5f;
            ringtoneSource.Play();
        }
    }

    public void StopGlobalRingtoneAndNotification()
    {
        isCallRinging = false;
        if (ringtoneSource != null)
        {
            ringtoneSource.Stop();
        }
        if (notificationToast != null)
        {
            notificationToast.SetActive(false);
        }
    }

    private void CreateNotificationToast()
    {
        if (notificationToast != null) return;

        // Create main container panel
        notificationToast = new GameObject("PhoneCallNotification");
        notificationToast.transform.SetParent(this.transform, false);
        
        RectTransform rect = notificationToast.AddComponent<RectTransform>();
        // Anchor to bottom right corner
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(730f, 190f); // Much wider and taller to fit 24f font perfectly
        rect.anchoredPosition = new Vector2(-40f, 40f); // Offset left and up from bottom-right

        // Background Image
        Image bgImage = notificationToast.AddComponent<Image>();
        if (notificationBgSprite != null)
        {
            bgImage.sprite = notificationBgSprite;
            bgImage.type = Image.Type.Simple;
            bgImage.color = Color.white;
        }
        else
        {
            bgImage.color = new Color(0.08f, 0.11f, 0.18f, 0.95f); // Deep dark blue-slate
        }

        // Outline (matches the HUD style exactly)
        Outline outline = notificationToast.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.9f, 1f, 0.8f); // Neon Cyan border
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Vertical Layout for nice padding and stacking
        VerticalLayoutGroup layout = notificationToast.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(70, 45, 25, 25); // Increased padding to clear the neon glow border perfectly
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true; // Set to true so child text elements are correctly sized
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Content: Title Text
        GameObject titleGo = new GameObject("NotifTitle", typeof(RectTransform));
        titleGo.transform.SetParent(notificationToast.transform, false);
        
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.pivot = new Vector2(0f, 0.5f); // Force pivot to left-center
        
        notifTitleText = titleGo.AddComponent<TextMeshProUGUI>();
        notifTitleText.text = "📞 PHONE IS RINGING!";
        notifTitleText.fontSize = 24f; // Font size 24 as requested
        notifTitleText.color = new Color(0f, 0.9f, 1f, 1f); // Neon Cyan
        notifTitleText.fontStyle = FontStyles.Bold;
        notifTitleText.alignment = TextAlignmentOptions.Left;
        notifTitleText.margin = new Vector4(0f, 0f, 0f, 0f);

        // Content: Body Text
        GameObject bodyGo = new GameObject("NotifBody", typeof(RectTransform));
        bodyGo.transform.SetParent(notificationToast.transform, false);
        
        RectTransform bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.pivot = new Vector2(0f, 0.5f); // Force pivot to left-center
        
        notifBodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        notifBodyText.text = "Someone is calling you. Go to the phone to answer the call!";
        notifBodyText.fontSize = 24f; // Font size 24 as requested
        notifBodyText.color = Color.white;
        notifBodyText.alignment = TextAlignmentOptions.Left;
        notifBodyText.enableWordWrapping = true;
        notifBodyText.overflowMode = TextOverflowModes.Overflow;
        notifBodyText.margin = new Vector4(0f, 0f, 0f, 0f);

        notificationToast.SetActive(false);
    }
}