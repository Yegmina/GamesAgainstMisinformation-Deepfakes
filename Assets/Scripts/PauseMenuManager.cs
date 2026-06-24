using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class PauseMenuManager : MonoBehaviour
{
    private static PauseMenuManager instance;
    public static PauseMenuManager Instance => instance;

    [Header("UI Panels")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject mainPausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings Controls")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;

    private bool isPaused = false;
    private PlayerController playerController;
    private bool wasControllerEnabled = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Ensure pause menu is closed at start
        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(false);
        }
        if (mainPausePanel != null)
        {
            mainPausePanel.SetActive(true);
        }
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // Initialize volume slider
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            UpdateVolumeText(volumeSlider.value);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
    }

    private void Update()
    {
        // Handle ESC key press globally (using KeyCode.Escape for bulletproof reliability)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Only allow pausing if we are not in the main menu or intro scenes
            if (SceneManager.GetActiveScene().name == "StartGame" || SceneManager.GetActiveScene().name == "IntroScene")
            {
                return;
            }

            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(true);
        }
        if (mainPausePanel != null)
        {
            mainPausePanel.SetActive(true);
        }
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // Find and disable player controls to prevent camera rotation while paused
        playerController = Object.FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            wasControllerEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        // Unlock and show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(false);
        }

        // Restore player controls
        if (playerController != null && wasControllerEnabled)
        {
            playerController.enabled = true;
        }

        // Restore correct cursor state depending on whether player is walking or interacting
        if (playerController != null && playerController.enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void OpenSettings()
    {
        if (mainPausePanel != null)
        {
            mainPausePanel.SetActive(false);
        }
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        if (mainPausePanel != null)
        {
            mainPausePanel.SetActive(true);
        }
    }

    public void QuitToMainMenu()
    {
        // Unpause time
        Time.timeScale = 1f;
        isPaused = false;

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(false);
        }

        // Reset Cursor state for main menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Load StartGame scene
        SceneManager.LoadScene("StartGame");
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        UpdateVolumeText(value);
    }

    private void UpdateVolumeText(float value)
    {
        if (volumeValueText != null)
        {
            volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
