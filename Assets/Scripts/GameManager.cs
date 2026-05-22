using UnityEngine;
using System;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Daily availability window, input visibility, and a 0–1 "home" blend for visuals / audio easing.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager S;

    // Wipe the existing words from the PlayerPrefs.
    public bool wipeExistingWords = false;

    public TMP_Text informationText;

    // Fader
    public Image fader;
    public float fadeDuration = 1.5f;

    public int availableHour = 18;
    public int availableMinute = 0;
    // Random 0–299 s so the window start varies slightly each run.
    public int secondsAdjustment = 0;
    public int durationMinutes = 5;
    // Minutes used to ease toward / away from the window in GetAvailabilityHomeBlend.
    public int eventMinutes = 2;

    public bool enforceTimeWindow = true;
    bool? _gameAvailableOverride;
    // Set when the user submits a word so the "away" ramp can use that moment.
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
        if (wipeExistingWords) {
            WipeExistingWords();
        }
        
        secondsAdjustment = Random.Range(0, 300);
        _wasGameAvailable = IsGameAvailable;
        // Show input immediately if we boot already inside the window (any script order).
        if (_wasGameAvailable && WordInputManager.S != null)
            WordInputManager.S.ShowInputField();

        // Fade in the game
        StartCoroutine(FadeInGame());
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
    /// 1 = fully "home" (aligned), 0 = furthest away. Circles ease over 12h before/after the daily anchor;
    /// <paramref name="eventMinutes"/> sharpens the last minutes into the window and out after it ends.
    /// </summary>
    public float GetAvailabilityHomeBlend(float approachCurvePower = 1f)
    {
        if (!enforceTimeWindow || IsGameAvailable)
            return 1f;

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

        // 12h before window → ease away → home; linger away, then final ramp into the window.
        float rawReturn = Mathf.Clamp01(minutesUntil / halfCycleMinutes);
        float tReturn = 1f - Mathf.Pow(rawReturn, approachCurvePower);

        if (minutesUntil <= rampMinutes)
        {
            float tAtRampStart = 1f - Mathf.Pow(rampMinutes / halfCycleMinutes, approachCurvePower);
            float rampT = 1f - minutesUntil / rampMinutes;
            return Mathf.Lerp(tAtRampStart, 1f, rampT);
        }

        return tReturn;
    }

    void WipeExistingWords()
    {
        PlayerPrefs.DeleteKey("lautir_words");
        PlayerPrefs.Save();
        WordDisplay.S.DisplayWords();
        WordInputManager.S.ClearSavedWords();
        wipeExistingWords = false;
    }
}
