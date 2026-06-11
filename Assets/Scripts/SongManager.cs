using UnityEngine;
using System.Collections;
using UnityEngine.Audio;


public class SongManager : MonoBehaviour
{

// Initially a duplicate of AudioManager, but will be used to manage the song as a whole

#if !(UNITY_WEBGL && !UNITY_EDITOR)
    public AudioMixer mixer;
#endif
    public string padCutoffParameterName = "PadCutoff";

    // There are 6 stages of the music
    // Stage 0 is the default state, light ambient drone
    // Stage 1 is the more complex drone? Sets the tone for the entire piece?
    // Stage 2 is the first melody
    // Stage 3 is the bassline?
    // Stage 4 is the second melody
    // Stage 5 is I don't know
    // Stage 6 is the ending?
    public int songStage = 0;


    public MelodyScript[] melodyScripts;

    [Range(0.05f, 1f)] public float approachCurvePower = 0.2f;

    // Seconds between layering each stage in as we build up to the target.
    public float stageStepDelay = 8f;
    // Seconds to hold the full mix once the target stage is reached.
    public float holdDuration = 30f;
    // Seconds to fade everything out after the hold.
    public float fadeOutDuration = 5f;
    // Seconds to fade each melody's bus up from silence when it's triggered.
    public float melodyFadeInDuration = 1f;
    // Mixer exposed parameter (in dB) used to fade the whole mix out.
    public string masterVolumeParameterName = "MasterVolume";

    const float CutoffHzMin = 200f;
    const float CutoffHzMax = 5000f;
    const float SilenceDb = -80f;

    public static SongManager S;

    Coroutine _progression;

    void Awake()
    {
        S = this;

        if (melodyScripts == null || melodyScripts.Length == 0)
            melodyScripts = GetComponentsInChildren<MelodyScript>(true);
    }

    void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        if (mixer == null || GameManager.S == null || string.IsNullOrEmpty(padCutoffParameterName))
            return;

        float t = GameManager.S.GetAvailabilityHomeBlend(approachCurvePower);
        float hz = Mathf.Lerp(CutoffHzMin, CutoffHzMax, t);
        mixer.SetFloat(padCutoffParameterName, hz);
#endif
    }

    // Jump to a specific stage. Plays through every stage from 0 up to this one in order,
    // layering each in, then holds the full mix and fades out. Restarts if called again.
    public void SetStage(int stage)
    {
        if (stage == songStage) return;
        songStage = stage;

        StartCoroutine(StageProgression(stage));
    }

    // Build up stages 0 → targetStage one at a time, hold, then fade everything out.
    IEnumerator StageProgression(int targetStage)
    {
        SetMasterVolumeDb(0f); // full volume for the build-up

        for (int s = 0; s <= targetStage; s++)
        {
            ApplyStageStep(s);
            yield return new WaitForSeconds(stageStepDelay);
        }

        // Let the full mix play for a while...
        yield return new WaitForSeconds(holdDuration);

        // ...then fade out.
        yield return FadeOutMaster();

        _progression = null;
    }

    // The single new layer that each stage adds on top of the previous ones.
    void ApplyStageStep(int stage)
    {
        switch (stage)
        {
            case 0: // light ambient drone — the default bed, nothing to add
                break;
            case 1: // first melody
                TriggerMelody(0);
                break;
            case 2: // second melody
                TriggerMelody(1);
                break;
            case 3: // bassline
                TriggerMelody(2);
                break;
            case 4:
                TriggerMelody(3);
                break;
            case 5:
                TriggerMelody(4);
                break;
            case 6: // ending
                TriggerMelody(5);
                break;
        }
        Debug.Log($"[LAUTIR] Stage step {stage}");
    }

    // Fade the whole mix to silence over fadeOutDuration via the mixer's master volume (dB).
    IEnumerator FadeOutMaster()
    {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        if (mixer == null || string.IsNullOrEmpty(masterVolumeParameterName))
            yield break;

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float db = Mathf.Lerp(0f, SilenceDb, elapsed / fadeOutDuration);
            mixer.SetFloat(masterVolumeParameterName, db);
            yield return null;
        }
        mixer.SetFloat(masterVolumeParameterName, SilenceDb);
#else
        yield break;
#endif
    }

    void SetMasterVolumeDb(float db)
    {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        if (mixer != null && !string.IsNullOrEmpty(masterVolumeParameterName))
            mixer.SetFloat(masterVolumeParameterName, db);
#endif
    }

    public void TriggerMelody(int melodyNum)
    {
        if (melodyScripts == null) return;
        // for now the word is tied to the melody number 
        // word0 = melody0, word1 = melody1, etc
        string word = WordInputManager.S.words[melodyNum];
        Debug.Log("Triggering Melody " + melodyNum + " with word " + word);
        melodyScripts[melodyNum].ParseWord(word);
        melodyScripts[melodyNum].TriggerMelody();

        // Fade this melody's bus up from silence as it starts.
        StartCoroutine(RampMelodyVolume(melodyNum));

        // Tell the word display to display the word
        WordDisplay.S.DisplayWord(melodyNum);
    }

    // Ramp a melody's mixer bus from silence to full (dB) over melodyFadeInDuration.
    IEnumerator RampMelodyVolume(int melodyNum)
    {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        if (mixer == null) yield break;

        string param = "Melody" + melodyNum + "Volume";
        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, melodyFadeInDuration);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            mixer.SetFloat(param, Mathf.Lerp(SilenceDb, 0f, elapsed / dur));
            yield return null;
        }
        mixer.SetFloat(param, 0f);
#else
        yield break;
#endif
    }

}
