using UnityEngine;

/// <summary>
/// LOW GRAVITY / HIGH JUMP
/// Owns vertical (Y) movement: jumping, custom low-gravity fall, and
/// grounded detection. Unity's default gravity is turned OFF for this
/// rigidbody (useGravity = false) and replaced with a custom, weaker
/// gravity so jumps feel big and floaty, matching the inflatable theme.
///
/// Attach to: Player character root (same object as Rigidbody).
/// Requires: Rigidbody (non-kinematic, useGravity gets disabled in Awake).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LowGravityJump : MonoBehaviour
{
    [Header("Gravity")]
    [Tooltip("Custom gravity strength applied instead of Physics.gravity. " +
             "Earth-like is ~9.81. Use something like 3-5 for floaty jumps.")]
    public float lowGravity = 4f;

    [Tooltip("Extra gravity multiplier applied while falling (not rising), " +
             "so jumps go up floaty but don't hang forever at the top.")]
    public float fallGravityMultiplier = 1.4f;

    [Tooltip("Extra gravity multiplier applied while rising if the jump " +
             "button is released early (short-hop support).")]
    public float lowJumpMultiplier = 2f;

    [Header("Jump")]
    [Tooltip("Initial upward velocity applied on jump")]
    public float jumpForce = 12f;

    [Tooltip("Max downward speed (terminal velocity) so falls don't get absurd")]
    public float maxFallSpeed = 18f;

    [Tooltip("Seconds after leaving ground the player can still jump (coyote time)")]
    public float coyoteTime = 0.12f;

    [Tooltip("Seconds a jump input is buffered before landing")]
    public float jumpBufferTime = 0.12f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.3f;
    public LayerMask groundLayer;

    public bool IsGrounded { get; private set; }

    private Rigidbody _rb;
    private PunctureDeflation _puncture;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private bool _jumpHeld;

    public bool CanJump { get; set; } = true;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _puncture = GetComponent<PunctureDeflation>();

        // We fully own vertical motion, so switch off Unity's built-in gravity.
        _rb.useGravity = false;

        if (groundCheck == null)
        {
            Debug.LogWarning($"{name}: LowGravityJump has no groundCheck assigned. " +
                              "Create an empty child at the character's feet and assign it.");
        }
    }

    private void Update()
    {
        // Lock out jumping while punctured/deflating — PunctureDeflation is in control then.
        if (_puncture != null)
        {
            CanJump = _puncture.CurrentState == PunctureDeflation.State.Normal;
        }

        CheckGrounded();

        // Coyote time bookkeeping
        if (IsGrounded) _coyoteTimer = coyoteTime;
        else _coyoteTimer -= Time.deltaTime;

        // Jump input + buffering
        if (Input.GetButtonDown("Jump")) _jumpBufferTimer = jumpBufferTime;
        else _jumpBufferTimer -= Time.deltaTime;

        _jumpHeld = Input.GetButton("Jump");

        if (CanJump && _jumpBufferTimer > 0f && _coyoteTimer > 0f)
        {
            DoJump();
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (_puncture != null && _puncture.CurrentState != PunctureDeflation.State.Normal)
        {
            // Deflation flight owns vertical velocity during that state.
            return;
        }

        Vector3 vel = _rb.linearVelocity;

        float g = lowGravity;
        if (vel.y < 0f)
        {
            g *= fallGravityMultiplier;               // fall a bit faster than you rose
        }
        else if (vel.y > 0f && !_jumpHeld)
        {
            g *= lowJumpMultiplier;                    // cut the jump short if button released
        }

        vel.y -= g * Time.fixedDeltaTime;
        vel.y = Mathf.Max(vel.y, -maxFallSpeed);

        _rb.linearVelocity = vel;
    }

    private void DoJump()
    {
        Vector3 vel = _rb.linearVelocity;
        vel.y = jumpForce;
        _rb.linearVelocity = vel;
    }

    private void CheckGrounded()
    {
        if (groundCheck == null)
        {
            IsGrounded = false;
            return;
        }
        IsGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
    }

    /// <summary>Called by PunctureDeflation once the character lands after a deflation flight.</summary>
    public void ResetVerticalVelocity()
    {
        Vector3 vel = _rb.linearVelocity;
        vel.y = 0f;
        _rb.linearVelocity = vel;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
