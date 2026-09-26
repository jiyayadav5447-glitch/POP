using UnityEngine;

/// <summary>
/// HAZARD (pins, spikes, sharp walls)
/// Drop this on any sharp obstacle in the map. On touching a character with
/// a PunctureDeflation component, it triggers the puncture.
///
/// Setup: Collider on this object should have "Is Trigger" checked ON
/// (recommended, so pins don't physically block movement — they just pop you).
/// If you want pins to be solid AND puncture on contact, use OnCollisionEnter
/// instead (see commented alternative below) and leave Is Trigger OFF.
///
/// Attach to: Any pin/spike/hazard GameObject. Requires a Collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HazardObject : MonoBehaviour
{
    [Tooltip("Only objects with this tag get punctured. Tag your player root as 'Player'.")]
    public string targetTag = "Player";

    [Tooltip("Seconds this specific hazard is disabled after hitting someone, " +
             "to avoid re-triggering every physics frame on overlap. 0 = no cooldown needed for triggers.")]
    public float hitCooldown = 0.1f;

    private float _cooldownTimer;

    private void Reset()
    {
        // Make sure new HazardObjects default to trigger mode.
        GetComponent<Collider>().isTrigger = true;
    }

    private void Update()
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPuncture(other.gameObject);
    }

    // --- Alternative for solid (non-trigger) hazards ---
    // private void OnCollisionEnter(Collision collision)
    // {
    //     TryPuncture(collision.gameObject);
    // }

    private void TryPuncture(GameObject other)
    {
        if (_cooldownTimer > 0f) return;
        if (!other.CompareTag(targetTag)) return;

        PunctureDeflation puncture = other.GetComponent<PunctureDeflation>();
        if (puncture == null) return;

        // Trigger colliders don't give us a contact point like OnCollisionEnter does,
        // so approximate it: the closest point on the character's collider to this
        // hazard's position is roughly where the spike went in. The outward normal
        // is estimated as the direction from the character's center to that point,
        // which is good enough to sit a decal flush against a round/blobby character.
        Collider playerCollider = other.GetComponent<Collider>();
        Vector3 hitPoint = playerCollider != null
            ? playerCollider.ClosestPoint(transform.position)
            : other.transform.position;

        Vector3 hitNormal = (hitPoint - other.transform.position);
        hitNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;

        puncture.Puncture(hitPoint, hitNormal);
        _cooldownTimer = hitCooldown;
    }
}
