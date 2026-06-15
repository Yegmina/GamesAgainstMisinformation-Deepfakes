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
        gameObject.name = "GlobalCanvas"; // Ensure name is always exactly "GlobalCanvas"
        DontDestroyOnLoad(gameObject);

        // Force timer to 10 minutes (600 seconds)
        timer = 600f;

        // Bind HUD UI components
        BindUIElements();
    }

    private void Start()
    {
        // Initial UI Update
        UpdateUI();
    }

    private void Update()
    {
        // Keep cursor unlocked and visible at all times as requested
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }

        // Don't run the timer if we are in an ending scene or start game scene
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene.Contains("Ending_") || currentScene == "StartGame")
        {
            timerRunning = false;
            return;
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

    public void SetTimerRunning(bool run)
    {
        timerRunning = run;
    }

    public void SetParanoia(int val)
    {
        paranoia = Mathf.Clamp(val, 0, 100);
        UpdateUI();

        // Load ending immediately if paranoia reaches 100%
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
        points = Mathf.Max(0, val);
        UpdateUI();
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
    }

    public void UpdateUI()
    {
        // Re-bind elements if they are lost or if we re-loaded a scene with a new canvas instance
        if (timerText == null || paranoiaText == null || paranoiaFill == null || pointsText == null)
        {
            BindUIElements();
        }

        // 1. Timer Text Update
        if (timerText != null)
        {
            int m = Mathf.FloorToInt(timer / 60f);
            int s = Mathf.FloorToInt(timer % 60f);
            timerText.text = string.Format("{0:00}:{1:00}\n<size=10><color=#888888>TIME LEFT</color></size>", m, s);
            timerText.color = timer < 120f ? new Color(1f, 0.25f, 0.25f) : Color.white;
        }

        // 2. Paranoia Text & Fill Update
        if (paranoiaText != null)
        {
            paranoiaText.text = paranoia + "%\n<size=10><color=#888888>PARANOIA</color></size>";
        }

        if (paranoiaFill != null)
        {
            paranoiaFill.rectTransform.anchorMax = new Vector2(paranoia / 100f, 1f);
            
            // Linear green-to-red color interpolation for paranoia stackbar
            Color col = Color.Lerp(new Color(0.3f, 0.75f, 0.3f), new Color(0.9f, 0.25f, 0.25f), paranoia / 100f);
            paranoiaFill.color = col;
        }

        // 3. Points Text Update
        if (pointsText != null)
        {
            pointsText.text = points + "\n<size=10><color=#888888>POINTS</color></size>";
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

