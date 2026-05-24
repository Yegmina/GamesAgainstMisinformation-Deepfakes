using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CallerController : MonoBehaviour
{
    // UI элементы
    public TMP_Text callerName;
    public Image callerAvatar;
    public GameObject callerScreen;
    
    // Аватарки
    public Sprite momAvatar;
    public Sprite dadAvatar;
    public Sprite sarahAvatar;
    public Sprite brotherAvatar;
    public Sprite unknownAvatar;
    
    // ===== ОТКРЫТЬ ЗВОНКИ =====
    
    public void OpenMomCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "MOM";
        callerAvatar.sprite = momAvatar;
    }
    
    public void OpenDadCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "DAD";
        callerAvatar.sprite = dadAvatar;
    }
    
    public void OpenSarahCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "SARAH";
        callerAvatar.sprite = sarahAvatar;
    }
    
    public void OpenBrotherCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "BROTHER";
        callerAvatar.sprite = brotherAvatar;
    }
    
    public void OpenUnknownCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "UNKNOWN NUMBER";
        callerAvatar.sprite = unknownAvatar;
    }
    
    // ===== ДЕЙСТВИЯ СО ЗВОНКОМ =====
    
    public void CloseCaller()
    {
        callerScreen.SetActive(false);
    }
    
    public void AnswerCall()
    {
        Debug.Log($"📞 Ответили на звонок от {callerName.text}");
        // Здесь будет логика когда ответили
    }
    
    public void DeclineCall()
    {
        Debug.Log($"📵 Отклонили звонок от {callerName.text}");
        CloseCaller();
    }
}