using UnityEngine;

public class PhoneUIManager : MonoBehaviour
{
    public GameObject homeScreen;
    public GameObject galleryScreen;
    public GameObject messagesScreen;
    public GameObject callsScreen;
    public GameObject browserScreen; 
    public GameObject SocialMediaScreen;
    public GameObject callerScreen;
    public GameObject chatScreen;

    // OPEN GALLERY
    public void OpenGallery()
    {
        homeScreen.SetActive(false);
        galleryScreen.SetActive(true);
    }

    // CLOSE GALLERY
    public void CloseGallery()
    {
        galleryScreen.SetActive(false);
        homeScreen.SetActive(true);
    }

    // OPEN MESSAGES
    public void OpenMessages()
    {
        homeScreen.SetActive(false);
        messagesScreen.SetActive(true);
    }

    // CLOSE MESSAGES
    public void CloseMessages()
    {
        messagesScreen.SetActive(false);
        homeScreen.SetActive(true);
    }
    
    // OPEN CALLS
    public void OpenCalls()
    {
        homeScreen.SetActive(false);
        callsScreen.SetActive(true);
    }

    // CLOSE CALLS
    public void CloseCalls()
    {
        callsScreen.SetActive(false);
        homeScreen.SetActive(true);
    }

    // ========== BROWSER (НОВЫЕ МЕТОДЫ) ==========
    
    // OPEN BROWSER
    public void OpenBrowser()
    {
        homeScreen.SetActive(false);
        browserScreen.SetActive(true);
    }

    // CLOSE BROWSER
    public void CloseBrowser()
    {
        browserScreen.SetActive(false);
        homeScreen.SetActive(true);
    }
    //open socialmedia
    public void OpenSocial()
    {
        homeScreen.SetActive(false);
        SocialMediaScreen.SetActive(true);
    }

    // CLOSE socialmedia
    public void CloseSocial()
    {
        SocialMediaScreen.SetActive(false);
        homeScreen.SetActive(true);
    }

    //////////////caller 
    public void OpenCaller()
    {
        homeScreen.SetActive(false);
        callerScreen.SetActive(true);
    }

    // CLOSE 
    public void CloseCaller()
    {
        callerScreen.SetActive(false);
        homeScreen.SetActive(true);
    }
    //////////chat
    
    
    // OPEN CHAT
    public void OpenChat()
    {
        homeScreen.SetActive(false);
        chatScreen.SetActive(true);
    }

    // CLOSE CHAT
    public void CloseChat()
    {
        chatScreen.SetActive(false);
        homeScreen.SetActive(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Apartment");
        }
    }
}