using System.Collections;
using System.Linq;
using UnityEngine;

// Drives the RNBO patch via RnboWebBridge + JS export (WebGL).

public class MelodyScript : MonoBehaviour
{
    // Must match the RNBO device / mixer "Instance Index" in the scene.
    public int instanceIndex = 1;

    bool rnboAvailable;
    bool webInitStarted;

    public int phraseLength = 32;
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
        Application.streamingAssetsPath + "/LautirSynth/lautirSynth.export.json";

    static string WebDepsUrl =>
        Application.streamingAssetsPath + "/LautirSynth/dependencies.json";

    void Start()
    {
        rnboAvailable = false;
    }

    // Simple trigger that will randomize the melody
    public void TriggerMelody()
    {
        if (melodyRunning) return;

        RnboWebBridge.ResumeAudioOnUserGesture();
        if (!RnboWebBridge.IsReady(instanceIndex) && !webInitStarted)
        {
            webInitStarted = true;
            RnboWebBridge.Init(instanceIndex, WebPatcherUrl, WebDepsUrl);
        }

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
        int[] availablePhrases = new int[] { 4, 8, 16, 32 };
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
        if (!WordInputManager.IsPlayableWord(word)) return;
        this.letters = word.ToCharArray();

        RnboWebBridge.ResumeAudioOnUserGesture();
        if (!RnboWebBridge.IsReady(instanceIndex) && !webInitStarted)
        {
            webInitStarted = true;
            RnboWebBridge.Init(instanceIndex, WebPatcherUrl, WebDepsUrl);
        }

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
        int[] availablePhrases = new int[] { 32, 16, 8, 4 };
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

        // Delays: 5th letter vs 1st (left), 5th vs 2nd (right)
        int delayDistance = Mathf.Abs(char.ToLowerInvariant(letters[4]) - char.ToLowerInvariant(letters[0]));
        leftDelay = 100 + (delayDistance * 36);

        delayDistance = Mathf.Abs(char.ToLowerInvariant(letters[4]) - char.ToLowerInvariant(letters[1]));
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
                ? $"RNBO instance {instanceIndex} not ready (check StreamingAssets/LautirSynth URLs)."
                : $"RNBO instance {instanceIndex}: {err}");
        }

        if (rnboAvailable)
        {
            // Set Parameters
            RnboWebBridge.SetParamById(instanceIndex, "phrase_length", phraseLength);
            RnboWebBridge.SetParamById(instanceIndex, "noteDensity", noteDensity);
            RnboWebBridge.SetParamById(instanceIndex, "melody", melody);
            RnboWebBridge.SetParamById(instanceIndex, "timbre", timbre);
            RnboWebBridge.SetParamById(instanceIndex, "note", note);
            RnboWebBridge.SetParamById(instanceIndex, "leftDelay", leftDelay);
            RnboWebBridge.SetParamById(instanceIndex, "rightDelay", rightDelay);
            RnboWebBridge.SetParamById(instanceIndex, "feedback", feedback);

            Debug.Log($"[LAUTIR] Params set (instance {instanceIndex}): {phraseLength}:{noteDensity}:{melody}:{timbre}:{note}");

            // Wait for 1 second
            yield return new WaitForSeconds(1f);

            // Arm + trigger
            if (!RnboWebBridge.SetParamById(instanceIndex, "begin", 1))
                Debug.LogError($"[LAUTIR] SetParam begin failed (instance {instanceIndex})");
            if (!RnboWebBridge.SendMessage(instanceIndex, "rnboReceive", 1))
                Debug.LogError($"[LAUTIR] SendMessage rnboReceive failed (instance {instanceIndex})");
            else
                Debug.Log($"[LAUTIR] Trigger sent (instance {instanceIndex}) — check browser console for AudioContext=running");
        }
    }
}
