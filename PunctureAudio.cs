using UnityEngine;

/// <summary>
/// SOUND
/// Listens to PunctureDeflation's events and plays the matching sounds:
/// a sharp "POP" on puncture, a looping hiss while air is escaping, and a
/// soft landing sound on recovery. Keeps all audio logic out of the
/// gameplay script so sound designers can tweak this file independently.
///
/// Attach to: Same GameObject as PunctureDeflation.
/// Requires: PunctureDeflation on the same object, and two AudioSource
/// components assigned below (one for one-shots, one dedicated to the loop).
/// </summary>
[RequireComponent(typeof(PunctureDeflation))]
public class PunctureAudio : MonoBehaviour
{
    [Header("Clips")]
    public AudioClip popSound;
    public AudioClip hissLoop;
    public AudioClip landSound;
    public AudioClip recoveredSound;

    [Header("Audio Sources")]
    [Tooltip("Used for one-shot sounds: pop, land, recovered")]
    public AudioSource oneShotSource;

    [Tooltip("Dedicated looping source for the escaping-air hiss")]
    public AudioSource loopSource;

    private PunctureDeflation _puncture;

    private void Awake()
    {
        _puncture = GetComponent<PunctureDeflation>();

        if (oneShotSource == null || loopSource == null)
        {
            Debug.LogWarning($"{name}: PunctureAudio needs both oneShotSource and " +
                              "loopSource AudioSources assigned in the Inspector.");
        }

        if (loopSource != null)
        {
            loopSource.clip = hissLoop;
            loopSource.loop = true;
            loopSource.playOnAwake = false;
        }
    }

    private void OnEnable()
    {
        _puncture.OnPunctured += HandlePunctured;
        _puncture.OnLanded += HandleLanded;
        _puncture.OnRecovered += HandleRecovered;
    }

    private void OnDisable()
    {
        _puncture.OnPunctured -= HandlePunctured;
        _puncture.OnLanded -= HandleLanded;
        _puncture.OnRecovered -= HandleRecovered;
    }

    private void HandlePunctured()
    {
        PlayOneShot(popSound);
        if (loopSource != null && hissLoop != null && !loopSource.isPlaying)
        {
            loopSource.Play();
        }
    }

    private void HandleLanded()
    {
        if (loopSource != null) loopSource.Stop();
        PlayOneShot(landSound);
    }

    private void HandleRecovered()
    {
        PlayOneShot(recoveredSound);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || oneShotSource == null) return;
        oneShotSource.PlayOneShot(clip);
    }
}
