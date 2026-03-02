using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WordInputManager : MonoBehaviour {
    public GameObject[] textBoxes;
    public TMP_Text[] letters;
    public GameObject submitButton;
    
    public string word;

    private int keyNum;
    private string currKey;

    public static WordInputManager S;

    void Awake(){
        S = this;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        Reset();
        if (GameManager.S != null && GameManager.S.IsGameAvailable) {
            ShowBoxes();  // In correct time at start: fade in
        } else {
            HideBoxesImmediate();  // Not in correct time at start: don't show at all (no fade)
        }
    }

    // Update is called once per frame
    void Update() {
        if (GameManager.S.IsGameAvailable) {
            if (keyNum < letters.Length) {
                submitButton.SetActive(false);
                GetKey();
            }
            else {
                submitButton.SetActive(true);
            }
        }
        
        // Always allow for deleting
        if (Input.GetKeyDown(KeyCode.Backspace) && keyNum > 0) {
            Debug.Log("Deleting");
            keyNum -= 1;
            letters[keyNum].text =  "X";
        }

        
    }
    
    void Reset() {
        keyNum = 0;
        if(GameManager.S.IsGameAvailable){
            for(int i = 0; i < textBoxes.Length; i++) {
                letters[i].text = "X";
            }
        }
    }

    void GetKey() {
        for (int i = (int)KeyCode.A; i < (int)KeyCode.Z; i++) {
            if (Input.GetKeyDown ((KeyCode)i)) {
                if(keyNum < letters.Length) {
                    char   c           = (char)i;
                    string letterInput = c.ToString().ToUpper();
                    letters[keyNum].text = letterInput;
                    keyNum += 1;
                }       
            }
        }
        
    }

    public void EnterWord() {
        for (int i = 0; i < letters.Length; i++) {
            word += letters[i].text;
        }
        Debug.Log(word);
        
    }

    /// <summary>Fade in. Use when we enter the correct time (at start or while running).</summary>
    public void ShowBoxes() {
        StopAllCoroutines();  // Cancel any in-progress fade out
        for (int i = 0; i < textBoxes.Length; i++) {
            textBoxes[i].SetActive(true);
            Image img = textBoxes[i].GetComponent<Image>();
            if (img != null) {
                Color c = img.color;
                img.color = new Color(c.r, c.g, c.b, 0f);
            }
        }
        StartCoroutine(FadeBoxes(1f, 3f));
    }

    /// <summary>Hide boxes instantly (no fade). Use when game starts outside the time window.</summary>
    public void HideBoxesImmediate() {
        StopAllCoroutines();  // Cancel any in-progress fade
        for (int i = 0; i < textBoxes.Length; i++) {
            Image img = textBoxes[i].GetComponent<Image>();
            if (img != null) {
                Color c = img.color;
                img.color = new Color(c.r, c.g, c.b, 0f);
            }
            textBoxes[i].SetActive(false);
        }
    }

    /// <summary>Fade out then hide. Use when we exit the correct time while game is running.</summary>
    public void HideBoxes() {
        StartCoroutine(HideBoxesAfterFade());
    }

    IEnumerator HideBoxesAfterFade() {
        yield return FadeBoxes(0f, 3f);
        for (int i = 0; i < textBoxes.Length; i++) {
            textBoxes[i].SetActive(false);
        }
    }

    public IEnumerator FadeBoxes(float targetAlpha, float duration){
        if (textBoxes.Length == 0 || duration <= 0f) yield break;
        Image[] images = new Image[textBoxes.Length];
        float[] startAlphas = new float[textBoxes.Length];
        for (int i = 0; i < textBoxes.Length; i++) {
            images[i] = textBoxes[i].GetComponent<Image>();
            if (images[i] == null) continue;
            startAlphas[i] = images[i].color.a;
        }
        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < images.Length; i++) {
                if (images[i] == null) continue;
                float a = Mathf.Lerp(startAlphas[i], targetAlpha, t);
                Color c = images[i].color;
                images[i].color = new Color(c.r, c.g, c.b, a);
            }
            yield return null;
        }
        for (int i = 0; i < images.Length; i++) {
            if (images[i] == null) continue;
            Color c = images[i].color;
            images[i].color = new Color(c.r, c.g, c.b, targetAlpha);
        }
    }

}
