using UnityEngine;
using UnityEngine.UI;

public class SocialMediaController : MonoBehaviour
{
    public ScrollRect scrollRect;
    
    // Скролл вверх
    public void ScrollUp()
    {
        scrollRect.verticalNormalizedPosition += 0.1f;
    }
    
    // Скролл вниз
    public void ScrollDown()
    {
        scrollRect.verticalNormalizedPosition -= 0.1f;
    }
    
    // Опционально: скролл наверх одним нажатием
    public void ScrollToTop()
    {
        scrollRect.verticalNormalizedPosition = 1f;
    }
    
    // Опционально: скролл вниз одним нажатием
    public void ScrollToBottom()
    {
        scrollRect.verticalNormalizedPosition = 0f;
    }
}