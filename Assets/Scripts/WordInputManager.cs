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
        if(GameManager.S.IsGameAvailable){
            ShowBoxes();
        } else {
            HideBoxes();
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

    public void ShowBoxes(){
        for (int i = 0; i < textBoxes.Length; i++) {
            textBoxes[i].SetActive(true);
        }
        StartCoroutine(FadeBoxes(1f, 3f));
    }

    public void HideBoxes(){
       StartCoroutine(FadeBoxes(0f, 3f));
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
            startAlphas[i] = images[i].color.a;
        }
        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < images.Length; i++) {
                float a = Mathf.Lerp(startAlphas[i], targetAlpha, t);
                Color c = images[i].color;
                images[i].color = new Color(c.r, c.g, c.b, a);
            }
            yield return null;
        }
        for (int i = 0; i < images.Length; i++) {
            Color c = images[i].color;
            images[i].color = new Color(c.r, c.g, c.b, targetAlpha);
        }
    }

}
