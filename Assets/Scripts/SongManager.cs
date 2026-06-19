using UnityEngine;
using System.Collections;
using UnityEngine.Audio;


public class SongManager : MonoBehaviour
{

// Initially a duplicate of AudioManager, but will be used to manage the song as a whole

    public AudioMixer mixer;
    public string padCutoffParameterName = "PadCutoff";

    // There are 6 stages of the music
    // Stage 0 is the default state, light ambient drone
    // Stage 1 is the more complex pad and a chime? Sets the tone for the entire piece.
    // Stage 2 is the bassline
    // Stage 3 is the first melody
    // Stage 4 is the second melody
    // Stage 5 is I don't know
    // Stage 6 is the ending?
    public int songStage = 0;
    public int stageToTest;


    public InstrumentScript[] instrumentScripts;
    public ChimeAndPadScript chimeAndPadScript;
    public BassScript bassScript;

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

        if (instrumentScripts == null || instrumentScripts.Length == 0)
            instrumentScripts = GetComponentsInChildren<InstrumentScript>(true);

        if (chimeAndPadScript == null)
            chimeAndPadScript = GetComponentInChildren<ChimeAndPadScript>(true);

        if (bassScript == null)
            bassScript = GetComponentInChildren<BassScript>(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SetStage(stageToTest);

            Debug.Log("We are now on stage: " + stageToTest);
        }

        if (mixer == null || GameManager.S == null || string.IsNullOrEmpty(padCutoffParameterName))
            return;

        float t = GameManager.S.GetAvailabilityHomeBlend(approachCurvePower);
        float hz = Mathf.Lerp(CutoffHzMin, CutoffHzMax, t);
        mixer.SetFloat(padCutoffParameterName, hz);
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
            case 1: // chimes (and drone? maybe split this into a different stage)
                LoadChimeAndPad();
                Debug.Log("Chime and pad loaded");
                break;
            case 2: // bassline
                TriggerBassline(1);
                Debug.Log("Bassline triggered");
                break;
            case 3: // first melody
                TriggerMelody(0);
                Debug.Log("First melody triggered");
                break;
            case 4:
                TriggerMelody(1);
                Debug.Log("Second melody triggered");
                break;
            case 5:
                TriggerMelody(4);
                break;
            case 6: // ending
                TriggerMelody(5);
                break;
        }
        
    }

    // Fade the whole mix to silence over fadeOutDuration via the mixer's master volume (dB).
    IEnumerator FadeOutMaster()
    {
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
    }

    void SetMasterVolumeDb(float db)
    {
        if (mixer != null && !string.IsNullOrEmpty(masterVolumeParameterName))
            mixer.SetFloat(masterVolumeParameterName, db);
    }

    public void LoadChimeAndPad()
    {
        if (chimeAndPadScript == null) return;

        int idx = chimeAndPadScript.wordIndex;
        chimeAndPadScript.PlayFromSavedWord();
    }

    public void TriggerBassline(int basslineNum)
    {
        if (bassScript == null) return;
        // word 0 = chime/pad, word 1 = baseline, word 2+ = melodies
        int wordIndex = basslineNum + 1;
        if (WordInputManager.S == null || WordInputManager.S.words == null
            || wordIndex < 0 || wordIndex >= WordInputManager.S.words.Count)
            return;

        string word = (WordInputManager.S.words[wordIndex] ?? "").Trim().ToUpperInvariant();
        if (word.Length < 6)
        {
            Debug.LogWarning($"[LAUTIR] TriggerBassline: need a 6-letter word at index {wordIndex}, got \"{word}\".");
            return;
        }

        Debug.Log("Triggering Bassline " + basslineNum + " with word " + word);
        bassScript.ParseWord(word);
    }

    public void TriggerMelody(int melodyNum)
    {
        if (instrumentScripts == null) return;
        // for now the word is tied to the melody number 
        // word0 = melody0, word1 = melody1, etc
        string word = WordInputManager.S.words[melodyNum + 2];
        Debug.Log("Triggering Melody " + melodyNum + " with word " + word);
        instrumentScripts[melodyNum].ParseWord(word);

        // Fade this melody's bus up from silence as it starts.
        StartCoroutine(RampMelodyVolume(melodyNum));

        // Tell the word display to display the word
        // WordDisplay.S.DisplayWord(melodyNum);
    }

    // Ramp a melody's mixer bus from silence to full (dB) over melodyFadeInDuration.
    // Should this happen in each instrument's script rather than here?
    IEnumerator RampMelodyVolume(int melodyNum)
    {
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
    }

}
