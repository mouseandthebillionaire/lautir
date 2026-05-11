using System.Collections;
using UnityEngine;
using Cycling74.RNBOTypes;

/// <summary>
/// Drives the RNBO patch: random phrase/density/melody, wait, then trigger playback.
/// Editor / standalone: native <see cref="LautirSynth8Handle"/>. WebGL: <see cref="RnboWebBridge"/> + JS export next to the build.
/// </summary>
public class MelodyScript : MonoBehaviour
{
    LautirSynth8Helper lautirSynthHelper;
    LautirSynth8Handle lautirSynthHandle;
    bool rnboAvailable;
    bool webInitStarted;

    // Must match the RNBO device / mixer "Instance Index" in the scene.
    const int instanceIndex = 1;

    // Native path: parameter indices resolved from patch metadata at startup.
    int phraseLengthParam;
    int noteDensityParam;
    int melodyParam;
    // Native path: tag for SendMessage() → RNBO inport (see patch inports).
    uint rnboReceiveInport;

    [SerializeField] float phraseLength = 16;
    [SerializeField] float noteDensity = 7;
    [SerializeField] float melody = 0;

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        rnboAvailable = false;
        return;
#else
        lautirSynthHelper = LautirSynth8Helper.FindById(instanceIndex);
        if (lautirSynthHelper == null)
        {
            Debug.LogError($"LautirSynth8Helper not found (instance id {instanceIndex}).");
            return;
        }

        lautirSynthHandle = lautirSynthHelper.Plugin;
        if (lautirSynthHandle == null)
        {
            Debug.LogError("RNBO plugin handle is null.");
            return;
        }

        if (!lautirSynthHandle.IsAvailable)
        {
            rnboAvailable = false;
            return;
        }

        rnboAvailable = true;
        phraseLengthParam = (int)(LautirSynth8Handle.GetParamIndexById("phrase_length") ?? 0);
        noteDensityParam = (int)(LautirSynth8Handle.GetParamIndexById("noteDensity") ?? 0);
        melodyParam = (int)(LautirSynth8Handle.GetParamIndexById("melody") ?? 0);
        rnboReceiveInport = LautirSynth8Handle.Tag("rnboReceive");
#endif
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            StartCoroutine(RandomizeMelody());
    }

    public IEnumerator RandomizeMelody()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // First Space: load RNBO.js + patch JSON from paths relative to the WebGL build root.
        if (!RnboWebBridge.IsReady() && !webInitStarted)
        {
            webInitStarted = true;
            RnboWebBridge.Init("LautirSynth/lautirSynth.export.json", "LautirSynth/dependencies.json");
        }

        float t0 = Time.realtimeSinceStartup;
        while (!RnboWebBridge.IsReady() && Time.realtimeSinceStartup - t0 < 2f)
            yield return null;

        rnboAvailable = RnboWebBridge.IsReady();
#endif

        // 1. Phrase length
        phraseLength = Random.Range(1, 16);
        if (rnboAvailable)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            RnboWebBridge.SetParamById("phrase_length", phraseLength);
#else
            lautirSynthHandle.SetParamValue(phraseLengthParam, phraseLength);
#endif
        }

        // 2. Note density
        noteDensity = Random.Range(1, 7);
        if (rnboAvailable)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            RnboWebBridge.SetParamById("noteDensity", noteDensity);
#else
            lautirSynthHandle.SetParamValue(noteDensityParam, noteDensity);
#endif
        }

        // 3. Melody index (0–27 in patch)
        melody = Random.Range(0, 27);
        if (rnboAvailable)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            RnboWebBridge.SetParamById("melody", melody);
#else
            lautirSynthHandle.SetParamValue(melodyParam, melody);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Web build: keep this log for quick verification in the browser console.
        if (rnboAvailable)
            Debug.Log($"{phraseLength}:{noteDensity}:{melody}");
        else
        {
            var err = RnboWebBridge.GetLastError();
            if (!string.IsNullOrEmpty(err))
                Debug.LogWarning(err);
        }
#endif

        yield return new WaitForSeconds(1f);

        // 4. Arm + trigger: inport tag must match the patch (e.g. rnboReceive).
        if (rnboAvailable)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            RnboWebBridge.SetParamById("begin", 1);
            RnboWebBridge.SendMessage("rnboReceive", 1);
#else
            lautirSynthHandle.SendMessage(rnboReceiveInport, 1);
#endif
        }
    }
}
