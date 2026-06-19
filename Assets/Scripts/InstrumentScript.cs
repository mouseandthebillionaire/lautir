using System.Collections;
using System.Linq;
using UnityEngine;

// Drives the combined RNBO patch via RnboWebBridge + JS export.

public class InstrumentScript : MonoBehaviour
{
    // Controls a single instrument in the combined RNBO patch

    bool rnboAvailable;
    bool webInitStarted;

    // Only needs to be unique if multiple InstrumentScripts each load their own device.
    private int instanceIndex = 1;
    public string instrumentName = "";

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

    const float WebInitTimeoutSeconds = 120f;

    void Start()
    {
        rnboAvailable = false;
        webInitStarted = true;
        RnboWebBridge.Init(instanceIndex, RnboWebBridge.LautirSongPatcherUrl, RnboWebBridge.LautirSongDepsUrl);
        StartCoroutine(WaitForPatchLoad());
    }

    void Update()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0)
            || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            RnboWebBridge.ResumeAudioOnUserGesture();
        }
    }

    IEnumerator WaitForPatchLoad()
    {
        float t0 = Time.realtimeSinceStartup;
        while (!RnboWebBridge.IsReady(instanceIndex) && Time.realtimeSinceStartup - t0 < WebInitTimeoutSeconds)
            yield return null;

        rnboAvailable = RnboWebBridge.IsReady(instanceIndex);
        if (!rnboAvailable)
        {
            var err = RnboWebBridge.GetLastError(instanceIndex);
            Debug.LogError(string.IsNullOrEmpty(err)
                ? $"RNBO instance {instanceIndex} failed to load (check StreamingAssets/LautirSong URLs)."
                : $"RNBO instance {instanceIndex}: {err}");
        }
        else
        {
            Debug.Log($"[LAUTIR] Patch loaded (instance {instanceIndex})");

            // Load a bunch of default parameters because they're getting reset in the patch for some reason
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/attack", 30f);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/decay", 200f);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/sustain", 0.5f);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/release", 300f);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/timbre", 0);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/feedback", 0.5f);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/volume", 0f);
        }
    }


    // More complicated trigger that will parse a word and trigger the melody

    public void ParseWord(string word)
    {
        if (word == null || word.Length == 0) return;
        this.letters = word.ToCharArray();

        RnboWebBridge.ResumeAudioOnUserGesture();
        if (!RnboWebBridge.IsReady(instanceIndex) && !webInitStarted)
        {
            webInitStarted = true;
            RnboWebBridge.Init(instanceIndex, RnboWebBridge.LautirSongPatcherUrl, RnboWebBridge.LautirSongDepsUrl);
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

    private IEnumerator PlayMelody(){
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
        }

        if (rnboAvailable){
            // Set Parameters
            
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/phraseLength", phraseLength);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/noteDensity", noteDensity);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/melody", melody);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/timbre", timbre);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/note", note);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/leftDelay", leftDelay);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/rightDelay", rightDelay);
            RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/feedback", feedback);

            Debug.Log($"[LAUTIR] Params set (instance {instanceIndex}): {phraseLength}:{noteDensity}:{melody}:{timbre}:{note}");
            
            // Wait for a tenth of a second
            // Might be too fast, but we can adjust later
            yield return new WaitForSeconds(.1f);

            // Arm + trigger
            if (!RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/begin", 1))
                Debug.LogError($"[LAUTIR] SetParam begin failed (instance {instanceIndex})");
            if (!RnboWebBridge.SendMessage(instanceIndex, "rnboReceive", 1))
                Debug.LogError($"[LAUTIR] SendMessage rnboReceive failed (instance {instanceIndex})");
            else
                Debug.Log($"[LAUTIR] Trigger sent (instance {instanceIndex}) — check browser console for AudioContext=running");
        
            // Ramp up volume to X over 4 measures
            float volume = 0f;
            float duration = 4f * 60f / GlobalVariables.S.bpm;
            while (volume < 1f)
            {
                volume += Time.deltaTime / duration;
                RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/volume", volume);
                yield return null;
            }
        }
    }
}
