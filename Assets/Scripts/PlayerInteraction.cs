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

        if (activeInteraction.SmoothTransition)
        {
            uiController.ShowInteraction(" ");
            StartCoroutine(SmoothInteractionTransition(activeInteraction));
        }
        else
        {
            controller.InteractionPoint(activeInteraction.SitPoint);
            activeInteraction.Interact();
            uiController.ShowInteraction(" "); //activeInteraction.ExitText
        }
    }

    private IEnumerator SmoothInteractionTransition(Interactable interactable)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            controller.InteractionPoint(interactable.SitPoint);
            interactable.Interact();
            yield break;
        }

        Transform targetPoint = interactable.SitPoint;
        if (targetPoint == null)
        {
            controller.InteractionPoint(interactable.SitPoint);
            interactable.Interact();
            yield break;
        }

        Vector3 startPos = controller.transform.position;
        Quaternion startRot = controller.transform.rotation;
        Quaternion startCamRot = mainCamera.transform.localRotation;
        float startFov = mainCamera.fieldOfView;

        Vector3 targetPos = targetPoint.position;
        Quaternion targetRot = Quaternion.Euler(0, targetPoint.eulerAngles.y, 0);
        Quaternion targetCamRot = Quaternion.Euler(targetPoint.eulerAngles.x, 0, 0);
        float targetFov = 35f; // Zoom in to focus on the phone

        float elapsed = 0f;
        float duration = 0.8f; // Beautiful smooth transition duration

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            controller.transform.position = Vector3.Lerp(startPos, targetPos, easedProgress);
            controller.transform.rotation = Quaternion.Slerp(startRot, targetRot, easedProgress);
            mainCamera.transform.localRotation = Quaternion.Slerp(startCamRot, targetCamRot, easedProgress);
            mainCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, easedProgress);

            yield return null;
        }

        // Snap to exact values
        controller.transform.position = targetPos;
        controller.transform.rotation = targetRot;
        mainCamera.transform.localRotation = targetCamRot;
        mainCamera.fieldOfView = targetFov;

        // Finally, trigger the interaction
        interactable.Interact();
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
