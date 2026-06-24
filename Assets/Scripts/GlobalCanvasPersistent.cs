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
        bool hideHud = currentScene.Contains("Ending_") || currentScene == "StartGame";
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
        bool hideHud = currentScene.Contains("Ending_") || currentScene == "StartGame";
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
        if (_hudTransform == null) return;

        if (_hudVisible != visible || _hudTransform.gameObject.activeSelf != visible)
        {
            _hudVisible = visible;
            _hudTransform.gameObject.SetActive(visible);

            if (_missionSidebarTransform == null)
            {
                _missionSidebarTransform = transform.Find("MissionSidebar");
            }
            if (_sidebarOpenButtonTransform == null)
            {
                _sidebarOpenButtonTransform = transform.Find("SidebarOpenButton");
            }

            if (_missionSidebarTransform != null)
            {
                _missionSidebarTransform.gameObject.SetActive(visible);
            }

            if (_sidebarOpenButtonTransform != null)
            {
                _sidebarOpenButtonTransform.gameObject.SetActive(false);
            }
        }
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
            timerText.text = string.Format("{0:00}:{1:00}\n<size=13><color=#00E5FF><b>TIME LEFT</b></color></size>", m, s);
            timerText.color = timer < 120f ? new Color(1f, 0.25f, 0.25f) : Color.white;
        }

        if (paranoiaText != null)
        {
            paranoiaText.text = paranoia + "%\n<size=13><color=#00E5FF><b>PARANOIA</b></color></size>";
        }

        if (paranoiaFill != null)
        {
            paranoiaFill.rectTransform.anchorMax = new Vector2(paranoia / 100f, 1f);
            Color col = Color.Lerp(new Color(0.3f, 0.75f, 0.3f), new Color(0.9f, 0.25f, 0.25f), paranoia / 100f);
            paranoiaFill.color = col;
        }

        if (pointsText != null)
        {
            pointsText.text = points + "\n<size=13><color=#00E5FF><b>POINTS</b></color></size>";
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
}