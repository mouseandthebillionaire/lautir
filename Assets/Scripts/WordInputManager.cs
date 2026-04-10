using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WordInputManager : MonoBehaviour {
    // Old Way
    //public GameObject[] textBoxes;
    //public TMP_Text[] letters;

    // New Way
    public TMP_InputField inputField;
    public GameObject submitButton;
    public string word;
    const int MaxWordLength = 6;

    // Saving Words
    const string SavedWordsKey = "lautir_words";
    const int SavedWordDaysCount = 6;
    const char WordSeparator = '\n';

    public List<string> words;

    // Load the 6-word array. [0] = oldest, [5] = most recent. Missing/empty = ""
    public static List<string> LoadWords() {
        var raw = PlayerPrefs.GetString(SavedWordsKey, "");
        var result = new List<string>(SavedWordDaysCount);
        for (int i = 0; i < SavedWordDaysCount; i++) result.Add("");

        if (!string.IsNullOrEmpty(raw)) {
            var parts = raw.Split(WordSeparator);
            for (int i = 0; i < SavedWordDaysCount && i < parts.Length; i++) {
                result[i] = parts[i] ?? "";
            }
        }
        return result;
    }   

    static void AddWordToSlots(List<string> slots, string newWord) {
        if (slots == null) return;

        // Ensure fixed length
        while (slots.Count < SavedWordDaysCount) slots.Add("");
        while (slots.Count > SavedWordDaysCount) slots.RemoveAt(0);

        for (int i = 0; i < slots.Count; i++) {
            if (string.IsNullOrEmpty(slots[i])) {
                slots[i] = newWord;
                return;
            }
        }

        // All slots are full: shift left and append newest
        slots.RemoveAt(0);
        slots.Add(newWord);
    }

    public static WordInputManager S;

    void Awake() {
        S = this;
        if (inputField != null)
            inputField.gameObject.SetActive(false);
    }

    void Start() {
        if (inputField != null) {
            inputField.characterLimit = MaxWordLength;
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            inputField.onEndEdit.AddListener(OnInputSubmit);
        }
        words = LoadWords();
        Reset();
        if (GameManager.S != null && GameManager.S.IsGameAvailable) {
            ShowInputField();
        }
        LogAndShowSavedWords("Loaded on Start");
    }

    void OnInputValueChanged(string value) {
        if (inputField == null) return;

        // TMP can get its caret/focus disrupted if we rewrite `inputField.text`
        // every frame. Only normalize when the user edits.
        var normalized = (value ?? "").ToUpperInvariant();
        if (normalized.Length > MaxWordLength) {
            normalized = normalized.Substring(0, MaxWordLength);
        }

        if (!string.Equals(normalized, value, StringComparison.Ordinal)) {
            // Avoid re-triggering onValueChanged.
            inputField.SetTextWithoutNotify(normalized);
        }
    }

    void OnInputSubmit(string value) {
        if (!GameManager.S.IsGameAvailable || inputField == null) return;
        var text = (inputField.text ?? "").Trim().ToUpper();
        if (text.Length == MaxWordLength)
            EnterWord();
    }

    // Intentionally no per-frame input rewriting.

    public float showFadeDuration = 0.5f;
    public float hideFadeDuration = 0.5f;

    public void ShowInputField() {
        if (inputField == null) return;
        StopAllCoroutines();
        inputField.gameObject.SetActive(true);
        // Ensure the input receives focus so the caret appears.
        inputField.ActivateInputField();
        StartCoroutine(FadeInInput());
    }

    public void HideInputFieldImmediate() {
        if (inputField == null) return;
        StopAllCoroutines();
        inputField.gameObject.SetActive(false);
    }

    public void HideInputField() {
        if (inputField == null || !inputField.gameObject.activeSelf) return;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndHideInput());
    }

    IEnumerator FadeInInput() {
        var go = inputField.gameObject;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < showFadeDuration) {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / showFadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    IEnumerator FadeOutAndHideInput() {
        var go = inputField.gameObject;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        float elapsed = 0f;
        float startAlpha = cg.alpha;
        while (elapsed < hideFadeDuration) {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / hideFadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
        go.SetActive(false);
    }

    void Reset() {
        // not sure what to reset with inputField
    }

    public void SaveWords(string[] _words) {
        // Persist exactly SavedWordDaysCount slots to keep indices stable.
        var slots = new List<string>(_words ?? Array.Empty<string>());
        while (slots.Count < SavedWordDaysCount) slots.Add("");
        if (slots.Count > SavedWordDaysCount) slots = slots.GetRange(slots.Count - SavedWordDaysCount, SavedWordDaysCount);

        PlayerPrefs.SetString(SavedWordsKey, string.Join(WordSeparator.ToString(), slots));
        PlayerPrefs.Save();
    }

    [ContextMenu("Clear Saved Words (PlayerPrefs)")]
    public void ClearSavedWords() {
        PlayerPrefs.DeleteKey(SavedWordsKey);
        PlayerPrefs.Save();
        words = LoadWords();
        LogAndShowSavedWords("Cleared");
    }

    public void EnterWord() {
        if (inputField != null) {
            word = inputField.text;
        }
        AddWordToSlots(words, word);
        SaveWords(words.ToArray());
        LogAndShowSavedWords("EnterWord");
        WordDisplay.S.DisplayWords();
        GameManager.S.NotifyUserEndedWindow(DateTime.Now);
        GameManager.S.SetGameAvailable(false);
        HideInputField();
    }

    public void LogAndShowSavedWords(string when) {
        var words = LoadWords();
        var line = "Saved words " + when + ": [" + string.Join(", ", words) + "]";
        Debug.Log(line);
    }


}
