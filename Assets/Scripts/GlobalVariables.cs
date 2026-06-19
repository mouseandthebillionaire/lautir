using UnityEngine;

public class GlobalVariables : MonoBehaviour
{
    
    public int bpm = 60;
    public int key = 0; // 0=Aminor, 1=Bminor, 2=Cminor, 3=Dminor, 4=Eminor
    public int timeSignature = 4; // 4/4, 3/4, 6/8, 9/8, 12/8   
    public char[] letterCommonality = new char[] { 'E', 'T', 'A', 'O', 'I', 'N', 'S', 'R', 'H', 'D', 'L', 'U', 'C', 'M', 'F', 'Y', 'W', 'G', 'P', 'B', 'V', 'K', 'X', 'Q', 'J', 'Z' };

    public static GlobalVariables S;

    void Awake()
    {
        S = this;
    }
}
