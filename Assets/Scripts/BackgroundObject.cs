using UnityEngine;

/// <summary>
/// One circle: drifts "away", eases toward home with <see cref="GameManager"/>, additive tint, idle frame shuffle + pulse.
/// </summary>
public class BackgroundObject : MonoBehaviour
{
    public float[] properLoc = { 0, 1.75f };
    public float properScale = 7f;
    public float properOpacity = 1f;

    // Opacity when scattered; at home, overlaps add toward white (additive).
    [Range(0.01f, 1f)] public float awayOpacity = 0.2f;

    // Lower = linger at each end of the time blend, sharper through the middle. 1 = linear.
    [Range(0.05f, 1f)] public float approachCurvePower = 0.2f;

    float awayX, awayY, awayScale;
    float baseScale;
    // 1 = at scheduled "home" in the time window; dampens pulse near home.
    float homeBlend;

    // One of R, G, B at α/3 so overlapping at home reads as white.
    Color baseTint;
    const float ChannelAlpha = 1f / 3f;

    // Assign in the inspector so WebGL includes the shader; else Shader.Find (may miss without Always Included Shaders).
    [SerializeField] Shader additiveShader;

    private SpriteRenderer sr;
    private Sprite[] frames;
    public float frameRate = 10f;

    private float pulseScale = 1f;
    private float pulseSpeed = 2f;
    private float pulseAmplitude = 0.1f;

    void Start()
    {
        InitializeAnimation();
        SetHomeValues();
        SetAwayValues();
        InvokeRepeating(nameof(MoveHome), 0f, 0.5f);
    }

    void Update()
    {
        Pulse();
    }

    void InitializeAnimation()
    {
        sr = GetComponent<SpriteRenderer>();
        frames = Resources.LoadAll<Sprite>("Circles");
        InvokeRepeating(nameof(Animate), 0f, 1f / frameRate);
        SetAdditiveBlending();

        pulseScale = Random.Range(0.9f, 1.1f);
        pulseSpeed = Random.Range(0.1f, 1f);
        pulseAmplitude = Random.Range(0.05f, 0.10f);
    }

    void SetAdditiveBlending()
    {
        Shader shader = additiveShader != null ? additiveShader : Shader.Find("Custom/Sprites Additive");
        if (shader != null)
            sr.material = new Material(shader);
    }

    void SetHomeValues()
    {
        float randOffset = Random.Range(0.1f, 0.1f);
        properLoc[0] += randOffset;
        properLoc[1] += randOffset;
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
        baseScale = awayScale;
        transform.localScale = new Vector3(baseScale, baseScale, 1);
        sr.color = new Color(baseTint.r, baseTint.g, baseTint.b, awayOpacity);
    }

    void MoveHome()
    {
        // t = 1 at "home" in the availability blend (see GameManager).
        float t = GameManager.S.GetAvailabilityHomeBlend(approachCurvePower);

        Vector3 awayPos = new Vector3(awayX, awayY, 0);
        Vector3 homePos = new Vector3(properLoc[0], properLoc[1], 0);
        transform.localPosition = Vector3.Lerp(awayPos, homePos, t);

        baseScale = Mathf.Lerp(awayScale, properScale, t);
        homeBlend = t;

        float alpha = Mathf.Lerp(awayOpacity, properOpacity, t);
        sr.color = new Color(baseTint.r, baseTint.g, baseTint.b, alpha);
    }

    void Animate()
    {
        int frameIndex = Random.Range(0, frames.Length);
        sr.sprite = frames[frameIndex];
    }

    void Pulse()
    {
        // Softer pulse as we approach home (homeBlend → 1).
        float effectiveAmplitude = pulseAmplitude * (1f - homeBlend);
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float pulseMult = Mathf.Lerp(1f, 1f + effectiveAmplitude, t);
        float s = baseScale * pulseScale * pulseMult;
        transform.localScale = new Vector3(s, s, 1);
    }
}
