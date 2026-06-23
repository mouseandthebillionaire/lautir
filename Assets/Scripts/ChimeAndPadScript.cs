using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the <c>chime_and_pad</c> RNBO subpatcher: preloads pad WAV from the saved word,
/// maps letter positions to chime/pad params, starts the chime immediately, and ramps the pad
/// once its buffer is ready.
/// </summary>
public class ChimeAndPadScript : MonoBehaviour
{
    const string ParamPrefix = "chime_and_pad";
    const float WebInitTimeoutSeconds = 120f;
    const float PadLoadTimeoutSeconds = 60f;

    public int instanceIndex = 1;

    // Chime and pad are both driven by the first word
    public int wordIndex = 0;

    [Header("Pad media")]
    public string padMediaFolder = "LautirSong/media/";

    const int RequiredWordLength = WordInputManager.MaxWordLength;

    [Header("Global Song Variables")]
    public int bpm = 60;
    public int key = 0; // 0=Aminor, 1=Bminor, 2=Cminor, 3=Dminor, 4=Eminor
    //Not sure if I'll use this...
    public int timeSignature = 4; // 4/4, 3/4, 6/8, 9/8, 12/8
    
    [Header("Chime")]
    public float chimeVolume = 1f;
    public float attack = 30f;
    public float decay = 200f;
    public float sustain = 0.5f;
    public float release = 300f;
    public float feedback = 0.5f;
    public float padFeedback = 0.5f;

    [Header("Pad")]
    public float padVolume = 1f;
    public int timbre = 500;
    public int leftDelay = 300;
    public int rightDelay = 400;
    public int padLeftDelay = 300;
    public int padRightDelay = 400;

    bool rnboAvailable;
    bool webInitStarted;
    bool playing;

    string _padPath;
    Coroutine _padLoadCoroutine;
    Coroutine _padRampCoroutine;

    bool IsPadReady =>
        RnboWebBridge.GetDataBufferLoadState(instanceIndex) == RnboWebBridge.DataBufferLoadState.Succeeded;

    bool IsPadLoading =>
        RnboWebBridge.GetDataBufferLoadState(instanceIndex) == RnboWebBridge.DataBufferLoadState.Loading;

    void Start()
    {
        StartCoroutine(InitializeRnbo());
    }

    void Update()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0)
            || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            RnboWebBridge.ResumeAudioOnUserGesture();
        }
    }

    string Param(string name) => ParamPrefix + "/" + name;

    IEnumerator InitializeRnbo()
    {
        rnboAvailable = false;
        if (!RnboWebBridge.IsReady(instanceIndex) && !webInitStarted)
        {
            webInitStarted = true;
            RnboWebBridge.Init(instanceIndex, RnboWebBridge.LautirSongPatcherUrl, RnboWebBridge.LautirSongDepsUrl);
        }

        float t0 = Time.realtimeSinceStartup;
        while (!RnboWebBridge.IsReady(instanceIndex) && Time.realtimeSinceStartup - t0 < WebInitTimeoutSeconds)
            yield return null;

        rnboAvailable = RnboWebBridge.IsReady(instanceIndex);
        if (!rnboAvailable)
        {
            var err = RnboWebBridge.GetLastError(instanceIndex);
            Debug.LogError(string.IsNullOrEmpty(err)
                ? $"[LAUTIR] ChimeAndPad: RNBO instance {instanceIndex} failed to load."
                : $"[LAUTIR] ChimeAndPad: {err}");
            yield break;
        }

        Debug.Log($"[LAUTIR] ChimeAndPad RNBO ready (instance {instanceIndex})");
        yield return PreloadPadFromSavedWord();
    }

    // Begin fetching the pad WAV as soon as RNBO is ready and we have a saved word.
    IEnumerator PreloadPadFromSavedWord()
    {
        for (int i = 0; i < 30; i++)
        {
            string word = GetSavedWordAtWordIndex();
            if (WordInputManager.IsMissedWord(word)) yield break;
            if (WordInputManager.IsPlayableWord(word))
            {
                EnsurePadLoading(PadPathForFirstLetter(word[0]));
                yield break;
            }
            yield return null;
        }
    }

    string GetSavedWordAtWordIndex() => WordInputManager.GetWordAt(wordIndex);

    string PadPathForFirstLetter(char letter)
    {
        char padLetter = char.ToUpperInvariant(letter);
        if (!char.IsLetter(padLetter))
        {
            Debug.LogWarning($"[LAUTIR] ChimeAndPad: invalid first letter '\\u{(int)letter:X4}', using A");
            padLetter = 'A';
        }

        return $"{padMediaFolder.TrimEnd('/')}/pad_{padLetter}.wav";
    }

    void EnsurePadLoading(string padPath)
    {
        if (string.IsNullOrEmpty(padPath)) return;
        if (padPath == _padPath && (IsPadReady || IsPadLoading)) return;

        if (_padLoadCoroutine != null)
        {
            StopCoroutine(_padLoadCoroutine);
            _padLoadCoroutine = null;
        }

        _padPath = padPath;
        _padLoadCoroutine = StartCoroutine(LoadPadCoroutine(padPath));
    }

    // Arms the chime at volume 0 and begins pad load; pad fades in on its own schedule.
    public IEnumerator ArmChimeFromSavedWord()
    {
        string word = GetSavedWordAtWordIndex();
        if (WordInputManager.IsMissedWord(word))
            yield break;

        if (!WordInputManager.IsPlayableWord(word))
        {
            Debug.LogWarning(
                $"[LAUTIR] ChimeAndPad: need a {RequiredWordLength}-letter word at index {wordIndex}, got \"{word}\". " +
                "Enter a word in the ritual or set test words on WordInputManager.");
            yield break;
        }

        if (playing) yield break;
        playing = true;

        RnboWebBridge.ResumeAudioOnUserGesture();
        yield return WaitForReady();
        if (!rnboAvailable)
        {
            playing = false;
            yield break;
        }

        ParseWordFromLetters(word.ToCharArray());
        EnsurePadLoading(PadPathForFirstLetter(word[0]));
        yield return StartChime();
        StopPadRamp();
        _padRampCoroutine = StartCoroutine(RampPadVolumeWhenReady());
        playing = false;
    }

    public void StopPadRamp()
    {
        if (_padRampCoroutine != null)
        {
            StopCoroutine(_padRampCoroutine);
            _padRampCoroutine = null;
        }
    }

    // Called by SongManager — look up the saved word and play chime + pad.
    public void PlayFromSavedWord()
    {
        StartCoroutine(PlayFromSavedWordRoutine());
    }

    IEnumerator PlayFromSavedWordRoutine()
    {
        yield return ArmChimeFromSavedWord();
    }

    IEnumerator PlayFromWord(string word)
    {
        if (playing) yield break;
        playing = true;

        try
        {
            yield return WaitForReady();
            if (!rnboAvailable) yield break;

            ParseWordFromLetters(word.ToCharArray());
            EnsurePadLoading(PadPathForFirstLetter(word[0]));

            yield return StartChime();
            yield return RampPadVolumeWhenReady();
        }
        finally
        {
            playing = false;
        }
    }

    // Map the five letters of the saved word to chime + pad RNBO parameters (no pad I/O).
    public void ParseWordFromLetters(char[] letters)
    {
        if (letters == null || letters.Length < RequiredWordLength)
        {
            Debug.LogError($"[LAUTIR] ChimeAndPad: ParseWordFromLetters needs {RequiredWordLength} letters.");
            return;
        }

        // Letter Commonality for a bunch of these parameters
        // Might want to move this to a GlobalVariables file?
        char[] letterCommonality = new char[] { 'e', 't', 'a', 'o', 'i', 'n', 's', 'r', 'h', 'd', 'l', 'u', 'c', 'm', 'f', 'y', 'w', 'g', 'p', 'b', 'v', 'k', 'x', 'q', 'j', 'z' };

        // Song BPM from 2nd letter
        int[] availableBpm = new int[] { 45, 60, 90, 120 };
        int bpmLetterIndex = System.Array.IndexOf(letterCommonality, char.ToLowerInvariant(letters[1]));
        if (bpmLetterIndex < 0) bpmLetterIndex = 0;
        bpm = availableBpm[Mathf.Min(bpmLetterIndex / 7, availableBpm.Length - 1)];
        if (GlobalVariables.S != null) GlobalVariables.S.bpm = bpm;

        // Song Key from 3rd letter
        int[] availableKeys = new int[] { 0, 1, 2, 3, 4 };
        int keyLetterIndex = System.Array.IndexOf(letterCommonality, char.ToLowerInvariant(letters[2]));
        if (keyLetterIndex < 0) keyLetterIndex = 0;
        key = availableKeys[Mathf.Min(keyLetterIndex / 5, availableKeys.Length - 1)];
        if (GlobalVariables.S != null) GlobalVariables.S.key = key;

        // Chime timbre from 4th letter
        timbre = (char.ToLowerInvariant(letters[3]) - 'a') * 40;

        // Chime delays: 5th letter vs 1st (left), 5th vs 2nd (right)
        int delayDistance = Mathf.Abs(char.ToLowerInvariant(letters[4]) - char.ToLowerInvariant(letters[0]));
        leftDelay = 100 + (delayDistance * 36);

        delayDistance = Mathf.Abs(char.ToLowerInvariant(letters[4]) - char.ToLowerInvariant(letters[1]));
        rightDelay = 100 + (delayDistance * 36);
    }

    // Apply params and trigger the chime immediately; pad stays silent until its buffer is ready.
    public IEnumerator StartChime()
    {

        SetParam("timbre", timbre);
        SetParam("attack", attack);
        SetParam("decay", decay);
        SetParam("sustain", sustain);
        SetParam("release", release);
        SetParam("leftDelay", leftDelay);
        SetParam("rightDelay", rightDelay);
        SetParam("feedback", feedback);

        SetParam("padLeftDelay", padLeftDelay);
        SetParam("padRightDelay", padRightDelay);
        SetParam("padFeedback", padFeedback);

        SetParam("begin", 1);
        SetParam("chimeVolume", 0f);
        SetParam("padVolume", 0f);

        // Maybe we need to do this last?
        RnboWebBridge.SetParamById(instanceIndex, "_bpm", bpm);
        RnboWebBridge.SetParamById(instanceIndex, "_key", key);
        Debug.Log($"[LAUTIR] ChimeAndPad: bpm={bpm}, key={key}");
        
        yield break;
    }

    IEnumerator RampPadVolumeWhenReady(float padLoadTimeoutSeconds = PadLoadTimeoutSeconds)
    {
        try
        {
            float t0 = Time.realtimeSinceStartup;
            while (!IsPadReady && Time.realtimeSinceStartup - t0 < padLoadTimeoutSeconds)
            {
                if (!IsPadLoading && !IsPadReady)
                    break;
                yield return null;
            }

            if (!IsPadReady)
            {
                Debug.LogWarning("[LAUTIR] ChimeAndPad: pad not ready — skipping pad volume ramp");
                yield break;
            }

            float volume = 0f;
            float duration = GlobalVariables.BarDurationSeconds(8);
            while (volume < 1f)
            {
                volume += Time.deltaTime / duration;
                SetParam("padVolume", volume);
                yield return null;
            }
            SetParam("padVolume", 1f);
        }
        finally
        {
            _padRampCoroutine = null;
        }
    }

    public bool LoadPad(string streamingAssetsRelativePath)
    {
        RnboWebBridge.ResetDataBufferLoadState(instanceIndex);
        return RnboWebBridge.LoadPadFromStreamingAssets(instanceIndex, streamingAssetsRelativePath);
    }

    public IEnumerator LoadPadAndWait(string streamingAssetsRelativePath, float timeoutSeconds = PadLoadTimeoutSeconds)
    {
        EnsurePadLoading(streamingAssetsRelativePath);
        yield return WaitForPadReady(timeoutSeconds);
    }

    IEnumerator LoadPadCoroutine(string streamingAssetsRelativePath, float timeoutSeconds = PadLoadTimeoutSeconds)
    {
        Debug.Log($"[LAUTIR] ChimeAndPad: loading pad {streamingAssetsRelativePath}");
        RnboWebBridge.ResetDataBufferLoadState(instanceIndex);
        if (!RnboWebBridge.LoadPadFromStreamingAssets(instanceIndex, streamingAssetsRelativePath))
        {
            _padLoadCoroutine = null;
            yield break;
        }

        yield return RnboWebBridge.WaitForDataBufferLoad(instanceIndex, timeoutSeconds);

        var state = RnboWebBridge.GetDataBufferLoadState(instanceIndex);
        if (state == RnboWebBridge.DataBufferLoadState.Succeeded)
            Debug.Log($"[LAUTIR] Pad buffer loaded: {streamingAssetsRelativePath}");
        else if (state == RnboWebBridge.DataBufferLoadState.Failed)
            Debug.LogWarning($"[LAUTIR] Pad load failed: {RnboWebBridge.GetLastError(instanceIndex)}");

        _padLoadCoroutine = null;
    }

    IEnumerator WaitForPadReady(float timeoutSeconds = PadLoadTimeoutSeconds)
    {
        if (IsPadReady) yield break;

        float t0 = Time.realtimeSinceStartup;
        while (IsPadLoading && Time.realtimeSinceStartup - t0 < timeoutSeconds)
            yield return null;
    }

    IEnumerator WaitForReady()
    {
        if (!RnboWebBridge.IsReady(instanceIndex) && !webInitStarted)
        {
            webInitStarted = true;
            RnboWebBridge.Init(instanceIndex, RnboWebBridge.LautirSongPatcherUrl, RnboWebBridge.LautirSongDepsUrl);
        }

        float t0 = Time.realtimeSinceStartup;
        while (!RnboWebBridge.IsReady(instanceIndex) && Time.realtimeSinceStartup - t0 < WebInitTimeoutSeconds)
            yield return null;

        rnboAvailable = RnboWebBridge.IsReady(instanceIndex);
    }

    bool SetParam(string name, float value) =>
        RnboWebBridge.SetParamById(instanceIndex, Param(name), value);
}
