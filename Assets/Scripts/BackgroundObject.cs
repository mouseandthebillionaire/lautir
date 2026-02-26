using UnityEngine;

public class BackgroundObject : MonoBehaviour
{
    public float[] properLoc = {0, 1.75f};
    public float[] properScale = {6f, 6f};
    public float properOpacity = 1f;
    [Tooltip("Opacity when scattered (away). At home, objects overlap and add to white.")]
    [Range(0.01f, 1f)] public float awayOpacity = 0.2f;

    const float HalfCycleMinutes = 720f; // 12 hours each way (home→away, then away→home)
    const float ReturnRampMinutes = 2f; // final approach to home is linear over this many minutes
    const float MoveAwayRampMinutes = 2f; // first minutes after time is up: ease out from home (no sudden jump)
    [Tooltip("Lower = object lingers at each end longer, then moves more sharply. 1 = linear.")]
    [Range(0.05f, 1f)] public float approachCurvePower = 0.2f;
    float awayX, awayY, awayScale;

    /// <summary>One of R, G, or B so overlapping at home adds to white (additive blending).</summary>
    Color baseTint;
    const float ChannelAlpha = 1f / 3f; // so R+G+B overlap = white

    // Animation Values
    private SpriteRenderer sr;
    private Sprite[] frames;
    public float frameRate = 10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitializeAnimation();
        SetAwayValues();
        InvokeRepeating("MoveHome", 0f, 0.5f);
    }

    // Update is called once per frame
    void Update()
    {
    }

    void InitializeAnimation()
    {
        sr = GetComponent<SpriteRenderer>();
        frames = Resources.LoadAll<Sprite>("Circles");
        InvokeRepeating("Animate", 0f, 1f / frameRate);
        SetAdditiveBlending();
    }

    void SetAdditiveBlending()
    {
        Shader additiveShader = Shader.Find("Custom/Sprites Additive");
        if (additiveShader != null)
            sr.material = new Material(additiveShader);
        // If shader not found, sprites keep default blend; R/G/B tints still applied
    }

    void SetAwayValues()
    {
        awayX = Random.Range(-10f, 10f);
        awayY = Random.Range(-10f, 10f);
        awayScale = Random.Range(0.01f, 10f);

        int channel = Random.Range(0, 3);
        if (channel == 0) baseTint = new Color(1f, 0f, 0f, ChannelAlpha);
        else if (channel == 1) baseTint = new Color(0f, 1f, 0f, ChannelAlpha);
        else baseTint = new Color(0f, 0f, 1f, ChannelAlpha);

        transform.localPosition = new Vector3(awayX, awayY, 0);
        transform.localScale = new Vector3(awayScale, awayScale, 1);
        sr.color = new Color(baseTint.r, baseTint.g, baseTint.b, awayOpacity);
    }

    void MoveHome()
    {
        float minutes = (float)GameManager.S.MinutesUntilAvailable();
        float t; // 1 = home, 0 = away
        if (minutes >= HalfCycleMinutes)
        {
            // Move-away leg: home to away. Ramp-out keyed to when the home window ENDS (durationMinutes), not 1440.
            float raw = Mathf.Clamp01((minutes - HalfCycleMinutes) / HalfCycleMinutes); // 0 at 720, 1 at 1440
            float tCurve = 1f - Mathf.Pow(1f - raw, approachCurvePower);
            float minutesSinceEnd = (float)GameManager.S.MinutesSinceAvailableEnded();
            if (minutesSinceEnd <= MoveAwayRampMinutes && MoveAwayRampMinutes > 0f)
            {
                // Ease out from home over first 2 min after availability window ends
                float minutesAtRampEnd = 1440f - GameManager.S.durationMinutes - MoveAwayRampMinutes;
                float rawAtRampEnd = Mathf.Clamp01((minutesAtRampEnd - HalfCycleMinutes) / HalfCycleMinutes);
                float tAtRampEnd = 1f - Mathf.Pow(1f - rawAtRampEnd, approachCurvePower);
                float rampT = minutesSinceEnd / MoveAwayRampMinutes; // 0 when window just ended, 1 at 2 min after
                t = Mathf.Lerp(1f, tAtRampEnd, rampT);
            }
            else
                t = tCurve;
        }
        else
        {
            // Return-home leg: 0–720 min → away to home. Linger away then snap home; last ReturnRampMinutes are linear ramp
            float raw = Mathf.Clamp01(minutes / HalfCycleMinutes); // 0 at 0 (just available), 1 at 720 (farthest)
            float tCurve = 1f - Mathf.Pow(raw, approachCurvePower);
            if (minutes <= ReturnRampMinutes && ReturnRampMinutes > 0f)
            {
                // Final approach: linear over last 2 min so ramp feels like 2 min, not 5 sec
                float tAtRampStart = 1f - Mathf.Pow(ReturnRampMinutes / HalfCycleMinutes, approachCurvePower);
                float rampT = 1f - minutes / ReturnRampMinutes; // 0 at 2 min left, 1 at 0 min
                t = Mathf.Lerp(tAtRampStart, 1f, rampT);
            }
            else
                t = tCurve;
        }

        Vector3 awayPos = new Vector3(awayX, awayY, 0);
        Vector3 homePos = new Vector3(properLoc[0], properLoc[1], 0);
        transform.localPosition = Vector3.Lerp(awayPos, homePos, t);

        float scale = Mathf.Lerp(awayScale, properScale[0], t);
        transform.localScale = new Vector3(scale, scale, 1);

        float alpha = Mathf.Lerp(awayOpacity, properOpacity, t);
        sr.color = new Color(baseTint.r, baseTint.g, baseTint.b, alpha);
    }

    void Animate()
    {
        int frameIndex = Random.Range(0, frames.Length);
        sr.sprite = frames[frameIndex];
    }
}
