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
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip outgoingRingingClip;

    private void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    private void PlayOutgoingRinging()
    {
        if (audioSource != null && outgoingRingingClip != null)
        {
            audioSource.clip = outgoingRingingClip;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    public void StopOutgoingRinging()
    {
        if (audioSource != null && audioSource.clip == outgoingRingingClip)
        {
            audioSource.Stop();
        }
    }

    // ===== ОТКРЫТЬ ЗВОНКИ =====
    
    public void OpenMomCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "MOM";
        callerAvatar.sprite = momAvatar;
        PlayOutgoingRinging();
    }
    
    public void OpenDadCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "DAD";
        callerAvatar.sprite = dadAvatar;
        PlayOutgoingRinging();
    }
    
    public void OpenSarahCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "SARAH";
        callerAvatar.sprite = sarahAvatar;
        PlayOutgoingRinging();
    }
    
    public void OpenBrotherCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "BROTHER";
        callerAvatar.sprite = brotherAvatar;
        PlayOutgoingRinging();
    }
    
    public void OpenUnknownCall()
    {
        callerScreen.SetActive(true);
        callerName.text = "UNKNOWN NUMBER";
        callerAvatar.sprite = unknownAvatar;
        // Do not play outgoing ring here, as this is used for incoming calls too.
    }
    
    // ===== ДЕЙСТВИЯ СО ЗВОНКОМ =====
    
    public void CloseCaller()
    {
        callerScreen.SetActive(false);
        StopOutgoingRinging();
    }
    
    public void AnswerCall()
    {
        Debug.Log($"📞 Ответили на звонок от {callerName.text}");
        StopOutgoingRinging();
        // Здесь будет логика когда ответили
    }
    
    public void DeclineCall()
    {
        Debug.Log($"📵 Отклонили звонок от {callerName.text}");
        CloseCaller();
    }
}