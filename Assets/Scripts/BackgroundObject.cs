using UnityEngine;
using System.Collections;

public class BackgroundObject : MonoBehaviour
{
    public float[] properLoc = { 0, 1.75f };
    public float properScale = 7f;
    public float properOpacity = 1f;

    // Opacity when scattered; at home, overlaps add toward white (additive).
    [Range(0.01f, 1f)] public float awayOpacity = 0.2f;

    // Lower = linger at each end of the time blend, sharper through the middle. 1 = linear.
    [Range(0.05f, 1f)] public float approachCurvePower = 0.2f;

    // Wander when scattered; Become still when home.
    public float wanderDistance = 10f;
    public float wanderSpeed = 1f;

    float awayX, awayY, awayScale;
    float baseScale;
    // 1 = at scheduled "home" in the time window; dampens pulse near home.
    float homeBlend;
    Vector3 baseLocalPos;

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
        StartCoroutine(Wander());
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

        baseLocalPos = new Vector3(awayX, awayY, 0);
        transform.localPosition = baseLocalPos;
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
        baseLocalPos = Vector3.Lerp(awayPos, homePos, t);

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

    private IEnumerator Wander()
    {
        // Per-instance seeds so circles don't move in lockstep.
        float seedX = Random.Range(0f, 1000f);
        float seedY = Random.Range(0f, 1000f);

        // Randomize the Wander Speed
        wanderSpeed = Random.Range(0.1f, 0.5f);

        Vector2 offset = Vector2.zero;
        Vector2 velocity = Vector2.zero;

        while (true)
        {
            // As we approach "active time"/home (homeBlend -> 1), wander radius approaches 0.
            float radius = wanderDistance * (1f - homeBlend);

            // Creature-like meander: smooth noise-driven steering, not circular orbit.
            float t = Time.time * Mathf.Max(0.01f, wanderSpeed);
            float nx = (Mathf.PerlinNoise(seedX, t) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(seedY, t) - 0.5f) * 2f;
            Vector2 desiredDir = new Vector2(nx, ny);
            if (desiredDir.sqrMagnitude < 0.001f) desiredDir = Vector2.right;
            desiredDir.Normalize();

            // Interpret wanderSpeed as "how fast it scoots", scaled by wander radius.
            float maxSpeed = wanderSpeed * Mathf.Max(0.5f, radius * 0.25f);
            Vector2 desiredVel = desiredDir * maxSpeed;

            float dt = Time.deltaTime;
            // Turn smoothing: higher -> snappier, lower -> floatier.
            float turn = 1f - Mathf.Exp(-3f * dt);
            velocity = Vector2.Lerp(velocity, desiredVel, turn);
            offset += velocity * dt;

            // Soft boundary: stay within radius, bounce gently if we hit the edge.
            float r = Mathf.Max(0f, radius);
            float mag = offset.magnitude;
            if (mag > r && mag > 0.0001f)
            {
                Vector2 n = offset / mag;
                offset = n * r;
                velocity = Vector2.Reflect(velocity, -n) * 0.35f;
            }

            transform.localPosition = baseLocalPos + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }
    }
}
