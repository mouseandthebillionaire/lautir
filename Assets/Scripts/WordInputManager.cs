using UnityEngine;
using TMPro;

public class WordInputManager : MonoBehaviour {
    public GameObject[] textBoxes;
    public TMP_Text[] letters;
    public GameObject submitButton;
    
    public string word;

    private int keyNum;
    private string currKey;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        Reset();
        
        if(!GameManager.S.IsGameAvailable){
            for(int i = 0; i < textBoxes.Length; i++) {
                textBoxes[i].SetActive(false);
            }
            
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
}
