using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private UIController uiController;

    private PlayerInputHandler playerInputHandler;
    private Interactable currentInteractable;

    private void Awake()
    {
        playerInputHandler = GetComponent<PlayerInputHandler>();
    }

    private void OnEnable()
    {
        if (playerInputHandler != null)
            playerInputHandler.OnInteract += HandleInteraction;
    }

    private void OnDisable()
    {
        if (playerInputHandler != null)
            playerInputHandler.OnInteract -= HandleInteraction;
    }

    private void HandleInteraction()
    {
        if (currentInteractable == null) return;

        currentInteractable.Interact();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Interactable interactable))
        {
            currentInteractable = interactable;
            uiController.ShowInteraction(interactable.InteractionText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out Interactable interactable) &&
            currentInteractable == interactable)
        {
            currentInteractable = null;
            uiController.HideInteraction();
        }
    }
}
