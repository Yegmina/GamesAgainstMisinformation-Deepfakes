using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private UIController uiController;

    private PlayerInputHandler playerInputHandler;

    private Interactable currentInteractable;
    private Interactable activeInteraction;

    private void Awake()
    {
        playerInputHandler = GetComponent<PlayerInputHandler>();
    }

    private void OnEnable()
    {
        if (playerInputHandler != null)
        {
            playerInputHandler.OnInteract += HandleInteraction;
            playerInputHandler.OnExit += HandleExit;
        }
    }

    private void OnDisable()
    {
        playerInputHandler.OnInteract -= HandleInteraction;
        playerInputHandler.OnExit -= HandleExit;
    }

    private void HandleInteraction()
    {
        if (currentInteractable == null) return;

        activeInteraction = currentInteractable;
        activeInteraction.Interact();

        GetComponent<PlayerController>().enabled = false; // Disable character controller to prevent movement while interacting
        transform.position = activeInteraction.SitPoint.position;
        transform.rotation = activeInteraction.SitPoint.rotation;

    }

    private void HandleExit()
    {
        if (activeInteraction == null)
            return;

        activeInteraction.ExitInteraction();
        activeInteraction = null;
        GetComponent<PlayerController>().enabled = true;
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
