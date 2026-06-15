using UnityEngine;
using UnityEngine.SceneManagement;

public class Interactable : MonoBehaviour
{
    [SerializeField] private string interactionText = "Press E to use computer";
    [SerializeField] private string exitText = "Esc to exit";
    [SerializeField] private string sceneToLoad = "";

    public Transform SitPoint => sitPoint;
    public string InteractionText => interactionText;
    public string ExitText => exitText;

    [SerializeField]
    private Transform sitPoint;

    public virtual void Interact() // adding actions to the interactable object
    {
        Debug.Log($"Interact with {gameObject.name}");
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    public virtual void ExitInteraction()
    {
        Debug.Log($"Exit {gameObject.name}");
    }
}
