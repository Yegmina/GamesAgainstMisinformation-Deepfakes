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

        controller.enabled = false;
        controller.InteractionPoint(activeInteraction.SitPoint);
    }

    private void HandleExit()
    {
        if (activeInteraction == null)
            return;

        activeInteraction.ExitInteraction();
        activeInteraction = null;
        controller.enabled = true;
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
