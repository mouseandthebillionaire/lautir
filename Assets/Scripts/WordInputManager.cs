using System;
using System.Collections;
using System.Collections.Generic;
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

    const string SavedWordsKey = "lautir_words";
    const string RitualSlotKey = "lautir_ritual_slot";
    const string LastRitualDateKey = "lautir_last_ritual_date";
    const char WordSeparator = '\n';

    public bool testWords = false;

    public List<string> words;

    /// <summary>Next ritual slot to fill (0–5). Each committed day — word or missed blank — advances this.</summary>
    public static int RitualDayIndex => LoadRitualDayIndex();

    /// <summary>Number of non-empty saved words.</summary>
    public static int SavedWordCount()
    {
        int count = 0;
        foreach (var w in LoadWords())
            if (!string.IsNullOrEmpty(w)) count++;
        return count;
    }

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

    /// <summary>Record a missed ritual day as a blank at the current slot index.</summary>
    public void RecordMissedRitualDay() => CommitRitualDay("");

    /// <summary>Fill blank slots for calendar days missed while the app was closed.</summary>
    public void AdvanceForMissedCalendarDays(GameManager gm)
    {
        if (testWords || AllDaysUsed) return;

        var last = LoadLastRitualDate();
        if (!last.HasValue) return;

        var today = DateTime.Today;
        for (var d = last.Value.AddDays(1); d < today && !AllDaysUsed; d = d.AddDays(1))
            CommitRitualDay("");

        if (today > last.Value && !AllDaysUsed && gm != null && gm.IsPastTodaysRitualWindow())
            CommitRitualDay("");
    }

    public static WordInputManager S;

    static readonly string[] DefaultTestWords = { "WHALE", "OCEAN", "SHARK", "SQUID", "CORAL" };

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

    /// <summary>Returns the saved word at <paramref name="index"/>, or empty if missing/short.</summary>
    public static string GetWordAt(int index)
    {
        if (index < 0 || index >= SavedWordDaysCount) return "";

        if (S?.words != null && index < S.words.Count)
        {
            var fromList = (S.words[index] ?? "").Trim().ToUpperInvariant();
            if (fromList.Length >= MaxWordLength) return fromList;
        }

        if (S != null && S.testWords && index < DefaultTestWords.Length)
            return DefaultTestWords[index];

        var loaded = LoadWords();
        if (index < loaded.Count)
            return (loaded[index] ?? "").Trim().ToUpperInvariant();

        return "";
    }

    void Start()
    {
        if (inputField != null)
        {
            inputField.characterLimit = MaxWordLength;
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            inputField.onEndEdit.AddListener(OnInputSubmit);
        }

        RefreshWords();
        Reset();
        if (GameManager.S != null && GameManager.S.IsGameAvailable)
            ShowInputField();
    }

    void OnInputValueChanged(string value)
    {
        if (inputField == null) return;

        // Avoid rewriting text every frame; normalize only on user edits (caret/focus stay stable).
        var normalized = (value ?? "").ToUpperInvariant();
        if (normalized.Length > MaxWordLength)
            normalized = normalized.Substring(0, MaxWordLength);

        if (!string.Equals(normalized, value, StringComparison.Ordinal))
            inputField.SetTextWithoutNotify(normalized);
    }

    void OnInputSubmit(string value)
    {
        if (!GameManager.S.IsGameAvailable || inputField == null) return;
        var text = (inputField.text ?? "").Trim().ToUpper();
        if (text.Length == MaxWordLength)
            EnterWord();
    }

    public float showFadeDuration = 0.5f;
    public float hideFadeDuration = 0.5f;

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
        if (AllDaysUsed)
        {
            HideInputField();
            return;
        }

        if (inputField != null)
            word = inputField.text;

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
