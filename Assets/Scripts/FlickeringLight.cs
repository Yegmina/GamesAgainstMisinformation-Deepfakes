using UnityEngine;

public class FlickeringLight : MonoBehaviour
{
    [SerializeField] private Light[] lights;

    [SerializeField] private float flickerChance = 0.2f;
    [SerializeField] private float interval = 0.05f;

    private void Start()
    {
        InvokeRepeating(nameof(Flicker), 0f, interval);
    }

    private void Flicker()
    {
        bool on = Random.value > flickerChance;

        foreach (Light light in lights)
        {
            if (light != null)
                light.enabled = on;
        }
    }
}