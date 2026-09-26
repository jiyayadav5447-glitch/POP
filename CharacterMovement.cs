using UnityEngine;

/// <summary>
/// MOVEMENT
/// Handles ground movement (run + turn) for the inflatable character.
/// This script ONLY moves the character. It does not jump, does not know
/// about gravity, and does not know about puncture/deflation directly —
/// it just checks "CanControl" before applying input, so PunctureDeflation
/// can lock it out while the character is flying/deflating.
///
/// Attach to: Player character root (same object as the Rigidbody).
/// Requires: Rigidbody (non-kinematic).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CharacterMovement : MonoBehaviour
{
    [Header("Move Settings")]
    [Tooltip("Top ground speed in units/sec")]
    public float moveSpeed = 6f;

    [Tooltip("How fast the character accelerates to top speed")]
    public float acceleration = 40f;

    [Tooltip("How fast the character decelerates when there's no input")]
    public float deceleration = 50f;

    [Tooltip("Degrees/sec the character rotates to face move direction")]
    public float turnSpeed = 720f;

    [Header("Input")]
    [Tooltip("If true, reads Horizontal/Vertical axes (keyboard/joystick). " +
             "If you're driving this from a mobile joystick UI instead, " +
             "leave this false and call SetMoveInput() from your joystick script.")]
    public bool useUnityInputAxes = true;

    // Current planar input, x = strafe, y = forward/back, expected range -1..1
    private Vector2 _moveInput;

    private Rigidbody _rb;
    private PunctureDeflation _puncture; // optional link, auto-found if present

    // External systems (PunctureDeflation) can flip this off to take control away
    public bool CanControl { get; set; } = true;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _puncture = GetComponent<PunctureDeflation>();
    }

    private void Update()
    {
        if (useUnityInputAxes)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _moveInput = new Vector2(h, v);
        }

        // Auto lock-out while punctured/deflating, if that script exists
        if (_puncture != null)
        {
            CanControl = _puncture.CurrentState == PunctureDeflation.State.Normal;
        }
    }

    private void FixedUpdate()
    {
        Vector3 targetVelocity = Vector3.zero;

        if (CanControl)
        {
            Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            // Move relative to world axes. Swap to camera-relative here if needed.
            targetVelocity = inputDir * moveSpeed;

            if (inputDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(inputDir, Vector3.up);
                _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
            }
        }

        // Only touch the horizontal velocity — vertical (Y) is owned by LowGravityJump
        Vector3 currentVel = _rb.linearVelocity;
        Vector3 currentPlanar = new Vector3(currentVel.x, 0f, currentVel.z);
        Vector3 targetPlanar = new Vector3(targetVelocity.x, 0f, targetVelocity.z);

        float rate = (targetPlanar.sqrMagnitude > currentPlanar.sqrMagnitude) ? acceleration : deceleration;
        Vector3 newPlanar = Vector3.MoveTowards(currentPlanar, targetPlanar, rate * Time.fixedDeltaTime);

        _rb.linearVelocity = new Vector3(newPlanar.x, currentVel.y, newPlanar.z);
    }

    /// <summary>Call this from a mobile on-screen joystick instead of keyboard axes.</summary>
    public void SetMoveInput(Vector2 input)
    {
        _moveInput = Vector2.ClampMagnitude(input, 1f);
    }
}
