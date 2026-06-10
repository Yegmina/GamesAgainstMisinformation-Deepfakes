using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField] private string interactionText = "Press E to use computer";

    public Transform SitPoint => sitPoint;
    public string InteractionText => interactionText;

    [SerializeField]
    private Transform sitPoint;

    public virtual void Interact() // adding actions to the interactable object
    {
        Debug.Log($"Interact with {gameObject.name}");
    }

    public virtual void ExitInteraction()
    {
        Debug.Log($"Exit {gameObject.name}");
    }
}
