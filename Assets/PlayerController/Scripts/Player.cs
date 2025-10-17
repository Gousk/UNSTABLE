using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class Player : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController movement;
    [SerializeField] private PlayerCameraController cameraController;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool sprintHeld;
    private bool lookFromMouse;

    private void Reset()
    {
        movement = GetComponent<PlayerController>();
        cameraController = GetComponent<PlayerCameraController>();
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        SetupInputActions();
        EnableActions();
    }

    private void OnEnable() => EnableActions();
    private void OnDisable() => DisableActions();

    private void OnDestroy()
    {
        moveAction?.Dispose();
        lookAction?.Dispose();
        jumpAction?.Dispose();
        sprintAction?.Dispose();
    }

    private void SetupInputActions()
    {
        // Move
        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += _ => moveInput = Vector2.zero;

        // Look
        lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
        lookAction.AddBinding("<Mouse>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick");
        lookAction.performed += ctx =>
        {
            lookInput = ctx.ReadValue<Vector2>();
            lookFromMouse = ctx.control.device is Mouse;
        };
        lookAction.canceled += _ => lookInput = Vector2.zero;

        // Jump
        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
        jumpAction.started += _ => movement?.QueueJump();

        // Sprint (hold)
        sprintAction = new InputAction("Sprint", InputActionType.Button);
        sprintAction.AddBinding("<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");
        sprintAction.performed += _ => sprintHeld = true;
        sprintAction.canceled += _ => sprintHeld = false;
    }

    private void EnableActions()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
    }

    private void DisableActions()
    {
        moveAction?.Disable();
        lookAction?.Disable();
        jumpAction?.Disable();
        sprintAction?.Disable();
    }

    private void Update()
    {
        if (movement == null) return;
        movement.TickMovement(moveInput, sprintHeld, Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (cameraController == null) return;
        cameraController.TickLook(lookInput, lookFromMouse, Time.deltaTime);
    }
}
