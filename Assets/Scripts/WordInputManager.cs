using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Five-letter word entry, PlayerPrefs persistence (5 slots), and fade in/out of the input field.
/// </summary>
public class WordInputManager : MonoBehaviour
{
    public TMP_InputField inputField;
    public GameObject submitButton;
    public string word;
    public const int MaxWordLength = 5;
    public const int SavedWordDaysCount = 5;
    public const string MissedWordPlaceholder = "-----";

    const string SavedWordsKey = "lautir_words";
    const string RitualSlotKey = "lautir_ritual_slot";
    const string LastRitualDateKey = "lautir_last_ritual_date";
    const char WordSeparator = '\n';

    public bool testWords = false;

    public List<string> words;

    /// <summary>Next ritual slot to fill (0–5). Each committed day — word or missed blank — advances this.</summary>
    public static int RitualDayIndex => LoadRitualDayIndex();

    /// <summary>Number of saved words with real content (excludes missed-day placeholders).</summary>
    public static int SavedWordCount()
    {
        int count = 0;
        foreach (var w in LoadWords())
            if (IsPlayableWord(w)) count++;
        return count;
    }

    public static bool IsMissedWord(string word) =>
        string.Equals((word ?? "").Trim(), MissedWordPlaceholder, StringComparison.OrdinalIgnoreCase);

    public static bool IsPlayableWord(string word) =>
        !string.IsNullOrEmpty(word) && word.Length >= MaxWordLength && !IsMissedWord(word);

    /// <summary>True once all 5 ritual days are committed (words and/or missed blanks).</summary>
    public static bool AllDaysUsed => RitualDayIndex >= SavedWordDaysCount;

    /// <summary>[0] oldest … [4] newest; missing entries are empty strings.</summary>
    public static List<string> LoadWords()
    {
        var raw = PlayerPrefs.GetString(SavedWordsKey, "");
        var result = new List<string>(SavedWordDaysCount);
        for (int i = 0; i < SavedWordDaysCount; i++) result.Add("");

        if (!string.IsNullOrEmpty(raw))
        {
            var parts = raw.Split(WordSeparator);
            for (int i = 0; i < SavedWordDaysCount && i < parts.Length; i++)
                result[i] = parts[i] ?? "";
        }
        return result;
    }

    static int LoadRitualDayIndex()
    {
        if (!PlayerPrefs.HasKey(RitualSlotKey))
        {
            int count = 0;
            foreach (var w in LoadWords())
                if (!string.IsNullOrEmpty(w)) count++;
            return count;
        }
        return Mathf.Clamp(PlayerPrefs.GetInt(RitualSlotKey, 0), 0, SavedWordDaysCount);
    }

    static void SaveRitualDayIndex(int index)
    {
        PlayerPrefs.SetInt(RitualSlotKey, Mathf.Clamp(index, 0, SavedWordDaysCount));
        PlayerPrefs.Save();
    }

    static DateTime? LoadLastRitualDate()
    {
        var raw = PlayerPrefs.GetString(LastRitualDateKey, "");
        if (DateTime.TryParse(raw, out var d)) return d.Date;
        return null;
    }

    static void SaveLastRitualDate(DateTime date)
    {
        PlayerPrefs.SetString(LastRitualDateKey, date.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
    }

    void EnsureWordsList()
    {
        words = LoadWords();
        while (words.Count < SavedWordDaysCount) words.Add("");
    }

    /// <summary>Commit today's ritual slot (word or blank). Skips if today is already committed.</summary>
    public void CommitRitualDay(string word)
    {
        if (testWords || RitualDayIndex >= SavedWordDaysCount) return;

        var today = DateTime.Today;
        var last = LoadLastRitualDate();
        if (last.HasValue && last.Value >= today) return;

        EnsureWordsList();
        words[RitualDayIndex] = word ?? "";
        SaveRitualDayIndex(RitualDayIndex + 1);
        SaveLastRitualDate(today);
        SaveWords(words.ToArray());
        RefreshWords();
        GameManager.S?.SyncCurrentDay();
    }

    /// <summary>Record a missed ritual day as <see cref="MissedWordPlaceholder"/> at the current slot index.</summary>
    public void RecordMissedRitualDay() => CommitRitualDay(MissedWordPlaceholder);

    /// <summary>Fill missed slots for calendar days missed while the app was closed.</summary>
    public void AdvanceForMissedCalendarDays(GameManager gm)
    {
        if (testWords || AllDaysUsed) return;

        var last = LoadLastRitualDate();
        if (!last.HasValue) return;

        var today = DateTime.Today;
        for (var d = last.Value.AddDays(1); d < today && !AllDaysUsed; d = d.AddDays(1))
            CommitRitualDay(MissedWordPlaceholder);

        if (today > last.Value && !AllDaysUsed && gm != null && gm.IsPastTodaysRitualWindow())
            CommitRitualDay(MissedWordPlaceholder);
    }

    /// <summary>Dev only (<see cref="GameManager.testing"/>): rewind last ritual date so reload can commit the next slot.</summary>
    public void PrepareTestingNewDayOnLoad()
    {
        if (testWords || AllDaysUsed) return;

        var last = LoadLastRitualDate();
        if (!last.HasValue) return;

        if (last.Value >= DateTime.Today)
            SaveLastRitualDate(DateTime.Today.AddDays(-1));
    }

    public static WordInputManager S;

    static readonly string[] DefaultTestWords = { "WHALE", "-----", "SHARK", "SQUID", "CORAL" };

    void Awake()
    {
        if (S != null && S != this)
        {
            if (ShouldReplaceSingleton(S, this))
            {
                Debug.LogWarning("[LAUTIR] Replacing duplicate WordInputManager with the wired instance.");
                Destroy(S.gameObject);
                S = this;
            }
            else
            {
                Debug.LogWarning("[LAUTIR] Duplicate WordInputManager — destroying extra.");
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            S = this;
        }

        if (inputField != null)
            inputField.gameObject.SetActive(false);

        RefreshWords();
    }

    static bool ShouldReplaceSingleton(WordInputManager current, WordInputManager candidate)
    {
        if (current.inputField == null && candidate.inputField != null) return true;
        if (!current.testWords && candidate.testWords) return true;
        return false;
    }

    void RefreshWords()
    {
        words = testWords
            ? new List<string>(DefaultTestWords)
            : LoadWords();
    }

    /// <summary>Returns the saved word at <paramref name="index"/>, placeholder for missed days, or empty if uncommitted.</summary>
    public static string GetWordAt(int index)
    {
        if (index < 0 || index >= SavedWordDaysCount) return "";

        if (S?.words != null && index < S.words.Count)
        {
            var fromList = (S.words[index] ?? "").Trim().ToUpperInvariant();
            if (IsMissedWord(fromList)) return MissedWordPlaceholder;
            if (fromList.Length >= MaxWordLength) return fromList;
        }

        if (S != null && S.testWords && index < DefaultTestWords.Length)
            return DefaultTestWords[index];

        var loaded = LoadWords();
        if (index < loaded.Count)
        {
            var fromPrefs = (loaded[index] ?? "").Trim().ToUpperInvariant();
            if (IsMissedWord(fromPrefs)) return MissedWordPlaceholder;
            return fromPrefs;
        }

        return "";
    }

    void Start()
    {
        if (inputField != null)
        {
            inputField.characterLimit = MaxWordLength;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.onFocusSelectAll = false;
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            inputField.onSubmit.AddListener(_ => SubmitFromInput());
            inputField.onSelect.AddListener(_ => RnboWebBridge.ResumeAudioOnUserGesture());
            inputField.onEndEdit.AddListener(OnInputEndEdit);
            inputField.onTouchScreenKeyboardStatusChanged.AddListener(OnMobileKeyboardStatusChanged);
        }

        RefreshWords();
        Reset();
        if (GameManager.S != null && GameManager.S.IsGameAvailable)
            ShowInputField();
    }

    void Update()
    {
        if (inputField == null || !inputField.isFocused) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            SubmitFromInput();
    }

    void SubmitFromInput()
    {
        if (inputField == null || !inputField.gameObject.activeInHierarchy) return;
        if (_submitFrame == Time.frameCount) return;
        _submitFrame = Time.frameCount;
        RnboWebBridge.ResumeAudioOnUserGesture();
        EnterWord();
    }

    void OnInputEndEdit(string value)
    {
        if (GameManager.S == null || !GameManager.S.IsGameAvailable || inputField == null) return;
        var text = (value ?? inputField.text ?? "").Trim().ToUpperInvariant();
        if (text.Length == MaxWordLength && CheckWord(text))
            SubmitFromInput();
    }

    void OnMobileKeyboardStatusChanged(TouchScreenKeyboard.Status status)
    {
        if (status == TouchScreenKeyboard.Status.Done)
            SubmitFromInput();
    }

    void OnInputValueChanged(string value)
    {
        if (inputField == null) return;

        var raw = value ?? "";
        var filtered = new StringBuilder();
        bool hadInvalid = false;

        foreach (char c in raw)
        {
            if (char.IsLetter(c))
                filtered.Append(char.ToUpperInvariant(c));
            else
                hadInvalid = true;
        }

        var normalized = filtered.ToString();
        if (normalized.Length > MaxWordLength)
            normalized = normalized.Substring(0, MaxWordLength);

        if (hadInvalid || !string.Equals(normalized, raw, StringComparison.Ordinal))
            inputField.SetTextWithoutNotify(normalized);

        if (hadInvalid)
            PlayInputJiggle();
    }

    void ClearInputSelection()
    {
        if (inputField == null) return;
        int end = inputField.text?.Length ?? 0;
        inputField.caretPosition = end;
        inputField.selectionAnchorPosition = end;
        inputField.selectionFocusPosition = end;
    }

    public float showFadeDuration = 0.5f;
    public float hideFadeDuration = 0.5f;
    public float jiggleDuration = 0.45f;
    public float jiggleDistance = 12f;

    Coroutine _jiggleCoroutine;
    int _submitFrame = -1;

    /// <summary>True when <paramref name="text"/> is exactly five letters (A–Z).</summary>
    public bool CheckWord(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length != MaxWordLength)
            return false;

        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsLetter(text[i]))
                return false;
        }

        return true;
    }

    void PlayInputJiggle()
    {
        if (inputField == null) return;

        if (_jiggleCoroutine != null)
            StopCoroutine(_jiggleCoroutine);
        _jiggleCoroutine = StartCoroutine(JiggleInputField());
    }

    IEnumerator JiggleInputField()
    {
        var rt = inputField.transform as RectTransform;
        if (rt == null)
        {
            _jiggleCoroutine = null;
            yield break;
        }

        Vector2 basePos = rt.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < jiggleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jiggleDuration;
            float offset = Mathf.Sin(t * Mathf.PI * 6f) * jiggleDistance * (1f - t);
            rt.anchoredPosition = basePos + new Vector2(offset, 0f);
            yield return null;
        }

        rt.anchoredPosition = basePos;
        inputField.ActivateInputField();
        _jiggleCoroutine = null;
    }

    public void ShowInputField()
    {
        if (inputField == null) return;
        // All days used → the ritual is complete; never show the field again.
        if (AllDaysUsed) return;
        StopAllCoroutines();
        inputField.gameObject.SetActive(true);
        inputField.ActivateInputField();
        StartCoroutine(FadeInInput());
    }

    public void HideInputFieldImmediate()
    {
        if (inputField == null) return;
        StopAllCoroutines();
        inputField.gameObject.SetActive(false);
    }

    public void HideInputField()
    {
        if (inputField == null || !inputField.gameObject.activeSelf) return;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndHideInput());
    }

    IEnumerator FadeInInput()
    {
        var go = inputField.gameObject;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < showFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / showFadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    IEnumerator FadeOutAndHideInput()
    {
        var go = inputField.gameObject;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        float elapsed = 0f;
        float startAlpha = cg.alpha;
        while (elapsed < hideFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / hideFadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
        go.SetActive(false);
    }

    void Reset() { }

    /// <summary>Keeps exactly <see cref="SavedWordDaysCount"/> slots so indices stay stable.</summary>
    public void SaveWords(string[] _words)
    {
        var slots = new List<string>(_words ?? Array.Empty<string>());
        while (slots.Count < SavedWordDaysCount) slots.Add("");
        if (slots.Count > SavedWordDaysCount) slots = slots.GetRange(slots.Count - SavedWordDaysCount, SavedWordDaysCount);

        PlayerPrefs.SetString(SavedWordsKey, string.Join(WordSeparator.ToString(), slots));
        PlayerPrefs.Save();
    }

    [ContextMenu("Clear Saved Words (PlayerPrefs)")]
    public void ClearSavedWords()
    {
        PlayerPrefs.DeleteKey(SavedWordsKey);
        PlayerPrefs.DeleteKey(RitualSlotKey);
        PlayerPrefs.DeleteKey(LastRitualDateKey);
        PlayerPrefs.Save();
        RefreshWords();
    }

    public void EnterWord()
    {
        RnboWebBridge.ResumeAudioOnUserGesture();

        if (AllDaysUsed)
        {
            HideInputField();
            return;
        }

        if (inputField != null)
            word = (inputField.text ?? "").Trim().ToUpperInvariant();

        if (!CheckWord(word))
        {
            PlayInputJiggle();
            ClearInputSelection();
            return;
        }

        if (IsMissedWord(word))
            return;

        GameManager.S?.NotifyWordEnteredThisWindow();
        CommitRitualDay(word);

        int stageToLoad = RitualDayIndex;
        Debug.Log("Setting Stage to " + stageToLoad);
        SongManager.S.SetStage(stageToLoad);

        GameManager.S.NotifyUserEndedWindow(DateTime.Now);
        GameManager.S.SetGameAvailable(false);
        HideInputField();
    }
}
