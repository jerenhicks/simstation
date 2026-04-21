using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Free-roam camera for testing SimStation.
/// Uses the new Unity Input System (compatible with "Input System Package" player settings).
///
/// Controls:
///   Right-click + drag   → rotate / tilt (yaw & pitch)
///   W / S                → move forward / back
///   A / D                → strafe left / right
///   Q / E                → move down / up
///   Scroll wheel         → dolly forward / back (fast)
///   Middle-mouse drag    → pan (slide the view)
///   Hold Shift           → 3× speed boost on all movement
/// </summary>
public class FreeCameraController : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Mouse-look sensitivity — lower this if rotation feels jittery (try 0.05–0.1)")]
    public float lookSensitivity = 0.08f;

    [Tooltip("Smoothing applied to mouse delta (0 = none, higher = smoother but slightly laggy)")]
    [Range(0f, 0.9f)]
    public float lookSmoothing = 0.2f;

    [Header("Move")]
    [Tooltip("WASD / QE move speed (units per second)")]
    public float moveSpeed = 10f;

    [Tooltip("Scroll-wheel dolly speed multiplier")]
    public float scrollSpeed = 5f;

    [Tooltip("Middle-mouse pan speed")]
    public float panSpeed = 0.05f;

    [Tooltip("Hold Shift for this speed multiplier")]
    public float shiftMultiplier = 3f;

    [Header("Pitch Clamp")]
    public float minPitch = -80f;
    public float maxPitch =  80f;

    // ── Private state ────────────────────────────────────────────────────────
    float   _yaw;
    float   _pitch;
    Vector2 _smoothedDelta;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        _yaw   = angles.y;
        _pitch = angles.x;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse    = Mouse.current;

        if (keyboard == null || mouse == null) return;

        bool  shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        float speed = moveSpeed * (shift ? shiftMultiplier : 1f);

        // ── Right-click drag → look ───────────────────────────────────────────
        if (mouse.rightButton.isPressed)
        {
            Vector2 rawDelta  = mouse.delta.ReadValue();
            float   t         = 1f - lookSmoothing; // 1 = no smoothing, ~0 = very smooth
            _smoothedDelta    = Vector2.Lerp(_smoothedDelta, rawDelta, t);
            _yaw   +=  _smoothedDelta.x * lookSensitivity;
            _pitch -=  _smoothedDelta.y * lookSensitivity;
            _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
        else
        {
            // Drain the smoothed delta when the button is released so there's
            // no "drift" carry-over the next time right-click is pressed.
            _smoothedDelta = Vector2.zero;
        }

        // ── WASD / QE → move ─────────────────────────────────────────────────
        Vector3 dir = Vector3.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)    dir += transform.forward;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)  dir -= transform.forward;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) dir += transform.right;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)  dir -= transform.right;
        if (keyboard.eKey.isPressed)                                      dir += Vector3.up;
        if (keyboard.qKey.isPressed)                                      dir -= Vector3.up;

        if (dir.sqrMagnitude > 0.001f)
            transform.position += dir.normalized * speed * Time.deltaTime;

        // ── Scroll wheel → dolly ─────────────────────────────────────────────
        Vector2 scroll = mouse.scroll.ReadValue();
        if (Mathf.Abs(scroll.y) > 0.001f)
        {
            transform.position += transform.forward * (scroll.y * 0.01f) * scrollSpeed * speed;
        }

        // ── Middle-mouse drag → pan ───────────────────────────────────────────
        if (mouse.middleButton.isPressed)
        {
            Vector2 delta   = mouse.delta.ReadValue();
            float   panMult = panSpeed * (shift ? shiftMultiplier : 1f);
            transform.position -= transform.right * delta.x * panMult;
            transform.position -= transform.up    * delta.y * panMult;
        }
    }
}
