using UnityEngine;
using UnityEngine.UI;

public class BrowserController : MonoBehaviour
{
    public ScrollRect scrollRect;
    
    // Для скролла кнопками (опционально)
    public void ScrollUp()
    {
        scrollRect.verticalNormalizedPosition += 0.1f;
    }
    
    public void ScrollDown()
    {
        scrollRect.verticalNormalizedPosition -= 0.1f;
    }
}