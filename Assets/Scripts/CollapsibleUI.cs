using UnityEngine;
using UnityEngine.UI;

public class CollapsibleUI : MonoBehaviour
{
    public GameObject content;
    public RectTransform parentToRebuild;

    public void Toggle()
    {
        if (content != null)
        {
            content.SetActive(!content.activeSelf);
            if (parentToRebuild != null)
            {
                // Rebuild the layout for the parent to ensure the content size fitter updates correctly
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentToRebuild);
            }
        }
    }
}