using UnityEngine;
using System.Collections;
using System.Linq;

public class BassScript : MonoBehaviour
{
    // Controls a the bass instrument in the combined RNBO patch

    bool rnboAvailable;
    bool webInitStarted;
    private int instanceIndex = 1;

    public string instrumentName = "bass";

    public int phraseLength = 8; // RNBO range 2–8
    public int noteDensity = 7;
    public int bassline = 0;
    public int mix = 0; // 0–4 in RNBO
    public int note = 2; // 1–4 in RNBO
    public int distVolume = 30;

    public bool tapToTriggerOnMobile = true;

    public char[] letters;

    bool running;

    const float WebInitTimeoutSeconds = 120f;

    static readonly char[] DefaultLetterCommonality =
        { 'E', 'T', 'A', 'O', 'I', 'N', 'S', 'R', 'H', 'D', 'L', 'U', 'C', 'M', 'F', 'Y', 'W', 'G', 'P', 'B', 'V', 'K', 'X', 'Q', 'J', 'Z' };

    char[] LetterCommonality =>
        GlobalVariables.S != null ? GlobalVariables.S.letterCommonality : DefaultLetterCommonality;

    int SongBpm => GlobalVariables.S != null ? GlobalVariables.S.bpm : 60;
    int SongKey => GlobalVariables.S != null ? GlobalVariables.S.key : 0;

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
        // Defaults only — word-driven params are applied in PlayBassline.
        SetParam("attack", 30f);
        SetParam("decay", 200f);
        SetParam("sustain", 0.5f);
        SetParam("release", 300f);
        SetParam("volume", 0f);
        SetParam("phraseLength", 16);
        SetParam("note", 2);
        SetParam("bass", 100);
        SetParam("distVolume", 75);
        SetParam("mix", 0);
        SetParam("drive", 75);
    }

    public void ParseWord(string word)
    {
        if (word == null || word.Length < WordInputManager.MaxWordLength) return;
        this.letters = word.ToCharArray();

        RnboWebBridge.ResumeAudioOnUserGesture();
        if (!RnboWebBridge.IsReady(instanceIndex) && !webInitStarted)
        {
            webInitStarted = true;
            RnboWebBridge.Init(instanceIndex, RnboWebBridge.LautirSongPatcherUrl, RnboWebBridge.LautirSongDepsUrl);
        }

        StartCoroutine(WordToBasslineWrapper());
    }

    IEnumerator WordToBasslineWrapper()
    {
        running = true;
        yield return WordToBassline();
        running = false;
    }

    public IEnumerator WordToBassline()
    {
        char[] letterCommonality = LetterCommonality;

        // Phrase length from 1st letter — RNBO accepts 2–8 (not 16/8/4 bars literally)
        int[] availablePhrases = { 8, 4, 2 };
        int letterIndex = System.Array.IndexOf(letterCommonality, char.ToUpperInvariant(letters[0]));
        if (letterIndex < 0) letterIndex = 0;
        phraseLength = availablePhrases[Mathf.Min(letterIndex / 5, availablePhrases.Length - 1)];

        int densityIndex = System.Array.IndexOf(letterCommonality, char.ToUpperInvariant(letters[1]));
        if (densityIndex < 0) densityIndex = 0;
        noteDensity = densityIndex / 4;

        bassline = char.ToLowerInvariant(letters[2]) - 'a';
        bassline = Mathf.Clamp(bassline, 0, 27);

        mix = Mathf.Clamp((char.ToLowerInvariant(letters[3]) - 'a') / 6, 0, 4);

        int noteIndex = System.Array.IndexOf(letterCommonality.Reverse().ToArray(), char.ToUpperInvariant(letters[1]));
        if (noteIndex < 0) noteIndex = 0;
        note = noteIndex / 7 + 1;

        yield return PlayBassline();
    }

    private IEnumerator PlayBassline()
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

        RnboWebBridge.SetParamById(instanceIndex, "_bpm", SongBpm);
        RnboWebBridge.SetParamById(instanceIndex, "_key", SongKey);

        SetParam("phraseLength", phraseLength);
        SetParam("noteDensity", noteDensity);
        SetParam("bassline", bassline);
        SetParam("mix", mix);
        SetParam("note", note);
        SetParam("bass", 100);
        SetParam("volume", 0f);

        Debug.Log($"[LAUTIR] Bass params (instance {instanceIndex}): phraseLength={phraseLength}, noteDensity={noteDensity}, baseline={bassline}, mix={mix}, distVolume={distVolume}, note={note}");

        yield return new WaitForSeconds(0.1f);

        SetParam("begin", 0);
        SetParam("begin", 1);
        if (!RnboWebBridge.SendMessage(instanceIndex, "rnboReceive", 1))
            Debug.LogError($"[LAUTIR] SendMessage rnboReceive failed (instance {instanceIndex})");
        else
            Debug.Log($"[LAUTIR] Bass trigger sent (instance {instanceIndex})");
    }

    bool SetParam(string name, float value) =>
        RnboWebBridge.SetParamById(instanceIndex, instrumentName + "/" + name, value);
}
