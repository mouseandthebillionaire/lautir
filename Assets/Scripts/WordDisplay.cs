using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>Renders the six saved words from <see cref="WordInputManager"/>.</summary>
public class WordDisplay : MonoBehaviour
{
    private TMP_Text wordText;
    private List<string> words;

    public static WordDisplay S;

    void Awake()
    {
        wordText = GetComponent<TMP_Text>();
        S = this;
    }

    void Start()
    {
        DisplayWords();
    }

    public void DisplayWords()
    {
        words = WordInputManager.LoadWords();
        wordText.text = "";
        for (int i = 0; i < words.Count; i++)
            wordText.text += words[i] + "\n";
    }
}
