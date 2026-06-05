using UnityEngine;

public class GuidePopup : MonoBehaviour
{
    public GameObject guidePopup;

    public void OpenGuide()
    {
        guidePopup.SetActive(true);
    }

    public void CloseGuide()
    {
        guidePopup.SetActive(false);
    }
}