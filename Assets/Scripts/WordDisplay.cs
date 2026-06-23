using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class WordDisplay : MonoBehaviour
{
    private TMP_Text[] wordTexts;
    public float wordAlpha = 0.1f;
    private List<string> words;

    public Image backgroundImage;

    public GameObject wordTextObjects;

    public static WordDisplay S;

    void Awake()
    {
        wordTexts = GetComponentsInChildren<TMP_Text>();
        // Display-only text; must not eat clicks meant for the input field underneath.
        if (wordTexts != null) foreach (var text in wordTexts) text.raycastTarget = false;
        S = this;
    }

    public void FadeInWordDisplay(){
        
        StartCoroutine(FadeInBackground());
    }

    public void FadeOutWordDisplay(){
        StartCoroutine(FadeOutBackground());
    }

    private IEnumerator FadeInBackground()
    {
        
        
        float elapsed = 0f;
        float duration = 4f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, Mathf.Lerp(0f, 1f, elapsed / duration));
            yield return null;
        }

        backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 1f);
    }

    private IEnumerator FadeOutBackground()
    {
        float elapsed = 0f;
        float duration = 4f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, Mathf.Lerp(1f, 0f, elapsed / duration));
            yield return null;
        }

        backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 0f);
    }

    public void DisplayWord(int wordIndex)
    {
        wordTexts[wordIndex].alpha = 0f;
        string word = WordInputManager.GetWordAt(wordIndex);
        wordTexts[wordIndex].text = word;
        StartCoroutine(FadeInWord(wordIndex));
    }

    private IEnumerator FadeInWord(int wordIndex)
    {
        // Build in a buffer since the melody is starting a little slowly
        yield return new WaitForSeconds(1.5f);

        float elapsed = 0f;
        // 8 seconds to match music fade in duration
        float duration = 8f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            wordTexts[wordIndex].alpha = Mathf.Lerp(0f, wordAlpha, elapsed / duration);
            yield return null;
        }
        wordTexts[wordIndex].alpha = wordAlpha;
    }

    public void ClearWord(int wordIndex){
        StartCoroutine(FadeOutWord(wordIndex));
    }

    private IEnumerator FadeOutWord(int wordIndex)
    {
        float elapsed = 0f;
        float duration = 8f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            wordTexts[wordIndex].alpha = Mathf.Lerp(wordAlpha, 0f, elapsed / duration);
            yield return null;
        }
        wordTexts[wordIndex].alpha = 0f;
    }
}
