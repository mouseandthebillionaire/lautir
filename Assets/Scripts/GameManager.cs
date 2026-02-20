using UnityEngine;
using System;
using TMPro;

public class GameManager : MonoBehaviour
{

    public static GameManager S;
    
    public TMP_Text informationText;    
    
    public int availableStartHour = 18;
    public int availableStartMinute = 0;
    public int availableEndHour = 22;
    public int availableEndMinute = 0;

    public bool enforceTimeWindow = true;
    public bool IsGameAvailable => !enforceTimeWindow || IsWithinAvailabilityWindow();

    void Awake(){
        S = this;
    }
    
    void Start()
    {
        if (enforceTimeWindow && !IsGameAvailable)
        {
            OnOutsideAvailabilityWindow();
        } else {
            // Let the Player Enter a word
            GetTextInput();
        }
    }

    void Update()
    {
        // Optional: re-check each frame if you need to disable mid-session when window closes
        // if (enforceTimeWindow && !IsGameAvailable) { ... }
    }

    bool IsWithinAvailabilityWindow()
    {
        var now = DateTime.Now.TimeOfDay;
        var start = new TimeSpan(availableStartHour, availableStartMinute, 0);
        var end = new TimeSpan(availableEndHour, availableEndMinute, 0);

        if (start <= end)
            return now >= start && now < end;
        // Window crosses midnight (e.g. 22:00 - 02:00)
        return now >= start || now < end;

    }

    /// <summary>Override or call from UI: show message, block input, or load a "come back later" screen.</summary>
    protected virtual void OnOutsideAvailabilityWindow()
    {
        informationText.text = $"only available between {availableStartHour:D2}:{availableStartMinute:D2} and {availableEndHour:D2}:{availableEndMinute:D2}. \n please come back later.";
        // TODO: e.g. show UI panel, disable player input, or load a "come back later" scene
    }

    /// <summary>Minutes until the game becomes available (0 if already available).</summary>
    public double MinutesUntilAvailable()
    {
        if (IsGameAvailable) return 0;
        var now = DateTime.Now;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, availableStartHour, availableStartMinute, 0);
        if (now < todayStart)
            return (todayStart - now).TotalMinutes;
        var tomorrowStart = todayStart.AddDays(1);
        return (tomorrowStart - now).TotalMinutes;
    }

    private void GetTextInput(){
        informationText.text = "enter a five letter word";
    }
}
