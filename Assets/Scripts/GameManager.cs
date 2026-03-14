using UnityEngine;
using System;
using TMPro;

public class GameManager : MonoBehaviour
{

    public static GameManager S;
    
    public TMP_Text informationText;    
    
    public int availableHour = 18;
    public int availableMinute = 0;
    public int durationMinutes = 5;
    // How long the performed event will last after word entry
    public int eventMinutes = 2;

    public bool enforceTimeWindow = true;
    /// <summary>When set via SetGameAvailable(), overrides the time-window check. Null = use time window.</summary>
    bool? _gameAvailableOverride;
    /// <summary>When set, the move-away ramp uses this as "window end" instead of scheduled end (user entered a word).</summary>
    DateTime? _userWindowEndTime;

    public bool IsGameAvailable => _gameAvailableOverride ?? (!enforceTimeWindow || IsWithinAvailabilityWindow());

    bool _wasGameAvailable;

    void Awake() {
        S = this;
    }

    void Start() {
        _wasGameAvailable = IsGameAvailable;  // So we don't trigger enter/exit on first Update
    }

    void Update() {
        bool nowAvailable = IsGameAvailable;

        if (nowAvailable) {
            GetTextInput();
            WordInputManager.S.ShowInputField();
        } else {
            OnOutsideAvailabilityWindow();
            WordInputManager.S.HideInputField();
        }

        // Only show/hide on transition into or out of the time window
        if (_wasGameAvailable && !nowAvailable) {
        } else if (!_wasGameAvailable && nowAvailable) {
            _userWindowEndTime = null;  // New window: clear "user ended" so ramp uses scheduled end next time
        }

        _wasGameAvailable = nowAvailable;
    }

    bool IsWithinAvailabilityWindow()
    {
        var now = DateTime.Now.TimeOfDay;
        var start = new TimeSpan(availableHour, availableMinute, 0);
        var end = start + TimeSpan.FromMinutes(durationMinutes);
        return now >= start && now <= end;
    }

    /// <summary>Override or call from UI: show message, block input, or load a "come back later" screen.</summary>
    protected virtual void OnOutsideAvailabilityWindow()
    {
        //informationText.text = $"only available between {availableHour:D2}:{availableMinute:D2} and {availableHour:D2}:{availableMinute + durationMinutes:D2}. \n please come back later.";
        // TODO: e.g. show UI panel, disable player input, or load a "come back later" scene
        informationText.text = "";
    }

    /// <summary>Minutes until the game becomes available (0 if already available).</summary>
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

    /// <summary>Minutes since the availability window ended (0 if currently available). Use for move-away ramp from home.</summary>
    public double MinutesSinceAvailableEnded()
    {
        if (IsGameAvailable) return 0;
        var now = DateTime.Now;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0);
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
        var scheduledEnd = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0).AddMinutes(durationMinutes);
        return scheduledEnd.TimeOfDay.TotalMinutes;
    }

    private void GetTextInput(){
        informationText.text = "begin";
    }

    public void SetGameAvailable(bool available) {
        _gameAvailableOverride = available;
    }
}
