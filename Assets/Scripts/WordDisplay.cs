using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class WordDisplay : MonoBehaviour
{
    private TMP_Text[] wordTexts;
    private List<string> words;

    public GameObject wordTextObjects;

    public static WordDisplay S;

    void Awake()
    {
        wordTexts = GetComponentsInChildren<TMP_Text>();
        // Display-only text; must not eat clicks meant for the input field underneath.
        if (wordTexts != null) foreach (var text in wordTexts) text.raycastTarget = false;
        S = this;
    }

    void Start()
    {
        DisplayWord(0);
    }

    public void DisplayWord(int wordIndex)
    {
        wordTexts[wordIndex].alpha = 0f;
        string word = WordInputManager.S.words[wordIndex];
        wordTexts[wordIndex].text = word;
        StartCoroutine(FadeInWord(wordIndex));
    }

    private IEnumerator FadeInWord(int wordIndex)
    {
        // Build in a buffer since the melody is starting a little slowly
        yield return new WaitForSeconds(1.5f);

        float elapsed = 0f;
        float duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            wordTexts[wordIndex].alpha = Mathf.Lerp(0f, 0.1f, elapsed / duration);
            yield return null;
        }
        wordTexts[wordIndex].alpha = 0.1f;
    }
}
