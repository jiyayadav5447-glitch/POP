using System.Collections;
using UnityEngine;

/// <summary>
/// PUNCTURE &amp; DEFLATION (the core "brain" mechanic)
/// Tracks the character's air. When a Hazard (pin/spike) hits the character,
/// it punctures: air starts draining, movement/jump input is locked out
/// (CharacterMovement / LowGravityJump both check CurrentState), and random
/// escaping-air impulses send the character flying and bouncing
/// unpredictably. When air runs out (or the flight timer ends), the
/// character settles and control is handed back.
///
/// Attach to: Player character root (same object as Rigidbody).
/// Requires: Rigidbody. Works alongside CharacterMovement + LowGravityJump,
/// but has no hard dependency on either (uses GetComponent defensively).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PunctureDeflation : MonoBehaviour
{
    public enum State { Normal, Deflating, Recovering }

    [Header("Air")]
    [Tooltip("Max air / starting air amount")]
    public float maxAir = 100f;

    [Tooltip("Air lost per second while deflating")]
    public float airLossPerSecond = 40f;

    [Range(0f, 1f)]
    [Tooltip("Air % below which the character is considered fully popped " +
             "(you could trigger a respawn instead of just recovering)")]
    public float poppedThreshold = 0.05f;

    [Header("Deflation Flight")]
    [Tooltip("Minimum time (sec) the character stays in the uncontrollable flight state")]
    public float minFlightTime = 1.2f;

    [Tooltip("Maximum time (sec) the flight state can last if air keeps draining")]
    public float maxFlightTime = 3.5f;

    [Tooltip("How strong each random escaping-air impulse is")]
    public float impulseStrength = 8f;

    [Tooltip("How often (sec) a new random impulse is applied while deflating")]
    public float impulseInterval = 0.25f;

    [Tooltip("Upward bias added to every impulse so the character doesn't just skid on the floor")]
    public float upwardBias = 0.5f;

    [Header("Recovery")]
    [Tooltip("Seconds of settling time on the ground before control is fully returned")]
    public float recoveryTime = 0.4f;

    [Tooltip("Air refilled on recovery (0 = stays deflated until a pickup/respawn refills it)")]
    public float airRefillOnRecover = 100f;

    public State CurrentState { get; private set; } = State.Normal;
    public float CurrentAir { get; private set; }
    public float AirPercent => CurrentAir / maxAir;

    // ---- Events other systems (audio, VFX, UI) can subscribe to ----
    public System.Action OnPunctured;      // fired the instant a hazard hits
    public System.Action<Vector3, Vector3> OnPuncturedAt; // (worldHitPoint, worldHitNormal) — use this for the hole decal
    public System.Action OnDeflateTick;    // fired every impulse while flying (good for hiss/particles)
    public System.Action OnLanded;         // fired when flight ends and recovery begins
    public System.Action OnRecovered;      // fired when control is handed back
    public System.Action OnPopped;         // fired if air hits ~0 (full pop -> respawn hook)

    private Rigidbody _rb;
    private LowGravityJump _lowGravityJump;
    private Coroutine _deflateRoutine;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _lowGravityJump = GetComponent<LowGravityJump>();
        CurrentAir = maxAir;
    }

    /// <summary>Call this from a Hazard (pin/spike) when it touches the character.</summary>
    public void Puncture()
    {
        // No specific contact point known — use the character's own position as a fallback
        // so anything listening to OnPuncturedAt still gets a reasonable location.
        Puncture(transform.position, Vector3.up);
    }

    /// <summary>
    /// Call this variant when you have the actual contact point/normal (see HazardObject),
    /// so visual effects like PunctureHoleEffect can place the hole exactly where it was hit.
    /// </summary>
    public void Puncture(Vector3 worldHitPoint, Vector3 worldHitNormal)
    {
        if (CurrentState != State.Normal) return; // ignore repeat hits while already deflating

        CurrentState = State.Deflating;
        OnPunctured?.Invoke();
        OnPuncturedAt?.Invoke(worldHitPoint, worldHitNormal);

        if (_deflateRoutine != null) StopCoroutine(_deflateRoutine);
        _deflateRoutine = StartCoroutine(DeflationFlightRoutine());
    }

    private IEnumerator DeflationFlightRoutine()
    {
        float elapsed = 0f;
        float sinceLastImpulse = impulseInterval; // apply one immediately

        while (elapsed < maxFlightTime)
        {
            elapsed += Time.deltaTime;
            sinceLastImpulse += Time.deltaTime;
            CurrentAir -= airLossPerSecond * Time.deltaTime;
            CurrentAir = Mathf.Max(CurrentAir, 0f);

            if (sinceLastImpulse >= impulseInterval)
            {
                sinceLastImpulse = 0f;
                ApplyRandomEscapeImpulse();
                OnDeflateTick?.Invoke();
            }

            // Fully popped: cut the flight short and go straight to recovery/respawn hook.
            if (AirPercent <= poppedThreshold)
            {
                OnPopped?.Invoke();
                break;
            }

            // Once minimum flight time has passed, air is allowed to end the flight naturally
            // if it happens to run out; otherwise we ride it out to maxFlightTime.
            if (elapsed >= minFlightTime && CurrentAir <= 0f)
            {
                break;
            }

            yield return null;
        }

        yield return StartCoroutine(RecoverRoutine());
    }

    private void ApplyRandomEscapeImpulse()
    {
        // Random direction, biased slightly upward so it reads as "air blasting out"
        Vector3 randomDir = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0f, 1f) + upwardBias,
            Random.Range(-1f, 1f)
        ).normalized;

        _rb.AddForce(randomDir * impulseStrength, ForceMode.VelocityChange);
    }

    private IEnumerator RecoverRoutine()
    {
        CurrentState = State.Recovering;
        OnLanded?.Invoke();

        // Kill residual chaotic velocity so the character doesn't slide off after landing.
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        if (_lowGravityJump != null) _lowGravityJump.ResetVerticalVelocity();

        yield return new WaitForSeconds(recoveryTime);

        CurrentAir = Mathf.Min(maxAir, CurrentAir + airRefillOnRecover);
        CurrentState = State.Normal;
        OnRecovered?.Invoke();
    }

    /// <summary>Call from a respawn/checkpoint system to fully reset the character.</summary>
    public void ResetAir()
    {
        if (_deflateRoutine != null) StopCoroutine(_deflateRoutine);
        CurrentAir = maxAir;
        CurrentState = State.Normal;
    }
}
