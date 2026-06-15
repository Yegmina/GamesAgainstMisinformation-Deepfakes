using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public float hoverScale = 1.05f;
    public float duration = 0.1f;
    public AudioClip clickSound;
    
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
    }

    void OnDisable()
    {
        StopScale();
        transform.localScale = originalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StopScale();
        scaleCoroutine = StartCoroutine(ScaleTo(originalScale * hoverScale));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopScale();
        scaleCoroutine = StartCoroutine(ScaleTo(originalScale));
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

        if (persistentSource != null && persistentSource.enabled)
        {
            persistentSource.PlayOneShot(clickSound, 0.8f);
        }
        else if (audioSource != null && audioSource.enabled)
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