using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class GlobalCanvasPersistent : MonoBehaviour
{
    private static GlobalCanvasPersistent instance;
    public static GlobalCanvasPersistent Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.name = "GlobalCanvas"; // Ensure name is always exactly "GlobalCanvas"
        DontDestroyOnLoad(gameObject);
    }
}
