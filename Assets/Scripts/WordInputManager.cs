using System;
using System.Collections;
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

    public static WordInputManager S;

    void Awake(){
        S = this;
    }
    
    const int MaxWordLength = 6;

    void Start() {
        if (inputField != null) {
            inputField.characterLimit = MaxWordLength;
            inputField.onValueChanged.AddListener(ClampInputToMaxLength);
            inputField.onEndEdit.AddListener(OnInputSubmit);
        }
        Reset();
        if (GameManager.S != null && GameManager.S.IsGameAvailable) {
            ShowInputField();
        } else {
            HideInputField();
        }
        LogAndShowSavedWords("Loaded on Start");
    }

    void ClampInputToMaxLength(string value) {
        if (inputField == null || value.Length <= MaxWordLength) return;
        inputField.text = value.Substring(0, MaxWordLength);
    }

    void OnInputSubmit(string value) {
        if (!GameManager.S.IsGameAvailable || inputField == null) return;
        var text = (inputField.text ?? "").Trim().ToUpper();
        if (text.Length == MaxWordLength)
            EnterWord();
    }

    // Update is called once per frame
    void Update() {
        if (GameManager.S.IsGameAvailable) {
            // allow typing in input field
            if (inputField != null) {
                inputField.text = inputField.text.ToUpper();
            }
        }
    }

    public void ShowInputField() {
        if (inputField != null) {
            inputField.gameObject.SetActive(true);
        }
    }
    
    public float hideFadeDuration = 0.5f;

    public void HideInputField() {
        if (inputField == null) return;
        Debug.Log("Hiding Input Field");
        StopAllCoroutines();
        StartCoroutine(FadeOutAndHideInput());
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

    // Saving Words
    const string SavedWordsKey = "lautir_words";
    const int SavedWordDaysCount = 6;
    const char WordSeparator = '\n';

    // Load the 6-word array. [0] = first day (oldest), [6] = most recent. Missing/empty = ""
    public static string[] LoadWords() {
        var raw = PlayerPrefs.GetString(SavedWordsKey, "");
        var parts = raw.Split(WordSeparator);
        var result = new string[SavedWordDaysCount];
        for (int i = 0; i < SavedWordDaysCount; i++) {
            result[i] = i < parts.Length ? parts[i] : "";
        }
        return result;
    }   

    public static void SaveWords(string[] words) {
        PlayerPrefs.SetString(SavedWordsKey, string.Join(WordSeparator.ToString(), words));
        PlayerPrefs.Save();
    }

    public void EnterWord() {
        if (inputField != null) {
            word = inputField.text;
        }
        Debug.Log(word);
        var words = LoadWords();
        for (int i = 0; i < SavedWordDaysCount - 1; i++) {
            words[i] = words[i + 1];
        }
        words[SavedWordDaysCount - 1] = word;
        SaveWords(words);
        GameManager.S.NotifyUserEndedWindow(DateTime.Now);
        GameManager.S.SetGameAvailable(false);
        HideInputField();
    }

    void LogAndShowSavedWords(string when) {
        var words = LoadWords();
        var line = "Saved words " + when + ": [" + string.Join(", ", words) + "]";
        Debug.Log(line);
    }


}
