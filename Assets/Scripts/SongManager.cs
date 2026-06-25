using UnityEngine;
using System.Collections;
using UnityEngine.Audio;


public class SongManager : MonoBehaviour
{

// Initially a duplicate of AudioManager, but will be used to manage the song as a whole

    public AudioMixer mixer;
    public string padCutoffParameterName = "PadCutoff";

    // Five saved words → song stages 0–5 (drone, chime, bass, three melodies).
    // Stage 0 is the default state, light ambient drone
    // Stage 1 is chime + pad (word 0)
    // Stage 2 is the bassline (word 1)
    // Stage 3 is the first melody (word 2)
    // Stage 4 is the second melody (word 3)
    // Stage 5 is the third melody (word 4)
    public int songStage = 0;
    public int stageToTest;


    public InstrumentScript[] instrumentScripts;
    public ChimeAndPadScript chimeAndPadScript;
    public BassScript bassScript;

    [Range(0.05f, 1f)] public float approachCurvePower = 0.2f;

    // Each layer fades in/out over this many bars; waits layerPlayBars at full level between steps.
    public int layerFadeBars = 2;
    public int layerPlayBars = 2;
    // Hold at full build before teardown — index = target stage (0 drone … 5 full song).
    public int[] holdBarsByTargetStage = { 0, 2, 4, 8, 12, 16 };
    [Tooltip("If true, layers fade out in build order (chime first). If false, last layer in fades first.")]
    public bool fadeOutInBuildOrder = false;
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

    // Build 0 → targetStage, hold, then peel layers off back to stage 0. Restarts if called again.
    public void SetStage(int stage)
    {
        RnboWebBridge.ResumeAudioOnUserGesture();

        if (_progression != null)
            StopCoroutine(_progression);

        _progression = StartCoroutine(StageProgression(Mathf.Clamp(stage, 0, WordInputManager.SavedWordDaysCount)));
    }

    // Build up, hold at the target, then peel layers off (order set by fadeOutInBuildOrder).
    IEnumerator StageProgression(int targetStage)
    {
        SetMasterVolumeDb(0f);
        for (int s = 0; s <= targetStage; s++)
        {
            songStage = s;
            yield return ApplyStageStep(s);
            if (s >= 1 && s < targetStage)
                yield return new WaitForSeconds(BarSeconds(layerPlayBars));
        }

        yield return new WaitForSeconds(BarSeconds(HoldBarsForTargetStage(targetStage)));

        if (fadeOutInBuildOrder)
        {
            for (int s = 1; s <= targetStage; s++)
            {
                yield return RemoveStageStep(s);
                songStage = targetStage - s;
                if (s < targetStage)
                    yield return new WaitForSeconds(BarSeconds(layerPlayBars));
            }
        }
        else
        {
            for (int s = targetStage; s >= 1; s--)
            {
                yield return RemoveStageStep(s);
                songStage = s - 1;
                if (s > 1)
                    yield return new WaitForSeconds(BarSeconds(layerPlayBars));
            }
        }

        songStage = 0;
        WordDisplay.S.FadeOutWordDisplay();

        _progression = null;
    }

    float BarSeconds(int bars) => GlobalVariables.BarDurationSeconds(bars);
    float FadeSeconds => BarSeconds(layerFadeBars);

    int HoldBarsForTargetStage(int targetStage)
    {
        if (holdBarsByTargetStage == null || holdBarsByTargetStage.Length == 0)
            return 16;
        int i = Mathf.Clamp(targetStage, 0, holdBarsByTargetStage.Length - 1);
        return Mathf.Max(0, holdBarsByTargetStage[i]);
    }

    IEnumerator ApplyStageStep(int stage)
    {
        switch (stage)
        {
            case 0:
                WordDisplay.S.FadeInWordDisplay();
                // 2 seconds to match word display fade in duration
                yield return new WaitForSeconds(2f);
                break;
            case 1:
                WordDisplay.S.DisplayWord(0);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(0)))
                {
                    if (chimeAndPadScript != null)
                        yield return chimeAndPadScript.ArmChimeFromSavedWord();
                    yield return FadeChimeIn();
                }
                Debug.Log("Chime and pad loaded");
                break;
            case 2:
                WordDisplay.S.DisplayWord(1);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(1)))
                {
                    TriggerBassline(1);
                    yield return new WaitForSeconds(0.1f);
                    yield return FadeBassIn();
                }
                Debug.Log("Bassline triggered");
                break;
            case 3:
                WordDisplay.S.DisplayWord(2);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(2)))
                {
                    TriggerMelody(0);
                    yield return new WaitForSeconds(0.1f);
                    yield return RampMelodyVolumeUp(0);
                }
                Debug.Log("First melody triggered");
                break;
            case 4:
                WordDisplay.S.DisplayWord(3);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(3)))
                {
                    TriggerMelody(1);
                    yield return new WaitForSeconds(0.1f);
                    yield return RampMelodyVolumeUp(1);
                }
                Debug.Log("Second melody triggered");
                break;
            case 5:
                WordDisplay.S.DisplayWord(4);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(4)))
                {
                    TriggerMelody(2);
                    yield return new WaitForSeconds(0.1f);
                    yield return RampMelodyVolumeUp(2);
                }
                Debug.Log("Third melody triggered");
                break;
        }
    }

    IEnumerator RemoveStageStep(int stage)
    {
        switch (stage)
        {
            case 0:
                break;
            case 1:
                WordDisplay.S.ClearWord(0);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(0)))
                    yield return FadeChimeAndPad();
                Debug.Log("Chime and pad removed");
                break;
            case 2:
                WordDisplay.S.ClearWord(1);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(1)))
                    yield return FadeBass();
                Debug.Log("Bassline removed");
                break;
            case 3:
                WordDisplay.S.ClearWord(2);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(2)))
                    yield return RampMelodyVolumeDown(0);
                Debug.Log("First melody removed");
                break;
            case 4:
                WordDisplay.S.ClearWord(3);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(3)))
                    yield return RampMelodyVolumeDown(1);
                Debug.Log("Second melody removed");
                break;
            case 5:
                WordDisplay.S.ClearWord(4);
                if (WordInputManager.IsPlayableWord(WordInputManager.GetWordAt(4)))
                    yield return RampMelodyVolumeDown(2);
                break;
        }
    }

    IEnumerator FadeChimeIn()
    {
        const int instance = 1;
        float elapsed = 0f;
        float dur = FadeSeconds;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            RnboWebBridge.SetParamById(instance, "chime_and_pad/chimeVolume", Mathf.Lerp(0f, 1f, elapsed / dur));
            yield return null;
        }
        RnboWebBridge.SetParamById(instance, "chime_and_pad/chimeVolume", 1f);
    }

    IEnumerator FadeBassIn()
    {
        const int instance = 1;
        const float target = 0.15f;
        float elapsed = 0f;
        float dur = FadeSeconds;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            RnboWebBridge.SetParamById(instance, "bass/volume", Mathf.Lerp(0f, target, elapsed / dur));
            yield return null;
        }
        RnboWebBridge.SetParamById(instance, "bass/volume", target);
    }

    IEnumerator FadeChimeAndPad()
    {
        chimeAndPadScript?.StopPadRamp();

        const int instance = 1;
        float elapsed = 0f;
        float dur = FadeSeconds;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float v = Mathf.Lerp(1f, 0f, elapsed / dur);
            RnboWebBridge.SetParamById(instance, "chime_and_pad/chimeVolume", v);
            RnboWebBridge.SetParamById(instance, "chime_and_pad/padVolume", v);
            yield return null;
        }
        RnboWebBridge.SetParamById(instance, "chime_and_pad/chimeVolume", 0f);
        RnboWebBridge.SetParamById(instance, "chime_and_pad/padVolume", 0f);
        RnboWebBridge.SetParamById(instance, "chime_and_pad/begin", 0f);
    }

    IEnumerator FadeBass()
    {
        const int instance = 1;
        const float from = 0.15f;
        float elapsed = 0f;
        float dur = FadeSeconds;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            RnboWebBridge.SetParamById(instance, "bass/volume", Mathf.Lerp(from, 0f, elapsed / dur));
            yield return null;
        }
        RnboWebBridge.SetParamById(instance, "bass/volume", 0f);
        RnboWebBridge.SetParamById(instance, "bass/begin", 0f);
    }

    IEnumerator RampMelodyVolumeUp(int melodyNum)
    {
        const int instance = 1;
        string instrument = GetMelodyInstrumentName(melodyNum);
        if (string.IsNullOrEmpty(instrument)) yield break;

        float elapsed = 0f;
        float dur = FadeSeconds;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            RnboWebBridge.SetParamById(instance, instrument + "/volume", Mathf.Lerp(0f, 1f, elapsed / dur));
            yield return null;
        }
        RnboWebBridge.SetParamById(instance, instrument + "/volume", 1f);
    }

    IEnumerator RampMelodyVolumeDown(int melodyNum)
    {
        const int instance = 1;
        string instrument = GetMelodyInstrumentName(melodyNum);
        if (string.IsNullOrEmpty(instrument)) yield break;

        float elapsed = 0f;
        float dur = FadeSeconds;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            RnboWebBridge.SetParamById(instance, instrument + "/volume", Mathf.Lerp(1f, 0f, elapsed / dur));
            yield return null;
        }
        RnboWebBridge.SetParamById(instance, instrument + "/volume", 0f);
        RnboWebBridge.SetParamById(instance, instrument + "/begin", 0f);
    }

    string GetMelodyInstrumentName(int melodyNum)
    {
        if (instrumentScripts != null && melodyNum >= 0 && melodyNum < instrumentScripts.Length
            && instrumentScripts[melodyNum] != null
            && !string.IsNullOrEmpty(instrumentScripts[melodyNum].instrumentName))
            return instrumentScripts[melodyNum].instrumentName;

        return "melody_" + melodyNum;
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

    public void TriggerBassline(int _wordIndex)
    {
        if (bassScript == null) return;
        int wordIndex = _wordIndex;
        string word = WordInputManager.GetWordAt(wordIndex);
        if (!WordInputManager.IsPlayableWord(word))
        {
            Debug.Log($"[LAUTIR] Skipping bass at index {wordIndex} (missed day).");
            return;
        }

        Debug.Log("Triggering Bassline with word " + word);
        bassScript.ParseWord(word);
    }

    public void TriggerMelody(int melodyNum)
    {
        if (instrumentScripts == null) return;

        int wordIndex = melodyNum + 2;
        string word = WordInputManager.GetWordAt(wordIndex);
        if (!WordInputManager.IsPlayableWord(word))
        {
            Debug.Log($"[LAUTIR] Skipping melody {melodyNum} at index {wordIndex} (missed day).");
            return;
        }

        Debug.Log("Triggering Melody " + melodyNum + " with word " + word);
        instrumentScripts[melodyNum].ParseWord(word);
    }

}
