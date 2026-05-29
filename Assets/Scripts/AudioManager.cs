using UnityEngine;
using UnityEngine.Audio;


public class AudioManager : MonoBehaviour
{
#if !(UNITY_WEBGL && !UNITY_EDITOR)
    public AudioMixer mixer;
#endif
    public string padCutoffParameterName = "PadCutoff";

    public MelodyScript[] melodyScripts;

    [Range(0.05f, 1f)] public float approachCurvePower = 0.2f;

    const float CutoffHzMin = 200f;
    const float CutoffHzMax = 5000f;

    public static AudioManager S;

    void Awake()
    {
        S = this;

        if (melodyScripts == null || melodyScripts.Length == 0)
            melodyScripts = GetComponentsInChildren<MelodyScript>(true);
    }

    void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        if (mixer == null || GameManager.S == null || string.IsNullOrEmpty(padCutoffParameterName))
            return;

        float t = GameManager.S.GetAvailabilityHomeBlend(approachCurvePower);
        float hz = Mathf.Lerp(CutoffHzMin, CutoffHzMax, t);
        mixer.SetFloat(padCutoffParameterName, hz);
#endif
    }

    public void TriggerMelody()
    {
        if (melodyScripts == null) return;
        foreach (var melody in melodyScripts)
        {
            if (melody != null)
                melody.TriggerMelody();
        }
    }

    public void ParseWord(string word)
    {
        foreach (var melody in melodyScripts)
        {
            if (melody != null)
                melody.ParseWord(word);
        }
    }
}
