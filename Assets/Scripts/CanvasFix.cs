using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasFix : MonoBehaviour
{
    void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = false;
            canvas.enabled = true;
            Canvas.ForceUpdateCanvases();
        }
    }
}
