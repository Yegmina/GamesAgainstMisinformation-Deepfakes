using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private UIController uiController;

    private PlayerInputHandler playerInputHandler;
    private PlayerController controller;

    private Interactable currentInteractable;
    private Interactable activeInteraction;

    private void Awake()
    {
        playerInputHandler = GetComponent<PlayerInputHandler>();
        controller = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (playerInputHandler != null)
        {
            playerInputHandler.OnInteract += HandleInteraction;
            playerInputHandler.OnExit += HandleExit;
        }

        ComputerOverlayController.ReturnToApartmentRequested += HandleComputerReturnToApartment;
    }

    private void OnDisable()
    {
        if (playerInputHandler != null)
        {
            playerInputHandler.OnInteract -= HandleInteraction;
            playerInputHandler.OnExit -= HandleExit;
        }

        ComputerOverlayController.ReturnToApartmentRequested -= HandleComputerReturnToApartment;
    }

    private void HandleInteraction()
    {
        if (currentInteractable == null) return;

        uiController.UnlockCursor();

        activeInteraction = currentInteractable;
        controller.enabled = false;
        controller.InteractionPoint(activeInteraction.SitPoint);
        activeInteraction.Interact();

        uiController.ShowInteraction(" "); //activeInteraction.ExitText
    }

    private void HandleExit()
    {
        if (activeInteraction == null)
            return;

        uiController.LockCursor();

        activeInteraction.ExitInteraction();
        controller.enabled = true;

        uiController.ShowInteraction(activeInteraction.InteractionText);

        activeInteraction = null;
    }

    private void HandleComputerReturnToApartment()
    {
        if (activeInteraction != null)
        {
            HandleExit();
            return;
        }

        ComputerOverlayController.CloseComputer();
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
