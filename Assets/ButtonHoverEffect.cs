using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public float hoverScale = 1.05f;
    public float duration = 0.1f;
    public AudioClip clickSound;

    [Header("Main Menu Visuals (Optional)")]
    public GameObject frameObject;
    public UnityEngine.UI.Image textInscriptionImage;
    public TMPro.TMP_Text textComponent;
    public Color normalTextColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    public Color hoverTextColor = Color.white;
    
    private AudioSource audioSource;
    private Vector3 originalScale;
    private Coroutine scaleCoroutine;

    void Awake()
    {
        originalScale = transform.localScale;
        
        // Получаем или создаём AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // ПРИНУДИТЕЛЬНО ВКЛЮЧАЕМ AudioSource
        audioSource.enabled = true;
        audioSource.playOnAwake = false;

        if (textInscriptionImage != null)
        {
            textInscriptionImage.color = normalTextColor;
        }
        if (textComponent != null)
        {
            textComponent.color = normalTextColor;
        }
        if (frameObject != null)
        {
            frameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        StopScale();
        transform.localScale = originalScale;
        if (frameObject != null)
        {
            frameObject.SetActive(false);
        }
        if (textInscriptionImage != null)
        {
            textInscriptionImage.color = normalTextColor;
        }
        if (textComponent != null)
        {
            textComponent.color = normalTextColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StopScale();
        scaleCoroutine = StartCoroutine(ScaleTo(originalScale * hoverScale));

        if (frameObject != null)
        {
            frameObject.SetActive(true);
        }
        if (textInscriptionImage != null)
        {
            textInscriptionImage.color = hoverTextColor;
        }
        if (textComponent != null)
        {
            textComponent.color = hoverTextColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopScale();
        scaleCoroutine = StartCoroutine(ScaleTo(originalScale));

        if (frameObject != null)
        {
            frameObject.SetActive(false);
        }
        if (textInscriptionImage != null)
        {
            textInscriptionImage.color = normalTextColor;
        }
        if (textComponent != null)
        {
            textComponent.color = normalTextColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound == null) return;

        // Try to find a persistent AudioSource (e.g., on PhoneManager) to avoid sound cut-off when the button is deactivated
        AudioSource persistentSource = null;
        GameObject phoneManager = GameObject.Find("PhoneManager");
        if (phoneManager != null)
        {
            persistentSource = phoneManager.GetComponent<AudioSource>();
        }

        if (persistentSource != null && persistentSource.enabled && persistentSource.gameObject.activeInHierarchy)
        {
            persistentSource.PlayOneShot(clickSound, 0.8f);
        }
        else if (audioSource != null && audioSource.enabled && audioSource.gameObject.activeInHierarchy)
        {
            audioSource.PlayOneShot(clickSound, 0.8f);
        }
    }

    private void StopScale()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
    }

    private IEnumerator ScaleTo(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.localScale = targetScale;
    }
}