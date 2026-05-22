using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slow volume and pan LFOs on child <see cref="AudioSource"/> pad layers.
/// </summary>
public class PadManager : MonoBehaviour
{
    public AudioSource[] padNotes;

    // LFO phase state (rad/s from period in seconds).
    private float[] fluctuationSpeeds;
    private float[] panSpeeds;
    private float[] phaseOffsets;

    // Volume LFO
    public float minVolume = 0.2f;
    public float maxVolume = 1f;
    // Multiplies volume LFO rate only (not pan).
    public float volumeSpeedMultiplier = 1f;
    private float midVolume;
    private float ampVolume;

    // Pan LFO
    public float minPan = -0.75f;
    public float maxPan = 0.75f;
    private float midPan;
    private float ampPan;

    void Awake()
    {
        padNotes = GetComponentsInChildren<AudioSource>();
#if UNITY_WEBGL && !UNITY_EDITOR
        // PlayOnAwake runs before WebGL decodes clips → "length of sound which is not loaded yet".
        foreach (var src in padNotes)
            src.playOnAwake = false;
#endif
    }

    void Start()
    {
        StartCoroutine(StartPadsThenFluctuate());
    }

    IEnumerator StartPadsThenFluctuate()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        foreach (var src in padNotes)
        {
            var clip = src.clip;
            if (clip == null) continue;
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading)
                yield return null;
            if (!src.isPlaying)
                src.Play();
        }
#endif
        yield return Fluctuate();
    }

    private IEnumerator Fluctuate()
    {
        int n = padNotes.Length;
        fluctuationSpeeds = new float[n];
        panSpeeds = new float[n];
        phaseOffsets = new float[n];
        for (int i = 0; i < n; i++)
        {
            // Period 15–60 s → angular frequency 2π/T
            float period = Random.Range(15f, 60f);
            float panPeriod = Random.Range(15f, 60f);
            fluctuationSpeeds[i] = 2 * Mathf.PI / period;
            panSpeeds[i] = 2 * Mathf.PI / panPeriod;
            phaseOffsets[i] = Random.Range(0f, 2 * Mathf.PI);
        }

        midVolume = (minVolume + maxVolume) / 2f;
        ampVolume = (maxVolume - minVolume) / 2f;
        midPan = (minPan + maxPan) / 2f;
        ampPan = (maxPan - minPan) / 2f;
        while (true)
        {
            float t = Time.time;
            for (int i = 0; i < padNotes.Length; i++)
            {
                float volPhase = fluctuationSpeeds[i] * volumeSpeedMultiplier * t + phaseOffsets[i];
                float panPhase = panSpeeds[i] * t + phaseOffsets[i];
                padNotes[i].volume = midVolume + ampVolume * Mathf.Sin(volPhase);
                padNotes[i].panStereo = midPan + ampPan * Mathf.Sin(panPhase);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
}
