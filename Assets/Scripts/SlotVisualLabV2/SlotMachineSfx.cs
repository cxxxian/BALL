using System.Collections;
using UnityEngine;

/// <summary>Reusable, self-contained sound bank for the protocol slot machine.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class SlotMachineSfx : MonoBehaviour
{
    [Header("Shared icy-cyan synth palette")]
    [SerializeField] private AudioSource output;
    [Range(0f, 1f)] public float volume = .72f;
    public AudioClip hover, press, spinStart, reelStop, rollConfirm, specialReveal;
    public AudioClip[] reelTicks = new AudioClip[3];
    public AudioClip[] reelStops = new AudioClip[3];

    private Coroutine previewRoutine;

    private void Awake()
    {
        if (output == null) output = GetComponent<AudioSource>();
        output.playOnAwake = false;
        output.loop = false;
        output.spatialBlend = 0f;
    }

    public void PlayHover() => Play(hover, .42f);
    public void PlayPress() => Play(press, .64f);
    public void PlaySpinStart() => Play(spinStart, .7f);
    public void PlayReelTick(int reelIndex)
    {
        if (reelTicks == null || reelTicks.Length == 0) return;
        Play(reelTicks[Mathf.Abs(reelIndex) % reelTicks.Length], .48f);
    }
    public void PlayReelStop(int reelIndex)
    {
        AudioClip variation = reelStops != null && reelStops.Length > 0 ? reelStops[Mathf.Abs(reelIndex) % reelStops.Length] : null;
        Play(variation != null ? variation : reelStop, .76f);
    }
    public void PlayRollConfirm() => Play(rollConfirm, .72f);
    public void PlaySpecialReveal() => Play(specialReveal, .72f);

    private void Play(AudioClip clip, float level)
    {
        if (clip != null && output != null)
            output.PlayOneShot(clip, Mathf.Clamp01(volume * level));
    }

    public void PreviewFullSet()
    {
        if (!isActiveAndEnabled || previewRoutine != null) return;
        previewRoutine = StartCoroutine(PreviewSequence());
    }

    private IEnumerator PreviewSequence()
    {
        PlayHover(); yield return new WaitForSecondsRealtime(.25f);
        PlayPress(); yield return new WaitForSecondsRealtime(.28f);
        PlaySpinStart(); yield return new WaitForSecondsRealtime(.38f);
        PlayReelTick(0); yield return new WaitForSecondsRealtime(.18f);
        PlayReelTick(1); yield return new WaitForSecondsRealtime(.18f);
        PlayReelTick(2); yield return new WaitForSecondsRealtime(.25f);
        PlayReelStop(0); yield return new WaitForSecondsRealtime(.24f);
        PlayReelStop(1); yield return new WaitForSecondsRealtime(.24f);
        PlayReelStop(2); yield return new WaitForSecondsRealtime(.34f);
        PlayRollConfirm(); yield return new WaitForSecondsRealtime(.62f);
        PlaySpecialReveal();
        previewRoutine = null;
    }

    private void OnDisable()
    {
        if (previewRoutine != null) StopCoroutine(previewRoutine);
        previewRoutine = null;
    }
}
