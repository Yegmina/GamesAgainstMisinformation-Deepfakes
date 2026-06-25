using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;

public class PlayerController : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private float moveSpeed = 3.0f;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = MouseLookSettings.DefaultSensitivity;
    [SerializeField] private float upDownLookRange = 80f;

    [Header("Look Settings")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlayerInputHandler playerInputHandler;

    private Vector3 currentMovement;
    private float verticalRotation;
    private float currentSpeed => moveSpeed;

    private float yaw;
    private bool invertHorizontalLook;
    private bool invertVerticalLook;
    private bool mouseLookSettingsInitialized;

    private bool rotationReady;

    private void OnEnable()
    {
        MouseLookSettings.Changed += HandleMouseLookSettingsChanged;
        SyncMouseLookSettings(true);
    }

    private void OnDisable()
    {
        MouseLookSettings.Changed -= HandleMouseLookSettingsChanged;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        verticalRotation = mainCamera.transform.localEulerAngles.x;

        if (verticalRotation > 180f)
            verticalRotation -= 360f;

        yaw = transform.eulerAngles.y;

        StartCoroutine(EnableRotationNextFrame());
    }

    private IEnumerator EnableRotationNextFrame()
    {
        yield return null; // Wait for the next frame
        rotationReady = true;
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    private Vector3 CalculateWorldDirection()
    {
        Vector3 inputDirection = new Vector3(playerInputHandler.MovementInput.x, 0f, playerInputHandler.MovementInput.y);
        Vector3 worldDirection = transform.TransformDirection(inputDirection);
        return worldDirection.normalized;
    }

    private void HandleMovement()
    {
        Vector3 worldDirection = CalculateWorldDirection();
        currentMovement.x = worldDirection.x * currentSpeed;
        currentMovement.z = worldDirection.z * currentSpeed;

        if (characterController.isGrounded)
        {
            currentMovement.y = -0.5f; // Small downward force to stay grounded
        }
        else
        {
            currentMovement.y += Physics.gravity.y * Time.deltaTime;
        }

        characterController.Move(currentMovement * Time.deltaTime);
    }

    private void ApplyHorizontalRotation(float rotationAmount)
    {
        yaw += rotationAmount;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void ApplyVerticalRotation(float rotationAmount)
    {
        verticalRotation = Mathf.Clamp(verticalRotation - rotationAmount, -upDownLookRange, upDownLookRange);
        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    private void HandleRotation()
    {
        if (!rotationReady)
            return;

        SyncMouseLookSettings(false);

        Vector2 rotationInput = playerInputHandler.RotationInput;
        if (invertHorizontalLook)
        {
            rotationInput.x = -rotationInput.x;
        }
        if (invertVerticalLook)
        {
            rotationInput.y = -rotationInput.y;
        }

        float mouseXRotation = rotationInput.x * mouseSensitivity;
        float mouseYRotation = rotationInput.y * mouseSensitivity;

        ApplyHorizontalRotation(mouseXRotation);
        ApplyVerticalRotation(mouseYRotation);
    }

    private void HandleMouseLookSettingsChanged()
    {
        SyncMouseLookSettings(true);
    }

    private void SyncMouseLookSettings(bool forceLog)
    {
        float newSensitivity = MouseLookSettings.Sensitivity;
        bool newInvertHorizontal = MouseLookSettings.InvertHorizontal;
        bool newInvertVertical = MouseLookSettings.InvertVertical;
        bool changed = !mouseLookSettingsInitialized
            || !Mathf.Approximately(mouseSensitivity, newSensitivity)
            || invertHorizontalLook != newInvertHorizontal
            || invertVerticalLook != newInvertVertical;

        mouseSensitivity = newSensitivity;
        invertHorizontalLook = newInvertHorizontal;
        invertVerticalLook = newInvertVertical;
        mouseLookSettingsInitialized = true;

        if (forceLog || changed)
        {
            Debug.Log($"[MouseLook] Applied to {name}: sensitivity={MouseLookSettings.FormatSensitivity(mouseSensitivity)} ({mouseSensitivity:0.00}), invertHorizontal={invertHorizontalLook}, invertVertical={invertVerticalLook}.");
        }
    }

    public void InteractionPoint(Transform point)
    {
        transform.position = point.position;
        transform.rotation = Quaternion.Euler(0, point.eulerAngles.y, 0);

        yaw = point.eulerAngles.y; //
        verticalRotation = point.eulerAngles.x;
        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    public void ResetGaze()
    {
        verticalRotation = 0f;
        if (mainCamera != null)
        {
            mainCamera.transform.localRotation = Quaternion.identity;
        }
    }

    public void SetVerticalRotation(float angle)
    {
        verticalRotation = Mathf.Clamp(angle, -upDownLookRange, upDownLookRange);
        if (mainCamera != null)
        {
            mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        }
    }
}
