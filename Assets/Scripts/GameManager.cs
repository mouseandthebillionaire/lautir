using UnityEngine;
using System;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// Daily availability window, input visibility, and a 0–1 "home" blend for visuals / audio easing.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager S;

    public TMP_Text informationText;

    public int availableHour = 18;
    public int availableMinute = 0;
    /// <summary>Random 0–299 s so the window start varies slightly each run.</summary>
    public int secondsAdjustment = 0;
    public int durationMinutes = 5;
    /// <summary>Minutes used to ease toward / away from the window in <see cref="GetAvailabilityHomeBlend"/>.</summary>
    public int eventMinutes = 2;

    public bool enforceTimeWindow = true;
    bool? _gameAvailableOverride;
    /// <summary>Set when the user submits a word so the "away" ramp can use that moment.</summary>
    DateTime? _userWindowEndTime;

    public bool IsGameAvailable => _gameAvailableOverride ?? (!enforceTimeWindow || IsWithinAvailabilityWindow());

    bool _wasGameAvailable;

    public float startTime;

    void Awake()
    {
        S = this;
    }

    void Start()
    {
        secondsAdjustment = Random.Range(0, 300);
        _wasGameAvailable = IsGameAvailable;
        // Show input immediately if we boot already inside the window (any script order).
        if (_wasGameAvailable && WordInputManager.S != null)
            WordInputManager.S.ShowInputField();
    }

    void Update()
    {
        bool nowAvailable = IsGameAvailable;

        if (!_wasGameAvailable && nowAvailable)
        {
            if (WordInputManager.S != null)
                WordInputManager.S.ShowInputField();
            _userWindowEndTime = null;
        }
        else if (_wasGameAvailable && !nowAvailable)
        {
            if (WordInputManager.S != null)
                WordInputManager.S.HideInputField();
        }

        _wasGameAvailable = nowAvailable;
    }

    bool IsWithinAvailabilityWindow()
    {
        var now = DateTime.Now.TimeOfDay;
        var start = new TimeSpan(availableHour, availableMinute, secondsAdjustment);
        var end = start + TimeSpan.FromMinutes(durationMinutes);
        return now >= start && now <= end;
    }

    /// <summary>Minutes until the next window opens (0 if already available).</summary>
    public double MinutesUntilAvailable()
    {
        if (IsGameAvailable) return 0;
        var now = DateTime.Now;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0).AddSeconds(secondsAdjustment);
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
        var todayStart = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0).AddSeconds(secondsAdjustment);
        var windowEnd = todayStart.AddMinutes(durationMinutes);
        if (now >= windowEnd)
            return (now - windowEnd).TotalMinutes;
        var yesterdayEnd = todayStart.AddDays(-1).AddMinutes(durationMinutes);
        return (now - yesterdayEnd).TotalMinutes;
    }

    public void NotifyUserEndedWindow(DateTime atTime)
    {
        _userWindowEndTime = atTime;
    }

    /// <summary>Minutes since effective end: word entry time if set, else scheduled end.</summary>
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
        var scheduledEnd = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0).AddSeconds(secondsAdjustment).AddMinutes(durationMinutes);
        return scheduledEnd.TimeOfDay.TotalMinutes;
    }

    public void SetGameAvailable(bool available)
    {
        _gameAvailableOverride = available;
    }

    /// <summary>
    /// 1 = fully "home" (inside window or at ramp edges), 0 = away. Used by circles and mixer filter.
    /// </summary>
    public float GetAvailabilityHomeBlend(float approachCurvePower = 1f)
    {
        if (!enforceTimeWindow || IsGameAvailable)
            return 1f;

        float rampMinutes = Mathf.Max(0.01f, eventMinutes);

        // Approaching window start: ease 0 → 1.
        float minutesUntil = (float)MinutesUntilAvailable();
        if (minutesUntil > 0f && minutesUntil < rampMinutes)
        {
            float t = 1f - (minutesUntil / rampMinutes);
            return EaseInOutPow01(t, approachCurvePower);
        }

        // After effective end: ease 1 → 0.
        float minutesSinceEnd = (float)MinutesSinceEffectiveWindowEnd();
        if (minutesSinceEnd > 0f && minutesSinceEnd < rampMinutes)
        {
            float t = 1f - (minutesSinceEnd / rampMinutes);
            return EaseInOutPow01(t, approachCurvePower);
        }

        return 0f;
    }

    // curvePower 1 ≈ linear; lower → stronger ease-in-out (linger at ends).
    static float EaseInOutPow01(float t, float curvePower)
    {
        t = Mathf.Clamp01(t);
        curvePower = Mathf.Clamp(curvePower, 0.05f, 1f);
        float p = Mathf.Lerp(1f, 8f, 1f - curvePower);

        if (t <= 0.5f)
            return 0.5f * Mathf.Pow(t * 2f, p);
        return 1f - 0.5f * Mathf.Pow((1f - t) * 2f, p);
    }
}
