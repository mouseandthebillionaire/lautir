using System.Collections;
using System.Linq;
using UnityEngine;

// Drives the combined RNBO patch in WebGL: random/word-derived params, wait, then trigger playback.
// WebGL only (RnboWebBridge + JS export); the native editor path has been removed.

public class InstrumentScript : MonoBehaviour
{
    // Manages every instrument in one single RNBO instance.

    // Key used by RnboWebBridge to identify this RNBO JS device. Arbitrary for a single patch;
    // only needs to be unique if multiple InstrumentScripts each load their own device.
    public int instanceIndex = 1;

    bool rnboAvailable;
    bool webInitStarted;

    // Optional prefix for nested patches (e.g. "melody_0/begin"). Leave empty for flat exports.
    public string instrumentName = "";

    public int phraseLength = 16;
    public int noteDensity = 7;
    public int melody = 0;
    public int timbre = 0; // 0 to 10000
    public int note = 2; // 2, 4, 8, 16
    public int leftDelay = 300;
    public int rightDelay = 400;
    public float feedback = 0.5f;

    public bool tapToTriggerOnMobile = true;

    public char[] letters;

    bool melodyRunning;

    const float WebInitTimeoutSeconds = 15f;

    static string WebPatcherUrl =>
        Application.streamingAssetsPath + "/LautirSong/lautirSong.export.json";

    static string WebDepsUrl =>
        Application.streamingAssetsPath + "/LautirSong/dependencies.json";

    string ParamId(string name) =>
        string.IsNullOrEmpty(instrumentName) ? name : $"{instrumentName}/{name}";

    bool SetParam(string paramId, float value)
    {
        if (RnboWebBridge.SetParamById(instanceIndex, paramId, value))
            return true;
        Debug.LogWarning($"[LAUTIR] SetParam failed: {paramId} = {value}");
        return false;
    }

    // Resume AudioContext on a user gesture and create the RNBO device (fires patch loadbang).
    void EnsureWebAudioReady()
    {
        RnboWebBridge.ResumeAudioOnUserGesture();
        if (RnboWebBridge.IsReady(instanceIndex) || webInitStarted)
            return;

        webInitStarted = true;
        Debug.Log($"[LAUTIR] Loading RNBO patch (instance {instanceIndex}) — loadbang runs when ready");
        RnboWebBridge.Init(instanceIndex, WebPatcherUrl, WebDepsUrl);
        StartCoroutine(BootstrapPlaybackAfterLoad());
    }

    // loadbang can fire before transport is running; re-assert transport + begin once the device exists.
    IEnumerator BootstrapPlaybackAfterLoad()
    {
        float t0 = Time.realtimeSinceStartup;
        while (!RnboWebBridge.IsReady(instanceIndex) && Time.realtimeSinceStartup - t0 < WebInitTimeoutSeconds)
            yield return null;

        if (!RnboWebBridge.IsReady(instanceIndex))
        {
            Debug.LogError($"[LAUTIR] Patch load timed out (instance {instanceIndex})");
            yield break;
        }

        // note param edge starts noteLogic; begin opens the delayed melody gate (see maxpat sel 1 → delay → gate).
        SetParam(ParamId("phrase_length"), phraseLength);
        SetParam(ParamId("noteDensity"), noteDensity);
        SetParam(ParamId("melody"), melody);
        SetParam(ParamId("note"), 0);
        yield return null;
        SetParam(ParamId("note"), note);
        SetParam(ParamId("begin"), 0);
        yield return null;
        SetParam(ParamId("begin"), 1);
        RnboWebBridge.SendMessage(instanceIndex, "rnboReceive", 1);

        yield return new WaitForSeconds(1.5f);
        SetParam(ParamId("note"), note <= 1 ? 2 : note - 1);
        yield return null;
        SetParam(ParamId("note"), note);
        SetParam(ParamId("begin"), 0);
        yield return null;
        SetParam(ParamId("begin"), 1);
        Debug.Log($"[LAUTIR] Playback bootstrapped (instance {instanceIndex})");
    }

    static bool FirstPointerDown()
    {
        if (Input.GetMouseButtonDown(0))
            return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            return true;
        return false;
    }

    void Update()
    {
        // Load the patch on first click/tap so the patcher's loadbang can start audio.
        if (!webInitStarted && FirstPointerDown())
            EnsureWebAudioReady();

        if (Input.GetKeyDown(KeyCode.Space))
            TriggerMelody();
    }

    // Simple trigger that will randomize the melody
    public void TriggerMelody()
    {
        if (melodyRunning) return;

        EnsureWebAudioReady();
        StartCoroutine(RandomizeMelodyWrapper());
    }

    IEnumerator RandomizeMelodyWrapper()
    {
        melodyRunning = true;
        yield return RandomizeMelody();
        melodyRunning = false;
    }

    public IEnumerator RandomizeMelody()
    {
        // First things first: set the values for all of these parameters
        // Set Phrase Length
        int[] availablePhrases = new int[] { 4, 8, 16, 24 };
        phraseLength = availablePhrases[Random.Range(0, availablePhrases.Length)];
        // Set Note Density
        noteDensity = Random.Range(1, 8);
        // Set Melody
        melody = Random.Range(0, 25);
        // Set Timbre
        timbre = Random.Range(0, 1000);
        // Set Note (RNBO param range 1–4)
        note = Random.Range(1, 5);
        // Set Left Delay
        leftDelay = Random.Range(100, 1000);
        // Set Right Delay
        rightDelay = Random.Range(100, 1000);

        // Play the melody
        yield return PlayMelody();
    }

    // More complicated trigger that will parse a word and trigger the melody
    public void ParseWord(string word)
    {
        if (word == null || word.Length == 0) return;
        this.letters = word.ToCharArray();

        EnsureWebAudioReady();
        StartCoroutine(WordToMelodyWrapper());
    }

    IEnumerator WordToMelodyWrapper()
    {
        melodyRunning = true;
        yield return WordToMelody();
        melodyRunning = false;
    }

    public IEnumerator WordToMelody()
    {
        // First things first: set the values for all of these parameters

        // Letter Commonality for a bunch of these
        char[] letterCommonality = new char[] { 'e', 't', 'a', 'o', 'i', 'n', 's', 'r', 'h', 'd', 'l', 'u', 'c', 'm', 'f', 'y', 'w', 'g', 'p', 'b', 'v', 'k', 'x', 'q', 'j', 'z' };

        // Set Phrase Length based on the first letter in the word
        int[] availablePhrases = new int[] { 24, 16, 8, 4 };
        // The most common letters happen more often, the least common happen less often
        int letterIndex = System.Array.IndexOf(letterCommonality, char.ToLowerInvariant(letters[0]));
        phraseLength = availablePhrases[letterIndex / 7];

        // Set Note Density based on the second letter of the word
        // The most common letters have the highest density, the least common have the lowest
        int densityIndex = System.Array.IndexOf(letterCommonality, char.ToLowerInvariant(letters[1]));
        noteDensity = densityIndex / 4;

        // Set Melody based on third letter of the word
        melody = char.ToLowerInvariant(letters[2]) - 'a';

        // Set Timbre based on 4th letters position along the alphabet
        timbre = (char.ToLowerInvariant(letters[3]) - 'a') * 40;

        // Set NoteLength (RNBO param range 1–4)
        // Should this also be applied to the second letter?
        // Does it make sense that a more dense melody should have shorter notes?
        // letterCommonality needs ot be flipped so that the most common letters get the shortest notes
        int noteIndex = System.Array.IndexOf(letterCommonality.Reverse().ToArray(), char.ToLowerInvariant(letters[1]));
        note = noteIndex / 7 + 1;

        // Set Left Delay based on distance between first and fifth letter?
        // Needs to be an absolute value
        int delayDistance = Mathf.Abs(char.ToLowerInvariant(letters[4]) - char.ToLowerInvariant(letters[0]));
        leftDelay = 100 + (delayDistance * 36);

        // Set Right Delay based on distance between second and sixth letter?
        // Needs to be an absolute value
        delayDistance = Mathf.Abs(char.ToLowerInvariant(letters[5]) - char.ToLowerInvariant(letters[1]));
        rightDelay = 100 + (delayDistance * 36);

        // Play the melody
        yield return PlayMelody();
    }

    private IEnumerator PlayMelody()
    {
        float t0 = Time.realtimeSinceStartup;
        while (!RnboWebBridge.IsReady(instanceIndex) && Time.realtimeSinceStartup - t0 < WebInitTimeoutSeconds)
            yield return null;

        rnboAvailable = RnboWebBridge.IsReady(instanceIndex);
        if (!rnboAvailable)
        {
            var err = RnboWebBridge.GetLastError(instanceIndex);
            Debug.LogError(string.IsNullOrEmpty(err)
                ? $"RNBO instance {instanceIndex} not ready (check StreamingAssets/LautirSong URLs)."
                : $"RNBO instance {instanceIndex}: {err}");
            yield break;
        }


        SetParam(ParamId("phrase_length"), phraseLength);
        SetParam(ParamId("noteDensity"), noteDensity);
        SetParam(ParamId("melody"), melody);
        SetParam(ParamId("timbre"), timbre);
        SetParam(ParamId("note"), note);
        SetParam(ParamId("leftDelay"), leftDelay);
        SetParam(ParamId("rightDelay"), rightDelay);
        SetParam(ParamId("feedback"), feedback);
        SetParam("limiterGain", 0f);

        Debug.Log($"[LAUTIR] Params set (instance {instanceIndex}): {phraseLength}:{noteDensity}:{melody}:{timbre}:{note}");

        yield return new WaitForSeconds(0.1f);

        SetParam(ParamId("begin"), 0);
        yield return null;
        SetParam(ParamId("begin"), 1);
        RnboWebBridge.SendMessage(instanceIndex, "rnboReceive", 1);

        Debug.Log($"[LAUTIR] Trigger sent (instance {instanceIndex})");
    }
}
