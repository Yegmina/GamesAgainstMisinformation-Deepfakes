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
        SceneManager.LoadScene(targetSceneName);
    }
}
