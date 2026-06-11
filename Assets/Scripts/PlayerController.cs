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
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float upDownLookRange = 80f;

    [Header("Look Settings")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlayerInputHandler playerInputHandler;

    private Vector3 currentMovement;
    private float verticalRotation;
    private float currentSpeed => moveSpeed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    /*void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }*/

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
        transform.Rotate(0, rotationAmount, 0);
    }

    private void ApplyVerticalRotation(float rotationAmount)
    {
        verticalRotation = Mathf.Clamp(verticalRotation - rotationAmount, -upDownLookRange, upDownLookRange);
        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    private void HandleRotation()
    {
        float mouseXRotation = playerInputHandler.RotationInput.x * mouseSensitivity;
        float mouseYRotation = playerInputHandler.RotationInput.y * mouseSensitivity;

        ApplyHorizontalRotation(mouseXRotation);
        ApplyVerticalRotation(mouseYRotation);
    }

    public void InteractionPoint(Transform point)
    {
        transform.position = point.position;
        transform.rotation = Quaternion.Euler(0, point.eulerAngles.y, 0);

        verticalRotation = point.eulerAngles.x;
        mainCamera.transform.localRotation =
            Quaternion.Euler(verticalRotation, 0, 0);
    }
}
