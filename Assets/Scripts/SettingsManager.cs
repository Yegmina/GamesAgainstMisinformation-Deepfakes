using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject mainMenuPanel;
    
    [Header("Settings Controls")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    
    private void Start()
    {
        // Initialize volume slider with current AudioListener volume
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            UpdateVolumeText(volumeSlider.value);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        
        // Initialize resolution dropdown
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            Resolution[] resolutions = Screen.resolutions;
            int currentResolutionIndex = 0;
            
            for (int i = 0; i < resolutions.Length; i++)
            {
                string option = resolutions[i].width + " x " + resolutions[i].height;
#if UNITY_2022_2_OR_NEWER
                option += " @ " + Mathf.RoundToInt((float)resolutions[i].refreshRateRatio.value) + "Hz";
#else
                option += " @ " + resolutions[i].refreshRate + "Hz";
#endif
                options.Add(option);
                
                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = i;
                }
            }
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        MouseSettingsUiBuilder.EnsureStartMenu(settingsPanel, volumeSlider, volumeValueText);

        // Ensure panel is hidden at start
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
    }

    public void OpenSettings()
    {
        MouseSettingsUiBuilder.EnsureStartMenu(settingsPanel, volumeSlider, volumeValueText);

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
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

    private void OnResolutionChanged(int index)
    {
        Resolution[] resolutions = Screen.resolutions;
        if (index >= 0 && index < resolutions.Length)
        {
            Resolution res = resolutions[index];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }
    }
}
