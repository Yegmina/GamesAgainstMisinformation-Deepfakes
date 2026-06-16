using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private UIController uiController;

    private PlayerInputHandler playerInputHandler;
    private PlayerController controller;

    private Interactable currentInteractable;
    private Interactable activeInteraction;
    private bool exitTransitioning;

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
        if (currentInteractable == null || activeInteraction != null || exitTransitioning) return;

        uiController.UnlockCursor();

        activeInteraction = currentInteractable;
        controller.enabled = false;
        controller.InteractionPoint(activeInteraction.SitPoint);
        activeInteraction.Interact();

        uiController.ShowInteraction(" "); //activeInteraction.ExitText
    }

    private void HandleExit()
    {
        if (activeInteraction == null || exitTransitioning)
            return;

        if (activeInteraction.OpensComputerOverlay)
        {
            StartCoroutine(ExitComputerInteraction());
            return;
        }

        CompleteExitInteraction();
    }

    private void CompleteExitInteraction()
    {
        uiController.LockCursor();

        activeInteraction.ExitInteraction();
        controller.enabled = true;

        uiController.ShowInteraction(activeInteraction.InteractionText);

        activeInteraction = null;
    }

    private IEnumerator ExitComputerInteraction()
    {
        exitTransitioning = true;
        Interactable exitingInteraction = activeInteraction;

        uiController.LockCursor();
        exitingInteraction.ExitInteraction();

        while (ComputerOverlayController.IsTransitioning)
        {
            yield return null;
        }

        controller.enabled = true;
        uiController.ShowInteraction(exitingInteraction.InteractionText);

        activeInteraction = null;
        exitTransitioning = false;
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
