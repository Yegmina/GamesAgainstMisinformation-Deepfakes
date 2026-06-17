using UnityEngine;
using UnityEngine.SceneManagement;

public class Interactable : MonoBehaviour
{
    [SerializeField] private string interactionText = "Press E to use computer";
    [SerializeField] private string exitText = "Esc to exit";
    [SerializeField] private string sceneToLoad = "";
    [SerializeField] private bool openComputerOverlay = false;

    public Transform SitPoint => sitPoint;
    public string InteractionText => interactionText;
    public string ExitText => exitText;
    public bool OpensComputerOverlay => ShouldOpenComputerOverlay();

    [SerializeField]
    private Transform sitPoint;

    protected virtual void Start()
    {
        if (ShouldOpenComputerOverlay())
        {
            ComputerOverlayController.PreloadComputer(sitPoint);
        }
    }

    public virtual void Interact() // adding actions to the interactable object
    {
        Debug.Log($"Interact with {gameObject.name}");
        if (ShouldOpenComputerOverlay())
        {
            Debug.Log($"Opening computer overlay from {gameObject.name}");
            ComputerOverlayController.OpenComputer(sitPoint);
            return;
        }

        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    public virtual void ExitInteraction()
    {
        Debug.Log($"Exit {gameObject.name}");
        if (ShouldOpenComputerOverlay())
        {
            ComputerOverlayController.CloseComputer();
        }
    }

    private bool ShouldOpenComputerOverlay()
    {
        return openComputerOverlay || (!string.IsNullOrWhiteSpace(interactionText) && interactionText.ToLowerInvariant().Contains("computer"));
    }
}
