using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    private PlayerControls controls;
    
    public Vector2 PointerWorldPosition { get; private set; }

    public bool IsPointerDownThisFrame { get; private set; } 
    public bool IsPointerDown { get; private set; }          
    public bool IsExecuteTriggered { get; private set; }    
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
        
        IsPointerDownThisFrame = controls.Board.Click.WasPressedThisFrame();
        IsPointerDown = controls.Board.Click.IsPressed();                    
        IsExecuteTriggered = controls.Board.Click.WasReleasedThisFrame();    
        IsPauseTriggered = controls.Board.Pause.triggered; 
    }
}