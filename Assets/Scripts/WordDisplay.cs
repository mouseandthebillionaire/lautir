using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class WordDisplay : MonoBehaviour
{
    private TMP_Text wordText;
    private List<string> words;

    public static WordDisplay S;

    void Awake()
    {
        wordText = GetComponent<TMP_Text>();
        // Display-only text; must not eat clicks meant for the input field underneath.
        if (wordText != null) wordText.raycastTarget = false;
        S = this;
    }

    void Start()
    {
        DisplayWords();
    }

    public void DisplayWords()
    {
        wordText.alpha = 0f;
        words = WordInputManager.LoadWords();
        wordText.text = "";
        for (int i = 0; i < words.Count; i++)
            wordText.text += words[i] + "\n";
        StartCoroutine(FadeInWords());
    }

    private IEnumerator FadeInWords()
    {
        float elapsed = 0f;
        float duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            wordText.alpha = Mathf.Lerp(0f, 0.1f, elapsed / duration);
            yield return null;
        }
        wordText.alpha = 0.1f;
    }
}
