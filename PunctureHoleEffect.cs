using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PUNCTURE HOLE VFX
/// Listens for PunctureDeflation.OnPuncturedAt and drops a small "hole" decal
/// on the character's skin at the exact hit point, parented to the character
/// so it rides along as it flies/bounces/rotates. Holes are cleared when the
/// character fully recovers (fresh balloon = no holes).
///
/// Attach to: Same GameObject as PunctureDeflation (the player root).
/// Requires: PunctureDeflation on the same object.
///
/// Setup:
/// - Easiest: assign `holePrefab` — any small flat mesh/quad with a dark
///   "hole" texture/material (a torn-rubber decal looks best). It gets
///   oriented to sit flush against the surface using the hit normal.
/// - Quick test without art: leave `holePrefab` empty and this script will
///   spawn a small black flattened sphere as a placeholder so you can verify
///   placement/behavior immediately, then swap in real art later.
/// </summary>
[RequireComponent(typeof(PunctureDeflation))]
public class PunctureHoleEffect : MonoBehaviour
{
    [Header("Hole Prefab")]
    [Tooltip("Small decal prefab to spawn at the hit point. Leave empty to use a placeholder.")]
    public GameObject holePrefab;

    [Tooltip("Diameter of the hole in world units")]
    public float holeSize = 0.15f;

    [Tooltip("How far to push the hole out along the surface normal, to avoid z-fighting with the skin")]
    public float surfaceOffset = 0.01f;

    [Header("Behavior")]
    [Tooltip("Max holes kept at once (oldest is removed first). Prevents clutter on repeated hits.")]
    public int maxHoles = 3;

    [Tooltip("Clear all holes when the character fully recovers (fresh balloon)")]
    public bool clearHolesOnRecover = true;

    private PunctureDeflation _puncture;
    private readonly List<GameObject> _activeHoles = new List<GameObject>();
    private Material _placeholderMat;

    private void Awake()
    {
        _puncture = GetComponent<PunctureDeflation>();
    }

    private void OnEnable()
    {
        _puncture.OnPuncturedAt += HandlePuncturedAt;
        if (clearHolesOnRecover) _puncture.OnRecovered += ClearAllHoles;
    }

    private void OnDisable()
    {
        _puncture.OnPuncturedAt -= HandlePuncturedAt;
        if (clearHolesOnRecover) _puncture.OnRecovered -= ClearAllHoles;
    }

    private void HandlePuncturedAt(Vector3 worldPoint, Vector3 worldNormal)
    {
        GameObject hole = holePrefab != null
            ? Instantiate(holePrefab)
            : CreatePlaceholderHole();

        // Sit it just off the surface, facing outward along the hit normal.
        hole.transform.position = worldPoint + worldNormal * surfaceOffset;
        hole.transform.rotation = Quaternion.LookRotation(worldNormal) * Quaternion.Euler(90f, 0f, 0f);
        hole.transform.localScale = Vector3.one * holeSize;

        // Slight random spin so repeated holes don't look identical/stamped.
        hole.transform.Rotate(0f, 0f, Random.Range(0f, 360f), Space.Self);

        // Parent to the character so the hole travels/rotates with it during the deflation flight.
        hole.transform.SetParent(transform, worldPositionStays: true);

        _activeHoles.Add(hole);
        if (_activeHoles.Count > maxHoles)
        {
            GameObject oldest = _activeHoles[0];
            _activeHoles.RemoveAt(0);
            if (oldest != null) Destroy(oldest);
        }
    }

    private void ClearAllHoles()
    {
        foreach (var hole in _activeHoles)
        {
            if (hole != null) Destroy(hole);
        }
        _activeHoles.Clear();
    }

    /// <summary>Placeholder decal so this works immediately with zero art assigned.</summary>
    private GameObject CreatePlaceholderHole()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>()); // decals shouldn't collide with anything
        go.transform.localScale = new Vector3(1f, 0.15f, 1f); // flatten into a disc-ish shape

        if (_placeholderMat == null)
        {
            // "Sprites/Default" renders unlit and flat-black reliably across render pipelines
            // for quick testing. Swap holePrefab in with real art for the final look.
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _placeholderMat = new Material(shader) { color = Color.black };
        }
        go.GetComponent<Renderer>().sharedMaterial = _placeholderMat;
        go.name = "PunctureHole_Placeholder";
        return go;
    }
}
