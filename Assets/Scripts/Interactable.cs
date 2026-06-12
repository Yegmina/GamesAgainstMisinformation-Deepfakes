using UnityEngine;
using UnityEngine.SceneManagement;

public class Interactable : MonoBehaviour
{
    [SerializeField] private string interactionText = "Press E to use computer";
    [SerializeField] private string sceneToLoad = "";

    public string InteractionText => interactionText;

    public void Interact() // adding actions to the interactable object
    {
        Debug.Log($"Interact with {gameObject.name}");
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
