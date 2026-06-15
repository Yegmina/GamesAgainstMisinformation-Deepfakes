using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class FlickerEffect : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] private Color glowColor = new Color(1f, 1f, 1f, 0.15f);
    
    [Header("Slow Breathing (Sine Wave)")]
    [SerializeField] private float sineAmplitude = 0.03f;
    [SerializeField] private float sineFrequency = 1.2f;

    [Header("Rapid Flicker (Perlin Noise)")]
    [SerializeField] private float noiseAmplitude = 0.05f;
    [SerializeField] private float noiseFrequency = 18f;

    [Header("Random Micro-Drops (Organic Drops)")]
    [SerializeField] [Range(0f, 1f)] private float dropChance = 0.01f; // Chance per frame to trigger a drop
    [SerializeField] private float dropIntensity = 0.4f; // Multiplier of intensity during drop
    [SerializeField] private float dropDuration = 0.15f; // Duration of drop in seconds

    private Image uiImage;
    private float baseAlpha;
    private float currentDropTimer = 0f;
    private float noiseOffset;

    private void Awake()
    {
        uiImage = GetComponent<Image>();
        baseAlpha = glowColor.a;
        noiseOffset = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        if (uiImage == null) return;

        // 1. Slow breathing wave
        float sineVal = Mathf.Sin(Time.time * sineFrequency) * sineAmplitude;

        // 2. Rapid Perlin noise flicker
        float noiseVal = (Mathf.PerlinNoise(Time.time * noiseFrequency, noiseOffset) - 0.5f) * 2f * noiseAmplitude;

        // 3. Random organic drops
        float dropMultiplier = 1f;
        if (currentDropTimer > 0f)
        {
            currentDropTimer -= Time.deltaTime;
            // Smoothly interpolate back to normal intensity after a drop
            dropMultiplier = Mathf.Lerp(1f, dropIntensity, currentDropTimer / dropDuration);
        }
        else if (Random.value < dropChance)
        {
            currentDropTimer = dropDuration;
            dropMultiplier = dropIntensity;
        }

        // Combine
        float finalAlpha = (baseAlpha + sineVal + noiseVal) * dropMultiplier;
        finalAlpha = Mathf.Clamp01(finalAlpha);

        Color targetColor = glowColor;
        targetColor.a = finalAlpha;
        uiImage.color = targetColor;
    }
}
