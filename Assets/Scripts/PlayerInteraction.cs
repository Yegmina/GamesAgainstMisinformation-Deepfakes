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

    // Saved player state before sitting down / focusing on an object (static to persist across scene changes)
    private static Vector3 savedPlayerPosition;
    private static Quaternion savedPlayerRotation;
    private static Quaternion savedCameraRotation;
    private static float savedCameraFov = 60f;
    private static bool wasInteractingWithPhone = false;

    private void Awake()
    {
        playerInputHandler = GetComponent<PlayerInputHandler>();
        controller = GetComponent<PlayerController>();
    }

    private void Start()
    {
        if (wasInteractingWithPhone)
        {
            wasInteractingWithPhone = false;
            StartCoroutine(RestoreFromPhoneTransition());
        }
    }

    private IEnumerator RestoreFromPhoneTransition()
    {
        // Prevent player from moving/looking around while transitioning
        exitTransitioning = true;
        controller.enabled = false;

        // Find the PhoneInteractable in the scene to get the phonePoint (SitPoint) reference
        PhoneInteractable phone = Object.FindFirstObjectByType<PhoneInteractable>();
        Transform phonePoint = (phone != null) ? phone.SitPoint : null;

        Camera mainCamera = Camera.main;
        if (mainCamera == null || phonePoint == null)
        {
            // Fallback: immediately snap player to their pre-interaction position
            controller.transform.position = savedPlayerPosition;
            controller.transform.rotation = savedPlayerRotation;
            if (mainCamera != null)
            {
                mainCamera.transform.localRotation = savedCameraRotation;
                mainCamera.fieldOfView = savedCameraFov;
            }
            controller.enabled = true;
            exitTransitioning = false;
            yield break;
        }

        // Instantly position player and camera to the phonePoint (zoom-in state) on load
        controller.transform.position = phonePoint.position;
        controller.transform.rotation = Quaternion.Euler(0, phonePoint.eulerAngles.y, 0);
        mainCamera.transform.localRotation = Quaternion.Euler(phonePoint.eulerAngles.x, 0, 0);
        mainCamera.fieldOfView = 35f; // Zoomed in FOV

        uiController.LockCursor();

        // Let the scene rendering frame settle for a moment before beginning the transition
        yield return null;

        float elapsed = 0f;
        float duration = 0.8f; // Smooth zoom out transition duration

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            controller.transform.position = Vector3.Lerp(phonePoint.position, savedPlayerPosition, easedProgress);
            controller.transform.rotation = Quaternion.Slerp(Quaternion.Euler(0, phonePoint.eulerAngles.y, 0), savedPlayerRotation, easedProgress);
            mainCamera.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(phonePoint.eulerAngles.x, 0, 0), savedCameraRotation, easedProgress);
            mainCamera.fieldOfView = Mathf.Lerp(35f, savedCameraFov, easedProgress);

            yield return null;
        }

        // Snap to exact saved values at the end of transition
        controller.transform.position = savedPlayerPosition;
        controller.transform.rotation = savedPlayerRotation;
        mainCamera.transform.localRotation = savedCameraRotation;
        mainCamera.fieldOfView = savedCameraFov;

        // Re-sync PlayerController's vertical rotation to the restored angle
        float restoredPitch = savedCameraRotation.eulerAngles.x;
        if (restoredPitch > 180f) restoredPitch -= 360f;
        controller.SetVerticalRotation(restoredPitch);

        controller.enabled = true;
        if (phone != null)
        {
            uiController.ShowInteraction(phone.InteractionText);
        }

        exitTransitioning = false;
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

        // Take a snapshot of the player's position and rotation before interacting
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            savedPlayerPosition = controller.transform.position;
            savedPlayerRotation = controller.transform.rotation;
            savedCameraRotation = mainCamera.transform.localRotation;
            savedCameraFov = mainCamera.fieldOfView;
        }
        else
        {
            savedPlayerPosition = controller.transform.position;
            savedPlayerRotation = controller.transform.rotation;
            savedCameraRotation = Quaternion.identity;
        }

        uiController.UnlockCursor();

        activeInteraction = currentInteractable;
        controller.enabled = false;

        if (activeInteraction.SmoothTransition)
        {
            wasInteractingWithPhone = true;
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

        Vector3 startPos = savedPlayerPosition;
        Quaternion startRot = savedPlayerRotation;
        Quaternion startCamRot = savedCameraRotation;
        float startFov = savedCameraFov;

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

        if (activeInteraction.SmoothTransition)
        {
            StartCoroutine(SmoothExitTransition());
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

    private IEnumerator SmoothExitTransition()
    {
        exitTransitioning = true;
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Vector3 startPos = controller.transform.position;
            Quaternion startRot = controller.transform.rotation;
            Quaternion startCamRot = mainCamera.transform.localRotation;
            float startFov = mainCamera.fieldOfView;

            float elapsed = 0f;
            float duration = 0.6f; // Smooth transition back duration

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

                controller.transform.position = Vector3.Lerp(startPos, savedPlayerPosition, easedProgress);
                controller.transform.rotation = Quaternion.Slerp(startRot, savedPlayerRotation, easedProgress);
                mainCamera.transform.localRotation = Quaternion.Slerp(startCamRot, savedCameraRotation, easedProgress);
                mainCamera.fieldOfView = Mathf.Lerp(startFov, savedCameraFov, easedProgress);

                yield return null;
            }

            // Snap to exact saved values
            controller.transform.position = savedPlayerPosition;
            controller.transform.rotation = savedPlayerRotation;
            mainCamera.transform.localRotation = savedCameraRotation;
            mainCamera.fieldOfView = savedCameraFov;

            // Re-sync PlayerController's vertical rotation to the restored angle
            float restoredPitch = savedCameraRotation.eulerAngles.x;
            if (restoredPitch > 180f) restoredPitch -= 360f;
            controller.SetVerticalRotation(restoredPitch);
        }

        CompleteExitInteraction();
        exitTransitioning = false;
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

        // Align player's gaze back straight forward (horizontally) so they are
        // not looking down at the empty monitor screen.
        controller.ResetGaze();

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

    public void GoToPhoneFromNotification(Interactable phoneInteractable)
    {
        if (exitTransitioning) return;

        // If we are currently interacting with something (like the computer)
        if (activeInteraction != null)
        {
            // If it is the computer, we need to exit it first and transition to the phone
            if (activeInteraction.OpensComputerOverlay)
            {
                StartCoroutine(TransitionFromComputerToPhone(activeInteraction, phoneInteractable));
                return;
            }
            else
            {
                // Just in case, exit any other interaction
                activeInteraction.ExitInteraction();
                activeInteraction = null;
            }
        }

        // Snapshot state for return
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            savedPlayerPosition = controller.transform.position;
            savedPlayerRotation = controller.transform.rotation;
            savedCameraRotation = mainCamera.transform.localRotation;
            savedCameraFov = mainCamera.fieldOfView;
        }
        else
        {
            savedPlayerPosition = controller.transform.position;
            savedPlayerRotation = controller.transform.rotation;
            savedCameraRotation = Quaternion.identity;
        }

        // Smooth transition directly from current apartment view to the phone
        activeInteraction = phoneInteractable;
        controller.enabled = false;
        wasInteractingWithPhone = true;
        uiController.ShowInteraction(" ");
        StartCoroutine(SmoothInteractionTransition(phoneInteractable));
    }

    private IEnumerator TransitionFromComputerToPhone(Interactable computerInteractable, Interactable phoneInteractable)
    {
        exitTransitioning = true;

        uiController.LockCursor();
        computerInteractable.ExitInteraction();

        while (ComputerOverlayController.IsTransitioning)
        {
            yield return null;
        }

        controller.ResetGaze();
        activeInteraction = null;
        exitTransitioning = false;

        // Take a snapshot from where we sit at the computer so we can return here
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            savedPlayerPosition = controller.transform.position;
            savedPlayerRotation = controller.transform.rotation;
            savedCameraRotation = mainCamera.transform.localRotation;
            savedCameraFov = mainCamera.fieldOfView;
        }

        // Start phone transition
        activeInteraction = phoneInteractable;
        controller.enabled = false;
        wasInteractingWithPhone = true;
        uiController.ShowInteraction(" ");
        yield return StartCoroutine(SmoothInteractionTransition(phoneInteractable));
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
