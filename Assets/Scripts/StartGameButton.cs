using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class StartGameButton : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "Apartment";

    private void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnStartGameClicked);
        }
    }

    private void OnStartGameClicked()
    {
        Debug.Log($"Starting game... Loading target scene: {targetSceneName}");

        // Reset global HUD (timer, paranoia, points)
        if (GlobalCanvasPersistent.Instance != null)
        {
            GlobalCanvasPersistent.Instance.ResetHUD();
        }

        // Reset Computer AI state so a new game session is generated
        ComputerOverlayController.ResetComputerState();

        SceneManager.LoadScene(targetSceneName);
    }
}
