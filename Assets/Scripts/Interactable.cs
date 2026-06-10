using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField] private string interactionText = "Press E to use computer";

    public string InteractionText => interactionText;

    public void Interact() // adding actions to the interactable object
    {
        Debug.Log($"Interact with {gameObject.name}");
    }
}
