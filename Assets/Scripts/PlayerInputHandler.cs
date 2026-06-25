using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset playerControls;

    [Header("Action Name Map Reference")]
    [SerializeField] private string actionNameMap = "Player";

    [Header("Action Name References")]
    [SerializeField] private string movement = "Movement";
    [SerializeField] private string rotation = "Rotation";
    [SerializeField] private string interact = "Interact";
    [SerializeField] private string exit = "Exit";

    private InputAction movementAction;
    private InputAction rotationAction;
    private InputAction interactAction;
    private InputAction exitAction;

    public Vector2 MovementInput { get; private set; }
    public Vector2 RotationInput { get; private set; }

    public event System.Action OnInteract;
    public event System.Action OnExit;

    private void Start()
    {
        InputActionMap mapReference = playerControls.FindActionMap(actionNameMap);

        movementAction = mapReference.FindAction(movement);
        rotationAction = mapReference.FindAction(rotation);
        interactAction = mapReference.FindAction(interact);
        exitAction = mapReference.FindAction(exit);

        SubscribeActionValuesToINputEvents();
    }

    private void SubscribeActionValuesToINputEvents()
    {
        movementAction.performed += inputInfo => MovementInput = inputInfo.ReadValue<Vector2>();
        movementAction.canceled += inputInfo => MovementInput = Vector2.zero;

        rotationAction.performed += inputInfo => RotationInput = inputInfo.ReadValue<Vector2>();
        rotationAction.canceled += inputInfo => RotationInput = Vector2.zero;

        interactAction.performed += _ =>
        {
            Debug.Log("Input System: Button pressed!");
            OnInteract?.Invoke();
        };

        exitAction.performed += _ =>
        {
            Debug.Log("Input System: Exit pressed!");
            OnExit?.Invoke();
        };
    }

    private void OnEnable()
    {
        playerControls.FindActionMap(actionNameMap).Enable();
    }

    private void OnDisable()
    {
        playerControls.FindActionMap(actionNameMap).Disable();
    }
}
