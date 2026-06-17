using UnityEngine;

public class PhoneInteractable : Interactable
{
    [Header("Glow Settings")]
    [SerializeField] private float delayTime = 10f;
    [SerializeField] private Color glowColor = new Color(0.2f, 0.6f, 1.0f); // Cozy light blue
    [SerializeField] private float maxIntensity = 3.5f;
    [SerializeField] private float minIntensity = 0.5f;
    [SerializeField] private float glowSpeed = 2.0f;
    [SerializeField] private float lightRange = 1.8f;

    [Header("Screen Material Settings")]
    [SerializeField] private bool dimScreenInitially = true;
    [SerializeField] private Color dimmedColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    private Renderer screenRenderer;
    private Color originalBaseColor = Color.white;
    private Light glowLight;
    private bool isGlowing = false;
    private bool hasInteracted = false;
    private float timer = 0f;

    private void Start()
    {
        base.Start();
        smoothTransition = true;

        // Find the PhoneScreenQuad renderer
        Transform screenQuad = transform.Find("PhoneScreenQuad");
        if (screenQuad != null)
        {
            screenRenderer = screenQuad.GetComponent<Renderer>();
            if (screenRenderer != null)
            {
                // Accessing .material instantiates it so we don't modify the asset on disk
                if (screenRenderer.material.HasProperty("_BaseColor"))
                {
                    originalBaseColor = screenRenderer.material.GetColor("_BaseColor");
                }
                else if (screenRenderer.material.HasProperty("_Color"))
                {
                    originalBaseColor = screenRenderer.material.GetColor("_Color");
                }

                if (dimScreenInitially)
                {
                    SetScreenColor(dimmedColor);
                }
            }
        }

        // Create the light GameObject but keep it disabled initially
        CreateGlowLight();
    }

    private void CreateGlowLight()
    {
        Transform screenQuad = transform.Find("PhoneScreenQuad");
        Vector3 spawnPos = screenQuad != null ? screenQuad.position + Vector3.up * 0.05f : transform.position + Vector3.up * 0.2f;

        GameObject lightGo = new GameObject("PhoneGlowLight");
        lightGo.transform.SetParent(this.transform);
        lightGo.transform.position = spawnPos;

        glowLight = lightGo.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = lightRange;
        glowLight.intensity = 0f;
        glowLight.shadows = LightShadows.None;
        glowLight.enabled = false;
    }

    private void Update()
    {
        if (hasInteracted) return;

        if (!isGlowing)
        {
            timer += Time.deltaTime;
            if (timer >= delayTime)
            {
                isGlowing = true;
                if (glowLight != null)
                {
                    glowLight.enabled = true;
                }
                
                if (dimScreenInitially)
                {
                    SetScreenColor(originalBaseColor);
                }
            }
        }
        else
        {
            // Breathing animation for the glow light
            if (glowLight != null)
            {
                float t = Mathf.PingPong(Time.time * glowSpeed, 1.0f);
                t = Mathf.SmoothStep(0f, 1f, t);
                glowLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
            }
        }
    }

    public override void Interact()
    {
        if (!hasInteracted)
        {
            StopGlow();
        }

        base.Interact();
    }

    private void StopGlow()
    {
        hasInteracted = true;
        isGlowing = false;
        
        if (glowLight != null)
        {
            glowLight.enabled = false;
            Destroy(glowLight.gameObject);
        }

        if (dimScreenInitially)
        {
            SetScreenColor(dimmedColor);
        }
    }

    private void SetScreenColor(Color color)
    {
        if (screenRenderer != null && screenRenderer.material != null)
        {
            if (screenRenderer.material.HasProperty("_BaseColor"))
            {
                screenRenderer.material.SetColor("_BaseColor", color);
            }
            else if (screenRenderer.material.HasProperty("_Color"))
            {
                screenRenderer.material.SetColor("_Color", color);
            }
        }
    }
}
