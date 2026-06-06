using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows an "incoming call" screen a set number of seconds after the game starts.
/// GREEN (Answer) opens the existing Caller Screen with the unknown avatar, plays the
/// ringtone once, and shows Answer/Decline buttons there. RED (Decline) just dismisses.
/// Either button on the Caller Screen stops the audio and returns to the Home Screen.
/// The call is only shown while the player is idle on the Home Screen (never during a chat).
/// </summary>
public class IncomingCallManager : MonoBehaviour
{
    [Header("Managers")]
    public PhoneUIManager phoneManager;
    public CallerController callerController;

    [Header("Incoming Call Screen")]
    public GameObject incomingCallScreen;
    public Button incomingAnswerButton;   // GREEN
    public Button incomingDeclineButton;  // RED

    [Header("Caller Screen buttons")]
    public GameObject callerEndCallButton;   // existing EndCallButton (hidden during this flow)
    public Button callerAnswerButton;        // GREEN (added to Caller Screen)
    public Button callerDeclineButton;       // RED (added to Caller Screen)

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip ringtoneClip;           // getFromTcom sound (plays once on answer)

    [Header("Incoming Ringtone")]
    public AudioSource ringtoneSource;       // dedicated source for the looping ringtone
    public AudioClip incomingRingtoneClip;   // iPhone-style ringtone (loops while ringing)

    [Header("Timing")]
    public float delaySeconds = 30f;

    float timer;
    bool callShown;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // Dedicated source for the looping ringtone so it never fights the answer sound.
        if (ringtoneSource == null) ringtoneSource = gameObject.AddComponent<AudioSource>();
        ringtoneSource.playOnAwake = false;
        ringtoneSource.loop = true;

        if (incomingAnswerButton != null) incomingAnswerButton.onClick.AddListener(AnswerIncoming);
        if (incomingDeclineButton != null) incomingDeclineButton.onClick.AddListener(DeclineIncoming);
        if (callerAnswerButton != null) callerAnswerButton.onClick.AddListener(EndCallerScreen);
        if (callerDeclineButton != null) callerDeclineButton.onClick.AddListener(EndCallerScreen);
    }

    void Start()
    {
        if (incomingCallScreen != null) incomingCallScreen.SetActive(false);
        if (callerAnswerButton != null) callerAnswerButton.gameObject.SetActive(false);
        if (callerDeclineButton != null) callerDeclineButton.gameObject.SetActive(false);
    }

    void Update()
    {
        if (callShown) return;

        timer += Time.deltaTime;
        if (timer < delaySeconds) return;

        // Only interrupt the player when they are idle on the Home Screen.
        // This avoids showing the call during a chat or another call.
        if (!IsIdleOnHome()) return;

        ShowIncomingCall();
    }

    bool IsIdleOnHome()
    {
        if (phoneManager == null || phoneManager.homeScreen == null) return true;
        return phoneManager.homeScreen.activeSelf;
    }

    void ShowIncomingCall()
    {
        callShown = true;
        if (phoneManager != null && phoneManager.homeScreen != null)
            phoneManager.homeScreen.SetActive(false);
        if (incomingCallScreen != null) incomingCallScreen.SetActive(true);

        // Start the looping ringtone while the phone is ringing.
        if (ringtoneSource != null && incomingRingtoneClip != null)
        {
            ringtoneSource.clip = incomingRingtoneClip;
            ringtoneSource.loop = true;
            ringtoneSource.Play();
        }
    }

    void StopRingtone()
    {
        if (ringtoneSource != null) ringtoneSource.Stop();
    }

    // GREEN on incoming call screen
    public void AnswerIncoming()
    {
        StopRingtone();
        if (incomingCallScreen != null) incomingCallScreen.SetActive(false);

        // Open the existing Caller Screen with the unknown caller.
        if (callerController != null) callerController.OpenUnknownCall();

        // Swap the single end-call button for the Answer/Decline pair.
        if (callerEndCallButton != null) callerEndCallButton.SetActive(false);
        if (callerAnswerButton != null) callerAnswerButton.gameObject.SetActive(true);
        if (callerDeclineButton != null) callerDeclineButton.gameObject.SetActive(true);

        // Play the ringtone once.
        if (audioSource != null && ringtoneClip != null)
        {
            audioSource.clip = ringtoneClip;
            audioSource.loop = false;
            audioSource.Play();
        }
    }

    // RED on incoming call screen
    public void DeclineIncoming()
    {
        StopRingtone();
        if (incomingCallScreen != null) incomingCallScreen.SetActive(false);
        if (phoneManager != null && phoneManager.homeScreen != null)
            phoneManager.homeScreen.SetActive(true);
    }

    // Answer or Decline on the Caller Screen: stop audio, close, go Home.
    public void EndCallerScreen()
    {
        if (audioSource != null) audioSource.Stop();

        if (callerAnswerButton != null) callerAnswerButton.gameObject.SetActive(false);
        if (callerDeclineButton != null) callerDeclineButton.gameObject.SetActive(false);
        if (callerEndCallButton != null) callerEndCallButton.SetActive(true);

        if (phoneManager != null) phoneManager.CloseCaller();
        else if (callerController != null) callerController.CloseCaller();
    }
}
