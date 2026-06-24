using UnityEngine;

public class GuidePopup : MonoBehaviour
{
    [SerializeField] private GameObject guidePopup;
    [SerializeField] private GameObject mainMenuPanel;

    private void Start()
    {
        CloseGuide();
    }

    public void OpenGuide()
    {
        if (guidePopup != null)
        {
            guidePopup.SetActive(true);
        }
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
    }

    public void CloseGuide()
    {
        if (guidePopup != null)
        {
            guidePopup.SetActive(false);
        }
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
    }
}