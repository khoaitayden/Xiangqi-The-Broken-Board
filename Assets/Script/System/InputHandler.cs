using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    private PlayerControls controls;
    
    public Vector2 PointerWorldPosition { get; private set; }
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

        if (IsValidScreenPosition(screenPosition))
        {
            PointerWorldPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Camera.main.nearClipPlane));
        }

        IsPointerDown = controls.Board.Click.IsPressed();
        IsExecuteTriggered = controls.Board.Click.WasReleasedThisFrame();
        IsPauseTriggered = controls.Board.Pause.triggered;
    }
    private bool IsValidScreenPosition(Vector2 pos)
    {
        if (!float.IsFinite(pos.x) || !float.IsFinite(pos.y)) return false;
        if (pos == Vector2.zero) return false;

        // Must be within actual screen bounds
        return pos.x >= 0 && pos.x <= Screen.width &&
            pos.y >= 0 && pos.y <= Screen.height;
    }
}