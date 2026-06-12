using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PlayAgainButton : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "Apartment";

    private void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnPlayAgainClicked);
        }
    }

    private void OnPlayAgainClicked()
    {
        Debug.Log($"Loading target scene: {targetSceneName}");
        SceneManager.LoadScene(targetSceneName);
    }
}
