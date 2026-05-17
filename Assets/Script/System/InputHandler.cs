using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    private PlayerControls controls;
    
    public Vector2 PointerWorldPosition { get; private set; }
    
    // Abstracted Input Triggers
    public bool IsPointerDownThisFrame { get; private set; } // NEW: Detects initial touch/click
    public bool IsPointerDown { get; private set; }          // Held down
    public bool IsExecuteTriggered { get; private set; }     // Released
    public bool IsPauseTriggered { get; private set; } 

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        controls = new PlayerControls();
    }

    private void OnEnable() => controls?.Enable();
    private void OnDisable() => controls?.Disable();

    private void Update()
    {
        if (controls == null) return;
        
        Vector2 screenPosition = controls.Board.PointerPosition.ReadValue<Vector2>();
        if (screenPosition != Vector2.zero) 
        {
            PointerWorldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
        }
        
        IsPointerDownThisFrame = controls.Board.Click.WasPressedThisFrame(); // Initial Touch
        IsPointerDown = controls.Board.Click.IsPressed();                    // Dragging/Holding
        IsExecuteTriggered = controls.Board.Click.WasReleasedThisFrame();    // Lifting finger
        IsPauseTriggered = controls.Board.Pause.triggered; 
    }
}