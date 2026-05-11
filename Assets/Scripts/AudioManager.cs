using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Drives a mixer cutoff from <see cref="GameManager.GetAvailabilityHomeBlend"/> (same easing idea as circles).
/// Disabled on WebGL when the mixer / native RNBO path is not used.
/// </summary>
public class AudioManager : MonoBehaviour
{
#if !(UNITY_WEBGL && !UNITY_EDITOR)
    [SerializeField] AudioMixer mixer;
#endif
    [SerializeField] string padCutoffParameterName = "PadCutoff";

    // Lower = linger at each end of the blend, sharper move through the middle (match BackgroundObject.approachCurvePower).
    [Range(0.05f, 1f)] [SerializeField] float approachCurvePower = 0.2f;

    const float CutoffHzMin = 200f;
    const float CutoffHzMax = 5000f;

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
}
