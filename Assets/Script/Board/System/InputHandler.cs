using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    private PlayerControls controls;
    
    // Public properties that BoardManager can read easily
    public Vector2 MouseWorldPosition { get; private set; }
    public bool IsClickTriggered { get; private set; }

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Update()
    {
        // 1. Constantly update the mouse world position
        Vector2 screenPosition = controls.Board.PointerPosition.ReadValue<Vector2>();
        MouseWorldPosition = Camera.main.ScreenToWorldPoint(screenPosition);

        // 2. Track if the click was triggered exactly on this frame
        IsClickTriggered = controls.Board.Click.triggered;
    }
}