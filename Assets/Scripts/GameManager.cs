using UnityEngine;
using System;
using TMPro;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{

    public static GameManager S;
    
    public TMP_Text informationText;    
    
    public int availableHour = 18;
    public int availableMinute = 0;
    public int secondsAdjustment = 0;
    public int durationMinutes = 5;
    // How long the performed event will last after word entry
    public int eventMinutes = 2;

    public bool enforceTimeWindow = true;
    bool? _gameAvailableOverride;
    DateTime? _userWindowEndTime;

    public bool IsGameAvailable => _gameAvailableOverride ?? (!enforceTimeWindow || IsWithinAvailabilityWindow());

    bool _wasGameAvailable;

    public float startTime;

    void Awake() {
        S = this;
    }

    void Start() {
        // Randomly adjust available seconds so that the start time is somehwere within a 5 minute window
        secondsAdjustment = UnityEngine.Random.Range(0, 300);

        _wasGameAvailable = IsGameAvailable;  // So we don't trigger enter/exit on first Update
        // Ensure input field is shown immediately if the game starts in an available state,
        // regardless of script execution order.
        if (_wasGameAvailable && WordInputManager.S != null)
        {
            WordInputManager.S.ShowInputField();
        }

    }

    void Update() {
        bool nowAvailable = IsGameAvailable;

        // Only show/hide and update state on transition into or out of the time window
        if (!_wasGameAvailable && nowAvailable) {
            // Just transitioned into availability window
            GetTextInput();
            if (WordInputManager.S != null)
                WordInputManager.S.ShowInputField();
            _userWindowEndTime = null;  // New window: clear "user ended" so ramp uses scheduled end next time
        } else if (_wasGameAvailable && !nowAvailable) {
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

    /// <summary>Minutes until the game becomes available (0 if already available).</summary>
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

    /// <summary>Minutes since the availability window ended (0 if currently available). Use for move-away ramp from home.</summary>
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

    /// <summary>Call when the user ends the session by entering a word. Move-away ramp will key off this time instead of scheduled window end.</summary>
    public void NotifyUserEndedWindow(DateTime atTime)
    {
        _userWindowEndTime = atTime;
    }

    /// <summary>Minutes since the effective window end (scheduled end, or word-entry when user entered a word). 0 if still in window. Circles use this to start moving away.</summary>
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

    /// <summary>Effective window end as minutes from midnight (for ramp curve). When user entered a word, this is word-entry time so circles move away immediately.</summary>
    public double GetEffectiveWindowEndMinutesSinceMidnight()
    {
        if (_userWindowEndTime.HasValue)
            return _userWindowEndTime.Value.TimeOfDay.TotalMinutes;
        var now = DateTime.Now;
        var scheduledEnd = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0).AddSeconds(secondsAdjustment).AddMinutes(durationMinutes);
        return scheduledEnd.TimeOfDay.TotalMinutes;
    }

    private void GetTextInput(){
        // informationText.text = "begin";
    }

    public void SetGameAvailable(bool available) {
        _gameAvailableOverride = available;
    }
}
