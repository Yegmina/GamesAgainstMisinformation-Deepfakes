using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionSidebarManager : MonoBehaviour
{
    [System.Serializable]
    public class Mission
    {
        public string title;
        public int currentProgress;
        public int targetProgress;
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

    private Mission[][] missionSets;
    private Mission[] currentMissionSet;

    private void Start()
    {
        missionSidebar.SetActive(true);
        sidebarOpenButton.SetActive(false);

        CreateMissionSets();
        LoadRandomMissionSet();
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
        missionSets = new Mission[][]
        {
            new Mission[]
            {
                new Mission
                {
                    title = "Detect Fake News Articles",
                    currentProgress = 0,
                    targetProgress = 3
                },

                new Mission
                {
                    title = "Detect Phishing Email",
                    currentProgress = 0,
                    targetProgress = 1
                },

                new Mission
                {
                    title = "Detect Deepfake Call",
                    currentProgress = 0,
                    targetProgress = 1
                }
            },

            new Mission[]
            {
                new Mission
                {
                    title = "Identify Real News Articles",
                    currentProgress = 0,
                    targetProgress = 5
                },

                new Mission
                {
                    title = "Detect Deepfake Calls",
                    currentProgress = 0,
                    targetProgress = 2
                },

                new Mission
                {
                    title = "Detect Phishing Conversation",
                    currentProgress = 0,
                    targetProgress = 1
                }
            },

            new Mission[]
            {
                new Mission
                {
                    title = "Identify Legitimate Emails",
                    currentProgress = 0,
                    targetProgress = 2
                },

                new Mission
                {
                    title = "Identify Real News Articles",
                    currentProgress = 0,
                    targetProgress = 3
                },

                new Mission
                {
                    title = "Detect Fake News Articles",
                    currentProgress = 0,
                    targetProgress = 3
                }
            }
        };
    }

    private void LoadRandomMissionSet()
    {
        int randomIndex = Random.Range(0, missionSets.Length);

        currentMissionSet = missionSets[randomIndex];

        UpdateMissionUI();
    }

    private void UpdateMissionUI()
    {
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
    }

    public void AddProgress(int missionIndex)
    {
        if (missionIndex < 0 || missionIndex > 2)
            return;

        Mission mission = currentMissionSet[missionIndex];

        if (mission.currentProgress < mission.targetProgress)
        {
            mission.currentProgress++;
            UpdateMissionUI();
        }
    }

    public string GetMissionTitle(int missionIndex)
    {
        if (currentMissionSet == null || missionIndex < 0 || missionIndex > 2)
            return null;

        return currentMissionSet[missionIndex].title;
    }
}