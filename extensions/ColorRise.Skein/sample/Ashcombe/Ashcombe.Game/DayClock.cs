namespace Ashcombe;

/// <summary>
/// The parts of a day in Ashcombe.
/// </summary>
public enum TimeOfDay
{
    Morning,
    Afternoon,
    Evening,
    Night,
}

/// <summary>
/// The day of the story and the time of that day. Time does not pass by itself: the player lets it pass, and the story
/// ends a day.
/// </summary>
public sealed class DayClock
{
    /// <summary>
    /// The day or the time changed.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The day, 1 at the start of the story.
    /// </summary>
    public int Day { get; private set; } = 1;

    /// <summary>
    /// The time of the day.
    /// </summary>
    public TimeOfDay Time { get; private set; } = TimeOfDay.Morning;

    /// <summary>
    /// The time of the day as the scripts read it with <c>time_of_day()</c>: <c>morning</c>, <c>afternoon</c>,
    /// <c>evening</c> or <c>night</c>.
    /// </summary>
    public string TimeName => Time.ToString().ToLowerInvariant();

    /// <summary>
    /// Lets time pass: the next part of the day, and after the night the morning of the next day.
    /// </summary>
    public void Advance()
    {
        if (Time == TimeOfDay.Night)
            Set(Day + 1, TimeOfDay.Morning);
        else
            Set(Day, Time + 1);
    }

    /// <summary>
    /// Ends the day: the morning of the next one.
    /// </summary>
    public void EndDay() => Set(Day + 1, TimeOfDay.Morning);

    /// <summary>
    /// Sets the day and the time: what a save file holds.
    /// </summary>
    public void Set(int day, TimeOfDay time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(day, 1);

        Day = day;
        Time = time;
        Changed?.Invoke();
    }
}
