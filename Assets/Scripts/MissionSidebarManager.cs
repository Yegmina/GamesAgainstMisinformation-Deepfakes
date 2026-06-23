using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionSidebarManager : MonoBehaviour
{
    private static MissionSidebarManager instance;
    public static MissionSidebarManager Instance => instance;

    [System.Serializable]
    public class Mission
    {
        public string title;
        public int currentProgress;
        public int targetProgress;
        public bool pointsAwarded;
    }

    public GameObject missionSidebar;
    public GameObject sidebarOpenButton;

    public TMP_Text mission1Text;
    public TMP_Text mission2Text;
    public TMP_Text mission3Text;

    public TMP_Text mission1Progress;
    public TMP_Text mission2Progress;
    public TMP_Text mission3Progress;

    public Slider mission1Slider;
    public Slider mission2Slider;
    public Slider mission3Slider;

    private Mission[] currentMissionSet;

    private Color originalColor1 = Color.white;
    private Color originalColor2 = Color.white;
    private Color originalColor3 = Color.white;

    private void Awake()
    {
        // If there is already a persistent GlobalCanvas and it is not our root, we are a duplicate and will be destroyed.
        var persistentCanvas = GlobalCanvasPersistent.Instance;
        if (persistentCanvas != null && persistentCanvas.gameObject != transform.root.gameObject)
        {
            return;
        }
        instance = this;
    }

    private void Start()
    {
        missionSidebar.SetActive(true);
        sidebarOpenButton.SetActive(false);

        // Capture original fill colors of sliders
        if (mission1Slider != null && mission1Slider.fillRect != null)
        {
            var img = mission1Slider.fillRect.GetComponent<Image>();
            if (img != null) originalColor1 = img.color;
        }
        if (mission2Slider != null && mission2Slider.fillRect != null)
        {
            var img = mission2Slider.fillRect.GetComponent<Image>();
            if (img != null) originalColor2 = img.color;
        }
        if (mission3Slider != null && mission3Slider.fillRect != null)
        {
            var img = mission3Slider.fillRect.GetComponent<Image>();
            if (img != null) originalColor3 = img.color;
        }

        CreateMissionSets();
        UpdateMissionUI();
    }

    public void CollapseSidebar()
    {
        missionSidebar.SetActive(false);
        sidebarOpenButton.SetActive(true);
    }

    public void OpenSidebar()
    {
        missionSidebar.SetActive(true);
        sidebarOpenButton.SetActive(false);
    }

    private void CreateMissionSets()
    {
        currentMissionSet = new Mission[]
        {
            new Mission
            {
                title = "Publish 2 real news on desktop",
                currentProgress = 0,
                targetProgress = 2,
                pointsAwarded = false
            },
            new Mission
            {
                title = "Receive 1 call on phone",
                currentProgress = 0,
                targetProgress = 1,
                pointsAwarded = false
            },
            new Mission
            {
                title = "Identify 1 phishing message on phone",
                currentProgress = 0,
                targetProgress = 1,
                pointsAwarded = false
            }
        };
    }

    private void UpdateMissionUI()
    {
        if (currentMissionSet == null || currentMissionSet.Length < 3) return;

        mission1Text.text = currentMissionSet[0].title;
        mission2Text.text = currentMissionSet[1].title;
        mission3Text.text = currentMissionSet[2].title;

        mission1Progress.text =
            currentMissionSet[0].currentProgress + "/" +
            currentMissionSet[0].targetProgress;

        mission2Progress.text =
            currentMissionSet[1].currentProgress + "/" +
            currentMissionSet[1].targetProgress;

        mission3Progress.text =
            currentMissionSet[2].currentProgress + "/" +
            currentMissionSet[2].targetProgress;

        mission1Slider.maxValue = currentMissionSet[0].targetProgress;
        mission1Slider.value = currentMissionSet[0].currentProgress;

        mission2Slider.maxValue = currentMissionSet[1].targetProgress;
        mission2Slider.value = currentMissionSet[1].currentProgress;

        mission3Slider.maxValue = currentMissionSet[2].targetProgress;
        mission3Slider.value = currentMissionSet[2].currentProgress;

        // Apply completed color (green) or progress color (blue)
        SetSliderFillColor(mission1Slider, currentMissionSet[0].currentProgress >= currentMissionSet[0].targetProgress, originalColor1);
        SetSliderFillColor(mission2Slider, currentMissionSet[1].currentProgress >= currentMissionSet[1].targetProgress, originalColor2);
        SetSliderFillColor(mission3Slider, currentMissionSet[2].currentProgress >= currentMissionSet[2].targetProgress, originalColor3);
    }

    private void SetSliderFillColor(Slider slider, bool isCompleted, Color originalColor)
    {
        if (slider == null || slider.fillRect == null) return;
        var img = slider.fillRect.GetComponent<Image>();
        if (img != null)
        {
            // Always use a vibrant green for the progress bar
            img.color = new Color(0.18f, 0.8f, 0.44f, 1f);
        }
    }

    public void ResetMissions()
    {
        CreateMissionSets();
        UpdateMissionUI();
        OpenSidebar();
    }

    public void AddProgress(int missionIndex)
    {
        if (currentMissionSet == null || missionIndex < 0 || missionIndex >= currentMissionSet.Length)
            return;

        Mission mission = currentMissionSet[missionIndex];

        if (mission.currentProgress < mission.targetProgress)
        {
            mission.currentProgress++;

            if (mission.currentProgress >= mission.targetProgress && !mission.pointsAwarded)
            {
                mission.pointsAwarded = true;
                if (GlobalCanvasPersistent.Instance != null)
                {
                    GlobalCanvasPersistent.Instance.AddPoints(100);
                }
            }

            UpdateMissionUI();
        }
    }

    public string GetMissionTitle(int missionIndex)
    {
        if (currentMissionSet == null || missionIndex < 0 || missionIndex >= currentMissionSet.Length)
            return null;

        return currentMissionSet[missionIndex].title;
    }
}