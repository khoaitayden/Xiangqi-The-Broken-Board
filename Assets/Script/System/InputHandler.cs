using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    // 1. The Singleton Instance
    public static InputHandler Instance { get; private set; }

    private PlayerControls controls;
    
    // Public properties
    public Vector2 MouseWorldPosition { get; private set; }
    public bool IsClickTriggered { get; private set; }
    public bool IsPauseTriggered { get; private set; }

    private void Awake()
    {
        // 2. Singleton initialization logic
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        
        Instance = this;
        
        // Optional: Keeps the input manager alive when switching levels
        DontDestroyOnLoad(this.gameObject); 

        controls = new PlayerControls();
    }

    private void OnEnable() => controls?.Enable();
    private void OnDisable() => controls?.Disable();

    private void Update()
    {
        if (controls == null) return;
        
        Vector2 screenPosition = controls.Board.PointerPosition.ReadValue<Vector2>();
        MouseWorldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
        
        IsClickTriggered = controls.Board.Click.triggered;
        IsPauseTriggered = controls.Board.Pause.triggered; // NEW
    }
}