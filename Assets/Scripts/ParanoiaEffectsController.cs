using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class ParanoiaEffectsController : MonoBehaviour
{
    private Volume volume;
    private VolumeProfile profile;

    // Post-processing components
    private ChromaticAberration chromaticAberration;
    private FilmGrain filmGrain;
    private Vignette vignette;
    private LensDistortion lensDistortion;
    private ColorAdjustments colorAdjustments;

    // Glitch timing state
    private float glitchTimer = 0f;
    private float nextGlitchTime = 3f;
    private bool isGlitchActive = false;
    private float glitchDuration = 0.1f;
    private float currentGlitchIntensity = 0f;

    // VHS Overlay UI references
    private GameObject vhsOverlayGo;
    private Image noisePanel;
    private Image[] scanlines;
    private float[] scanlineSpeeds;
    private float[] scanlinePositions;
    private Image staticBar;
    private float staticBarPosition = 0f;
    private float staticBarSpeed = -0.05f;

    // Dynamic, screen-space compatible VHS features (for Phone Screen Overlay)
    private Image uiVignette;
    private Texture2D vignetteTexture;
    private TMPro.TextMeshProUGUI vhsText;
    
    private readonly string[] creepyMessages = {
        "PLAY ▶", "RECORD 🔴", "SIGNAL LOST", "SYSTEM CORRUPTION", 
        "NOT ALONE", "I SEE YOU", "RUN", "HELP", "WARNING", "PARANOIA", "█▒░ ERROR ░▒█"
    };

    private void Awake()
    {
        // 1. Create a child GameObject on layer 0 (Default) so the camera's volumeLayerMask sees it
        GameObject volumeGo = new GameObject("ParanoiaVolume");
        volumeGo.transform.SetParent(transform, false);
        volumeGo.layer = 0; // Layer 0 is "Default"

        // 2. Set up the Volume dynamically at runtime on this child
        volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f; // High priority to ensure overrides apply
        
        // 3. Create an instanced profile to avoid polluting project assets
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        // 4. Add and auto-enable all parameters for overrides
        chromaticAberration = profile.Add<ChromaticAberration>(true);
        filmGrain = profile.Add<FilmGrain>(true);
        vignette = profile.Add<Vignette>(true);
        lensDistortion = profile.Add<LensDistortion>(true);
        colorAdjustments = profile.Add<ColorAdjustments>(true);

        // 5. Initialize to default safe/neutral states
        ResetEffects();
    }

    private void Start()
    {
        // Ensure post-processing is enabled on the Main Camera on startup
        EnsureCameraPostProcessing();
    }

    private void Update()
    {
        // Dynamically manage UI Canvas RenderModes based on active scene for post-processing compatibility
        EnsureScreenSpaceCameraForUI();

        // Make sure post-processing is enabled on whatever camera is active
        EnsureCameraPostProcessing();

        // Get current paranoia from the persistent global canvas
        int paranoia = 0;
        if (GlobalCanvasPersistent.Instance != null)
        {
            paranoia = GlobalCanvasPersistent.Instance.Paranoia;
        }

        // Apply paranoia effects based on current tier
        if (paranoia < 30)
        {
            // Tier 0: < 30% Paranoia - No effects
            ResetEffects();
            isGlitchActive = false;

            if (vhsOverlayGo != null && vhsOverlayGo.activeSelf)
            {
                vhsOverlayGo.SetActive(false);
            }
        }
        else if (paranoia >= 30 && paranoia < 60)
        {
            // Tier 1: 30% - 60% Paranoia - Subtle/gentle VHS glitching
            float t = (paranoia - 30f) / 30f; // 0.0 to 1.0 within tier 1
            ApplyTier1Effects(t);

            float maxLines = Mathf.Lerp(0.012f, 0.028f, t);
            float maxNoise = Mathf.Lerp(0.005f, 0.015f, t);
            float maxBar = Mathf.Lerp(0.012f, 0.025f, t);
            UpdateVHSOverlay(maxLines, maxNoise, maxBar, isGlitchActive, 1.0f, paranoia);
        }
        else
        {
            // Tier 2: 60% - 100% Paranoia - Stronger, creepier VHS glitching (designed to work on Phone screen too!)
            float t = (paranoia - 60f) / 40f; // 0.0 to 1.0 within tier 2
            ApplyTier2Effects(t);

            float maxLines = Mathf.Lerp(0.028f, 0.065f, t);
            float maxNoise = Mathf.Lerp(0.015f, 0.038f, t);
            float maxBar = Mathf.Lerp(0.025f, 0.06f, t);
            UpdateVHSOverlay(maxLines, maxNoise, maxBar, isGlitchActive, 1.4f, paranoia);
        }
    }

    private void EnsureScreenSpaceCameraForUI()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene == "SampleScene")
        {
            // 1. Convert the phone Canvas to World Space (so it gets full post-processing!)
            var phoneCanvasGo = GameObject.Find("Canvas");
            if (phoneCanvasGo != null && phoneCanvasGo.TryGetComponent<Canvas>(out var phoneCanvas))
            {
                if (phoneCanvas.renderMode != RenderMode.WorldSpace)
                {
                    phoneCanvas.renderMode = RenderMode.WorldSpace;
                    phoneCanvas.worldCamera = cam;
                }

                // Position phone Canvas static in front of the camera
                phoneCanvas.transform.position = cam.transform.position + cam.transform.forward * 10f;
                phoneCanvas.transform.rotation = cam.transform.rotation;
                UpdateWorldSpaceCanvasScale(phoneCanvas, cam, 10f);
            }

            // 2. Convert the GlobalCanvas to World Space (so our VHS overlay and text warp perfectly too!)
            if (TryGetComponent<Canvas>(out var globalCanvas))
            {
                if (globalCanvas.renderMode != RenderMode.WorldSpace)
                {
                    globalCanvas.renderMode = RenderMode.WorldSpace;
                    globalCanvas.worldCamera = cam;
                }

                // Position slightly closer than the phone Canvas so it draws on top
                globalCanvas.transform.position = cam.transform.position + cam.transform.forward * 9.9f;
                globalCanvas.transform.rotation = cam.transform.rotation;
                UpdateWorldSpaceCanvasScale(globalCanvas, cam, 9.9f);
            }
        }
        else
        {
            // Restore GlobalCanvas to ScreenSpaceOverlay in other scenes (like Apartment) to prevent any 3D world clipping
            if (TryGetComponent<Canvas>(out var globalCanvas))
            {
                if (globalCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    globalCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
            }
        }
    }

    private void UpdateWorldSpaceCanvasScale(Canvas canvas, Camera cam, float distance)
    {
        if (canvas == null || cam == null) return;

        float fov = cam.fieldOfView;
        float aspect = cam.aspect;

        // Calculate frustum height and width at the given distance
        float frustumHeight = 2.0f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * aspect;

        RectTransform rect = canvas.GetComponent<RectTransform>();
        if (rect != null)
        {
            float canvasWidth = rect.rect.width;
            float canvasHeight = rect.rect.height;

            if (canvasWidth > 0f && canvasHeight > 0f)
            {
                float scaleX = frustumWidth / canvasWidth;
                float scaleY = frustumHeight / canvasHeight;

                // Set scale to fit the camera frustum perfectly
                canvas.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }
    }

    private Texture2D GenerateVignetteTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - (size - 1) / 2f) / (size / 2f);
                float dy = (y - (size - 1) / 2f) / (size / 2f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                // Central area is fully transparent, edges fade to solid white alpha
                float alpha = Mathf.SmoothStep(0.25f, 0.98f, dist);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private void CreateVHSOverlay()
    {
        // 1. Create container
        vhsOverlayGo = new GameObject("VHS_Overlay", typeof(RectTransform));
        vhsOverlayGo.transform.SetParent(transform, false);
        vhsOverlayGo.transform.SetAsFirstSibling(); // Put behind other HUD UI elements

        RectTransform rect = vhsOverlayGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Add CanvasGroup to handle visibility and disable mouse blocking
        CanvasGroup group = vhsOverlayGo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // 2. Create full-screen noise panel (semi-transparent black for brightness flicker)
        GameObject noiseGo = new GameObject("FlickerPanel", typeof(RectTransform), typeof(Image));
        noiseGo.transform.SetParent(vhsOverlayGo.transform, false);
        RectTransform noiseRect = noiseGo.GetComponent<RectTransform>();
        noiseRect.anchorMin = Vector2.zero;
        noiseRect.anchorMax = Vector2.one;
        noiseRect.offsetMin = Vector2.zero;
        noiseRect.offsetMax = Vector2.zero;
        noisePanel = noiseGo.GetComponent<Image>();
        noisePanel.color = new Color(0f, 0f, 0f, 0f);

        // 3. Create procedural UI Vignette (vital so it works on ScreenSpaceOverlay Canvas too, like Phone UI!)
        GameObject vignetteGo = new GameObject("UIVignette", typeof(RectTransform), typeof(Image));
        vignetteGo.transform.SetParent(vhsOverlayGo.transform, false);
        RectTransform vigRect = vignetteGo.GetComponent<RectTransform>();
        vigRect.anchorMin = Vector2.zero;
        vigRect.anchorMax = Vector2.one;
        vigRect.offsetMin = Vector2.zero;
        vigRect.offsetMax = Vector2.zero;
        uiVignette = vignetteGo.GetComponent<Image>();
        vignetteTexture = GenerateVignetteTexture();
        uiVignette.sprite = Sprite.Create(vignetteTexture, new Rect(0, 0, vignetteTexture.width, vignetteTexture.height), new Vector2(0.5f, 0.5f));
        uiVignette.color = new Color(0f, 0f, 0f, 0f);

        // 4. Create scanlines (thin horizontal lines)
        int scanlineCount = 4;
        scanlines = new Image[scanlineCount];
        scanlineSpeeds = new float[scanlineCount];
        scanlinePositions = new float[scanlineCount];

        for (int i = 0; i < scanlineCount; i++)
        {
            GameObject lineGo = new GameObject("Scanline_" + i, typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(vhsOverlayGo.transform, false);
            RectTransform lineRect = lineGo.GetComponent<RectTransform>();
            
            lineRect.anchorMin = new Vector2(0f, 0f);
            lineRect.anchorMax = new Vector2(1f, 0f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.offsetMin = Vector2.zero;
            lineRect.offsetMax = Vector2.zero;
            
            float height = Random.Range(1f, 3f);
            lineRect.sizeDelta = new Vector2(0f, height);

            scanlines[i] = lineGo.GetComponent<Image>();
            scanlines[i].color = new Color(0f, 0f, 0f, 0f);

            scanlinePositions[i] = Random.value;
            scanlineSpeeds[i] = Random.Range(-0.04f, -0.15f); // Drift downwards
        }

        // 5. Create wide tracking static bar (horizontal VHS interference)
        GameObject barGo = new GameObject("TrackingBar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(vhsOverlayGo.transform, false);
        RectTransform barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 15f);
        
        staticBar = barGo.GetComponent<Image>();
        staticBar.color = new Color(0f, 0f, 0f, 0f);
        staticBarPosition = Random.value;
        staticBarSpeed = -0.06f;

        // 6. Create creepy VHS text container
        GameObject textGo = new GameObject("VHSText", typeof(RectTransform));
        textGo.transform.SetParent(vhsOverlayGo.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.06f, 0.08f);
        textRect.anchorMax = new Vector2(0.94f, 0.92f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        vhsText = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        vhsText.fontSize = 24f;
        vhsText.fontStyle = TMPro.FontStyles.Bold;
        vhsText.color = new Color(1f, 1f, 1f, 0f);
        vhsText.alignment = TMPro.TextAlignmentOptions.BottomLeft;
        vhsText.text = "";
    }

    private void UpdateVHSOverlay(float maxAlphaLines, float maxAlphaNoise, float maxAlphaBar, bool glitching, float speedMultiplier, int paranoia)
    {
        if (vhsOverlayGo == null)
        {
            CreateVHSOverlay();
        }

        vhsOverlayGo.SetActive(true);

        RectTransform mainRect = vhsOverlayGo.GetComponent<RectTransform>();

        // Apply tracking horizontal jitter/tear during glitches, especially at high paranoia (Tier 2)
        if (glitching && paranoia >= 60)
        {
            float jitterX = Random.Range(-18f, 18f);
            float jitterY = Random.Range(-3f, 3f);
            mainRect.offsetMin = new Vector2(jitterX, jitterY);
            mainRect.offsetMax = new Vector2(jitterX, jitterY);
        }
        else if (glitching)
        {
            float jitterX = Random.Range(-5f, 5f);
            mainRect.offsetMin = new Vector2(jitterX, 0f);
            mainRect.offsetMax = new Vector2(jitterX, 0f);
        }
        else
        {
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;
        }

        // 1. Update Noise/Flicker panel
        float noiseAlpha = 0f;
        if (maxAlphaNoise > 0f)
        {
            float baseFlicker = Mathf.PingPong(Time.time * 10f, maxAlphaNoise);
            float randomPop = (Random.value < 0.02f) ? Random.Range(0f, maxAlphaNoise * 1.4f) : 0f;
            noiseAlpha = Mathf.Clamp(baseFlicker + randomPop, 0f, 0.15f);
        }
        
        if (glitching)
        {
            noiseAlpha = Mathf.Max(noiseAlpha, Random.Range(0.03f, 0.14f));
            noisePanel.color = new Color(0.92f, 0.92f, 1f, noiseAlpha * 0.35f); // subtle cool color flash
        }
        else
        {
            noisePanel.color = new Color(0f, 0f, 0f, noiseAlpha); // standard ambient brightness instability
        }

        // 2. Update procedural UI Vignette (blood red pulse starting at 60% paranoia)
        if (uiVignette != null)
        {
            if (paranoia < 30)
            {
                uiVignette.color = new Color(0f, 0f, 0f, 0f);
            }
            else if (paranoia >= 30 && paranoia < 60)
            {
                // Soft static black vignette
                float normParanoia = (paranoia - 30f) / 30f;
                float targetVigAlpha = Mathf.Lerp(0.08f, 0.25f, normParanoia);
                uiVignette.color = new Color(0f, 0f, 0f, targetVigAlpha);
            }
            else // >= 60% Paranoia
            {
                // Pulse vignette like a panic heartbeat
                float normParanoia = (paranoia - 60f) / 40f; // 0.0 to 1.0
                float pulseSpeed = Mathf.Lerp(5f, 12f, normParanoia); // faster heartbeat closer to 100%
                float baseAlpha = Mathf.Lerp(0.25f, 0.45f, normParanoia);
                float heartPulse = Mathf.Sin(Time.time * pulseSpeed) * Mathf.Lerp(0.06f, 0.18f, normParanoia);
                float currentAlpha = Mathf.Clamp(baseAlpha + heartPulse, 0.1f, 0.75f);
                
                // Color transition from dark black to warning crimson blood-red
                Color pulseColor = Color.Lerp(new Color(0f, 0f, 0f), new Color(0.28f, 0.01f, 0.01f), normParanoia);
                uiVignette.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, currentAlpha);
            }
        }

        // 3. Update Scanlines
        for (int i = 0; i < scanlines.Length; i++)
        {
            if (scanlines[i] == null) continue;

            scanlinePositions[i] += scanlineSpeeds[i] * Time.deltaTime * speedMultiplier;
            if (scanlinePositions[i] < 0f) scanlinePositions[i] = 1f;
            if (scanlinePositions[i] > 1f) scanlinePositions[i] = 0f;

            RectTransform rect = scanlines[i].rectTransform;
            rect.anchorMin = new Vector2(0f, scanlinePositions[i]);
            rect.anchorMax = new Vector2(1f, scanlinePositions[i]);

            if (glitching)
            {
                float jitter = Random.Range(-0.015f, 0.015f);
                rect.anchorMin = new Vector2(0f, Mathf.Clamp01(scanlinePositions[i] + jitter));
                rect.anchorMax = new Vector2(1f, Mathf.Clamp01(scanlinePositions[i] + jitter));
            }

            float lineAlpha = maxAlphaLines;
            if (glitching)
            {
                lineAlpha = Random.Range(maxAlphaLines, maxAlphaLines * 2f);
            }
            float fade = Mathf.Sin(scanlinePositions[i] * Mathf.PI);
            scanlines[i].color = new Color(0f, 0f, 0f, lineAlpha * (0.3f + 0.7f * fade));
        }

        // 4. Update wide Tracking Bar
        if (staticBar != null)
        {
            staticBarPosition += staticBarSpeed * Time.deltaTime * speedMultiplier;
            if (staticBarPosition < 0f)
            {
                staticBarPosition = 1f;
                staticBarSpeed = Random.Range(-0.03f, -0.07f);
            }

            RectTransform barRect = staticBar.rectTransform;
            barRect.anchorMin = new Vector2(0f, staticBarPosition);
            barRect.anchorMax = new Vector2(1f, staticBarPosition);

            if (glitching)
            {
                barRect.sizeDelta = new Vector2(0f, Random.Range(10f, 30f));
                float jitter = Random.Range(-0.008f, 0.008f);
                barRect.anchorMin = new Vector2(0f, Mathf.Clamp01(staticBarPosition + jitter));
                barRect.anchorMax = new Vector2(1f, Mathf.Clamp01(staticBarPosition + jitter));
            }
            else
            {
                barRect.sizeDelta = new Vector2(0f, 15f);
            }

            float barAlpha = maxAlphaBar;
            if (glitching)
            {
                barAlpha = Random.Range(maxAlphaBar * 1.3f, maxAlphaBar * 2.8f);
            }
            float fade = Mathf.Sin(staticBarPosition * Mathf.PI);
            staticBar.color = new Color(0.12f, 0.12f, 0.12f, barAlpha * (0.4f + 0.6f * fade));
        }

        // 5. Update VHS Creepy Text Glitches (>= 60% Paranoia)
        if (vhsText != null)
        {
            if (paranoia >= 60 && glitching)
            {
                // Select a random creepy message if not already displaying one in this glitch frame
                if (string.IsNullOrEmpty(vhsText.text) || Random.value < 0.15f)
                {
                    vhsText.text = creepyMessages[Random.Range(0, creepyMessages.Length)];
                    
                    // Randomly position the text on the screen (camcorder style)
                    float rnd = Random.value;
                    if (rnd < 0.25f)
                    {
                        vhsText.alignment = TMPro.TextAlignmentOptions.BottomLeft;
                    }
                    else if (rnd < 0.5f)
                    {
                        vhsText.alignment = TMPro.TextAlignmentOptions.TopLeft;
                    }
                    else if (rnd < 0.75f)
                    {
                        vhsText.alignment = TMPro.TextAlignmentOptions.TopRight;
                    }
                    else
                    {
                        vhsText.alignment = TMPro.TextAlignmentOptions.BottomRight;
                    }
                }
                
                // Color is a classic glitched VHS white or blood-red
                vhsText.color = (Random.value < 0.35f) ? new Color(0.9f, 0.1f, 0.1f, Random.Range(0.4f, 0.85f)) : new Color(1f, 1f, 1f, Random.Range(0.5f, 0.9f));
            }
            else if (paranoia >= 30 && paranoia < 60 && glitching && Random.value < 0.08f)
            {
                // In Tier 1, just very rarely show standard "PLAY ▶" or "RECORD 🔴"
                vhsText.text = Random.value < 0.5f ? "PLAY ▶" : "RECORD 🔴";
                vhsText.alignment = TMPro.TextAlignmentOptions.TopLeft;
                vhsText.color = new Color(1f, 1f, 1f, Random.Range(0.2f, 0.5f));
            }
            else
            {
                // Slowly fade out text
                vhsText.color = Color.Lerp(vhsText.color, new Color(1f, 1f, 1f, 0f), Time.deltaTime * 10f);
                if (vhsText.color.a < 0.05f)
                {
                    vhsText.text = "";
                }
            }
        }
    }

    private void ResetEffects()
    {
        chromaticAberration.intensity.value = 0f;
        filmGrain.intensity.value = 0f;
        vignette.intensity.value = 0f;
        lensDistortion.intensity.value = 0f;
        colorAdjustments.contrast.value = 0f;
        colorAdjustments.saturation.value = 0f;
        colorAdjustments.postExposure.value = 0f;
        colorAdjustments.hueShift.value = 0f;
    }

    private void EnsureCameraPostProcessing()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var camData))
        {
            if (!camData.renderPostProcessing)
            {
                camData.renderPostProcessing = true;
            }
        }
    }

    private void ApplyTier1Effects(float t)
    {
        // Smoothly scale base values based on intensity t
        float baseChromatic = Mathf.Lerp(0.08f, 0.22f, t);
        float baseGrain = Mathf.Lerp(0.05f, 0.15f, t);
        float baseVignette = Mathf.Lerp(0.12f, 0.26f, t);

        // Handle random light glitch twitches
        HandleGlitchTiming(minInterval: 4.5f, maxInterval: 8.5f, duration: 0.08f, intensityFactor: 0.15f);

        // Apply base + subtle high-frequency flicker to chromatic aberration
        float flicker = Mathf.Sin(Time.time * 30f) * 0.02f;
        chromaticAberration.intensity.value = Mathf.Max(0f, baseChromatic + flicker + currentGlitchIntensity);

        // Set static Film Grain lookup type
        filmGrain.type.value = FilmGrainLookup.Thin1;
        filmGrain.intensity.value = baseGrain;

        // Set static Vignette (neutral dark vignette)
        vignette.color.value = Color.black;
        vignette.intensity.value = baseVignette;
        vignette.smoothness.value = 0.35f;

        // Lens distortion twitches slightly during glitches
        lensDistortion.intensity.value = isGlitchActive ? (Random.value < 0.5f ? -0.06f : 0.06f) : 0f;

        // Apply digital static and reset other color adjustments
        if (isGlitchActive)
        {
            colorAdjustments.postExposure.value = Random.Range(-0.05f, 0.1f);
            colorAdjustments.hueShift.value = Random.Range(-3f, 3f);
        }
        else
        {
            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.hueShift.value = 0f;
        }
        colorAdjustments.contrast.value = 0f;
        colorAdjustments.saturation.value = 0f;
    }

    private void ApplyTier2Effects(float t)
    {
        // Smoothly scale base values based on intensity t
        float baseChromatic = Mathf.Lerp(0.22f, 0.45f, t);
        float baseGrain = Mathf.Lerp(0.15f, 0.35f, t);
        float baseVignette = Mathf.Lerp(0.26f, 0.44f, t);
        float baseContrast = Mathf.Lerp(5f, 22f, t);
        float baseDesat = Mathf.Lerp(-5f, -28f, t);

        // Handle slightly more frequent and aggressive glitches
        HandleGlitchTiming(minInterval: 2f, maxInterval: 5f, duration: 0.13f, intensityFactor: 0.4f);

        // Faster, stronger chromatic aberration jitter/flicker
        float flicker = Mathf.Sin(Time.time * 45f) * 0.06f;
        chromaticAberration.intensity.value = Mathf.Max(0f, baseChromatic + flicker + (currentGlitchIntensity * 1.5f));

        // Stronger grain lookup
        filmGrain.type.value = FilmGrainLookup.Medium1;
        filmGrain.intensity.value = baseGrain;

        // Vignette pulses slowly like a panic heartbeat
        float heartPulse = Mathf.Sin(Time.time * 7f) * 0.04f;
        vignette.intensity.value = Mathf.Max(0.1f, baseVignette + heartPulse);
        vignette.smoothness.value = 0.42f;

        // Give vignette a reddish warning tint at higher paranoia
        vignette.color.value = Color.Lerp(Color.black, new Color(0.38f, 0.03f, 0.03f), t);

        // Lens distortion jitter/flicker during glitches
        if (isGlitchActive)
        {
            lensDistortion.intensity.value = Random.Range(-0.15f, 0.15f);
        }
        else
        {
            // Minor breathing lens warp
            lensDistortion.intensity.value = Mathf.Sin(Time.time * 1.5f) * 0.015f;
        }

        // Apply digital static and colder, tense contrast and desaturation
        if (isGlitchActive)
        {
            colorAdjustments.postExposure.value = Random.Range(-0.15f, 0.25f);
            colorAdjustments.hueShift.value = Random.Range(-8f, 8f);
            colorAdjustments.contrast.value = baseContrast + Random.Range(-10f, 25f);
        }
        else
        {
            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.hueShift.value = 0f;
            colorAdjustments.contrast.value = baseContrast;
        }
        colorAdjustments.saturation.value = baseDesat;
    }

    private void HandleGlitchTiming(float minInterval, float maxInterval, float duration, float intensityFactor)
    {
        glitchTimer += Time.deltaTime;

        if (!isGlitchActive)
        {
            if (glitchTimer >= nextGlitchTime)
            {
                // Trigger a new glitch
                isGlitchActive = true;
                glitchTimer = 0f;
                glitchDuration = duration;
                currentGlitchIntensity = intensityFactor;
            }
        }
        else
        {
            if (glitchTimer >= glitchDuration)
            {
                // End current glitch
                isGlitchActive = false;
                glitchTimer = 0f;
                nextGlitchTime = Random.Range(minInterval, maxInterval);
                currentGlitchIntensity = 0f;
            }
        }
    }

    private void OnDestroy()
    {
        if (profile != null)
        {
            Destroy(profile);
        }
        if (vhsOverlayGo != null)
        {
            Destroy(vhsOverlayGo);
        }
        if (vignetteTexture != null)
        {
            Destroy(vignetteTexture);
        }
    }
}