using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class WordDisplay : MonoBehaviour
{
    private TMP_Text wordText;
    private List<string> words;

    void Awake()
    {
        wordText = GetComponent<TMP_Text>();
    }
    
    
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        DisplayWords();
    }

    // Update is called once per frame
    void DisplayWords()
    {
        words = WordInputManager.LoadWords();
     
        wordText.text = "";
        for(int i = 0; i < words.Count; i++)
        {
            wordText.text += words[i] + "\n";
        }
        Debug.Log(wordText.text);
        Debug.Log(words.Count);
    }
}
