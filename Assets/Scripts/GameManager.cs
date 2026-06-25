using UnityEngine;
using System;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager S;

    // Wipe the existing words from the PlayerPrefs.
    public bool wipeExistingWords = false;

    [Tooltip("Dev only: each load acts as a new ritual day so you can walk through all 5 days by reloading.")]
    public bool testing = false;

    public int currentDay = 0;
    
    /// <summary>Ritual day (0–5): ritual slots committed so far (words + missed blanks). Synced to <see cref="GlobalVariables.currentDay"/>.</summary>
    public void SyncCurrentDay()
    {
        currentDay = WordInputManager.RitualDayIndex;
        if (GlobalVariables.S != null)
            GlobalVariables.S.currentDay = currentDay;
        Debug.Log("Current day: " + currentDay);
    }

    public TMP_Text informationText;

    // Fader
    public Image fader;
    public float fadeDuration = 1.5f;

    public int availableHour = 18;
    public int availableMinute = 0;
    // How long (minutes) the game stays available once the countdown finishes.
    public int availableDuration = 5;

    // Per-session delay (seconds) the user must wait AFTER reaching the open window.
    public int waitTime = 0;
    public int waitMin = 0;
    public int waitMax = 300;
    public int waitTimeRemaining = 0;

    // Minutes used to sharpen the final ramp into the window and out after it ends.
    public int eventMinutes = 2;
    // Blend value (0 = scattered, 1 = home) at the moment the final countdown BEGINS.
    // The 12h approach eases up to this value, then the countdown carries it the rest of the
    // way to 1. Lower it to make the countdown a more visibly dramatic "moving into place".
    [Range(0f, 1f)] public float preAlignmentBlend = 0.4f;
    // Minutes over which the circles slowly drift back to their Far locations after closing.
    public float awayRampMinutes = 720f;
    // Ease-out curve for the drift away. Lower = snappier initial departure; higher = gentler.
    [Range(0.05f, 1f)] public float awayCurvePower = 0.5f;

    public bool enforceTimeWindow = true;
    private bool gameAvailable = false;

    public bool IsGameAvailable => gameAvailable;

    // WaitingForWindow → CountingDown → Available → Closed
    enum Phase { WaitingForWindow, CountingDown, Available, Closed }
    Phase _phase = Phase.WaitingForWindow;

    float _countdownStartRealtime;
    float _availableStartRealtime;
    float _closedStartRealtime;
    // Set when the game closes (word entry or duration) so the "away" ramp can use that moment.
    DateTime? _userWindowEndTime;
    bool _wordEnteredThisWindow;

    void Awake()
    {
        S = this;
        // Display-only text shouldn't intercept clicks meant for the input field.
        if (informationText != null) informationText.raycastTarget = false;
    }

    void Start()
    {
        if (wipeExistingWords) {
            WipeExistingWords();
        }

        if (WordInputManager.S != null)
        {
            if (testing)
                WordInputManager.S.PrepareTestingNewDayOnLoad();
            else
                WordInputManager.S.AdvanceForMissedCalendarDays(this);
        }

        SyncCurrentDay();

        // Set the random wait time for this session.
        waitTime = Random.Range(waitMin, waitMax);
        waitTimeRemaining = waitTime;

        // Fade in the game
        fader.raycastTarget = false;
        StartCoroutine(FadeInGame());
    }

    void Update()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0)
            || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            RnboWebBridge.ResumeAudioOnUserGesture();
        }

        switch (_phase)
        {
            case Phase.WaitingForWindow:
                // Start the countdown once we reach the window (or window isn't enforced).
                if (IsWithinAvailabilityWindow() || !enforceTimeWindow)
                {
                    _countdownStartRealtime = Time.realtimeSinceStartup;
                    _phase = Phase.CountingDown;
                }
                break;

            case Phase.CountingDown:
                FinalCountdown();
                break;

            case Phase.Available:
                // Auto-close once the available duration has passed.
                if (Time.realtimeSinceStartup - _availableStartRealtime >= availableDuration * 60f)
                    SetGameAvailable(false);
                break;

            case Phase.Closed:
                break;
        }
    }

    // fade int he game each time
    private IEnumerator FadeInGame()
    {
        fader.color = new Color(fader.color.r, fader.color.g, fader.color.b, 1f);
        float elapsed = 0f;
        float duration = fadeDuration;
        
        // Give it a second
        yield return new WaitForSeconds(0.5f);

        // Start fading
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float faderAlpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            fader.color = new Color(fader.color.r, fader.color.g, fader.color.b, faderAlpha);
            yield return null;
        }
        fader.color = new Color(fader.color.r, fader.color.g, fader.color.b, 0f);
        
    }

    bool IsWithinAvailabilityWindow()
    {
        var now = DateTime.Now.TimeOfDay;
        var start = new TimeSpan(availableHour, availableMinute, 0);
        var end = start + TimeSpan.FromMinutes(availableDuration);
        return now >= start && now <= end;
    }

    /// <summary>True once today's ritual window has ended (used to record a missed day on launch).</summary>
    public bool IsPastTodaysRitualWindow()
    {
        if (!enforceTimeWindow) return false;
        var now = DateTime.Now;
        var end = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0)
            .AddMinutes(availableDuration);
        return now > end;
    }

    // Runs every frame while counting down. When the random waitTime elapses, the game opens.
    private void FinalCountdown()
    {
        float elapsed = Time.realtimeSinceStartup - _countdownStartRealtime;
        waitTimeRemaining = Mathf.Max(0, Mathf.CeilToInt(waitTime - elapsed));

        if (elapsed >= waitTime)
            SetGameAvailable(true);
    }

    // 0 → countdown just started, 1 → countdown finished (final alignment progress).
    float CountdownProgress01()
    {
        if (waitTime <= 0) return 1f;
        return Mathf.Clamp01((Time.realtimeSinceStartup - _countdownStartRealtime) / waitTime);
    }

    /// <summary>Minutes until the next window opens (0 if already available).</summary>
    public double MinutesUntilAvailable()
    {
        if (IsGameAvailable) return 0;
        var now = DateTime.Now;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0);
        if (now < todayStart)
            return (todayStart - now).TotalMinutes;
        var tomorrowStart = todayStart.AddDays(1);
        return (tomorrowStart - now).TotalMinutes;
    }

    /// <summary>Minutes since today's scheduled window ended (0 if still inside window).</summary>
    public double MinutesSinceAvailableEnded()
    {
        if (IsGameAvailable) return 0;
        var now = DateTime.Now;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0);
        var windowEnd = todayStart.AddMinutes(availableDuration);
        if (now >= windowEnd)
            return (now - windowEnd).TotalMinutes;
        var yesterdayEnd = todayStart.AddDays(-1).AddMinutes(availableDuration);
        return (now - yesterdayEnd).TotalMinutes;
    }

    /// <summary>Minutes since effective end: word entry / close time if set, else scheduled end.</summary>
    public double MinutesSinceEffectiveWindowEnd()
    {
        if (IsGameAvailable) return 0;
        if (_userWindowEndTime.HasValue)
        {
            var since = (DateTime.Now - _userWindowEndTime.Value).TotalMinutes;
            return since > 0 ? since : 0;
        }
        return MinutesSinceAvailableEnded();
    }

    /// <summary>Effective window end as minutes since midnight (for ramp math).</summary>
    public double GetEffectiveWindowEndMinutesSinceMidnight()
    {
        if (_userWindowEndTime.HasValue)
            return _userWindowEndTime.Value.TimeOfDay.TotalMinutes;
        var now = DateTime.Now;
        var scheduledEnd = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0).AddMinutes(availableDuration);
        return scheduledEnd.TimeOfDay.TotalMinutes;
    }

    /// <summary>
    /// 1 = fully "home" (aligned), 0 = furthest away. Circles ease over 12h before/after the daily
    /// anchor; the approach tops out at preAlignmentBlend, and the final countdown finishes alignment.
    /// </summary>
    public float GetAvailabilityHomeBlend(float approachCurvePower = 1f)
    {
        // Fully home while the game is open.
        if (gameAvailable)
            return 1f;

        // Final alignment phase: ease preAlignmentBlend → 1 across the random countdown.
        // Checked before the enforceTimeWindow shortcut so the countdown is always honored.
        if (_phase == Phase.CountingDown)
            return Mathf.Lerp(preAlignmentBlend, 1f, CountdownProgress01());

        // After closing (word entered or duration elapsed): slowly drift home → away.
        // Starts at 1 and eases toward 0 over awayRampMinutes, so circles ease back to Far.
        if (_phase == Phase.Closed)
        {
            float minutesSinceClose = (Time.realtimeSinceStartup - _closedStartRealtime) / 60f;
            float t = awayRampMinutes <= 0f ? 1f : Mathf.Clamp01(minutesSinceClose / awayRampMinutes);
            float awayPow = Mathf.Clamp(awayCurvePower, 0.05f, 1f);
            // Move away quickly at first, then slow down as the circles get further out (ease-out).
            return 1f - Mathf.Pow(t, awayPow);
        }

        // No time window enforced and not yet counting/available: treat as scattered.
        if (!enforceTimeWindow)
            return 0f;

        const float halfCycleMinutes = 720f; // 12h scattered ↔ 12h approaching home
        float rampMinutes = Mathf.Max(0.01f, eventMinutes);
        approachCurvePower = Mathf.Clamp(approachCurvePower, 0.05f, 1f);

        float minutesUntil = (float)MinutesUntilAvailable();

        if (minutesUntil >= halfCycleMinutes)
        {
            // After effective window end: ease home → away until 12h before next open.
            float raw = Mathf.Clamp01((minutesUntil - halfCycleMinutes) / halfCycleMinutes);
            float tCurve = 1f - Mathf.Pow(1f - raw, approachCurvePower);

            float minutesSinceEnd = (float)MinutesSinceEffectiveWindowEnd();
            if (minutesSinceEnd > 0f && minutesSinceEnd < rampMinutes)
            {
                double effectiveEndMin = GetEffectiveWindowEndMinutesSinceMidnight();
                float minutesAtRampEnd = 1440f - (float)effectiveEndMin - rampMinutes;
                float rawAtRampEnd = Mathf.Clamp01((minutesAtRampEnd - halfCycleMinutes) / halfCycleMinutes);
                float tAtRampEnd = 1f - Mathf.Pow(1f - rawAtRampEnd, approachCurvePower);
                float rampT = minutesSinceEnd / rampMinutes;
                return Mathf.Lerp(1f, tAtRampEnd, rampT);
            }

            return tCurve;
        }

        // 12h before window → ease away → home; linger away, then final ramp toward the window.
        // The approach tops out at preAlignmentBlend; the countdown finishes alignment.
        float rawReturn = Mathf.Clamp01(minutesUntil / halfCycleMinutes);
        float tReturn = 1f - Mathf.Pow(rawReturn, approachCurvePower);

        if (minutesUntil <= rampMinutes)
        {
            float tAtRampStart = 1f - Mathf.Pow(rampMinutes / halfCycleMinutes, approachCurvePower);
            float rampT = 1f - minutesUntil / rampMinutes;
            return Mathf.Lerp(tAtRampStart, 1f, rampT) * preAlignmentBlend;
        }

        return tReturn * preAlignmentBlend;
    }

    // Flip availability: called after the countdown, from WordInputManager on word entry, or on auto-close.
    public void SetGameAvailable(bool available)
    {
        if (!available && gameAvailable && !_wordEnteredThisWindow && WordInputManager.S != null)
            WordInputManager.S.RecordMissedRitualDay();

        gameAvailable = available;

        if (available)
        {
            _wordEnteredThisWindow = false;
            _availableStartRealtime = Time.realtimeSinceStartup;
            _phase = Phase.Available;
            if (WordInputManager.S != null)
                WordInputManager.S.ShowInputField();
        }
        else
        {
            // Record the close moment (if not already set by word entry) so the away ramp starts here.
            if (!_userWindowEndTime.HasValue)
                _userWindowEndTime = DateTime.Now;
            _closedStartRealtime = Time.realtimeSinceStartup;
            _phase = Phase.Closed;
            if (WordInputManager.S != null)
                WordInputManager.S.HideInputField();
        }
    }

    // Called by WordInputManager when the user submits a word, so the away ramp eases from that moment.
    public void NotifyUserEndedWindow(DateTime atTime)
    {
        _userWindowEndTime = atTime;
    }

    public void NotifyWordEnteredThisWindow() => _wordEnteredThisWindow = true;

    void WipeExistingWords()
    {
        PlayerPrefs.DeleteKey("lautir_words");
        PlayerPrefs.DeleteKey("lautir_ritual_slot");
        PlayerPrefs.DeleteKey("lautir_last_ritual_date");
        PlayerPrefs.Save();
        WordInputManager.S.ClearSavedWords();
        SyncCurrentDay();
        wipeExistingWords = false;
    }
}
