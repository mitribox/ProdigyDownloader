using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;

namespace ClonerApp.Engine;

public static class ScheduleCalculator
{
    public static DateTime? CalculateNextRun(Project project, DateTime utcNow)
    {
        return project.RunMode switch
        {
            RunMode.Once => null,
            RunMode.Monitor => utcNow.AddMinutes(Math.Max(1, project.MonitorIntervalMinutes ?? 30)),
            RunMode.Schedule => CalculateSchedule(project, utcNow),
            _ => null
        };
    }

    public static DateTime? CalculateInitialNextRun(Project project, DateTime utcNow) =>
        CalculateNextRun(project, utcNow);

    private static DateTime? CalculateSchedule(Project project, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(project.ScheduleTime) ||
            !TimeSpan.TryParse(project.ScheduleTime, out var timeOfDay))
        {
            return utcNow.AddDays(1);
        }

        var days = ParseDays(project.ScheduleDays);
        var localNow = utcNow.ToLocalTime();
        var candidate = localNow.Date.Add(timeOfDay);

        for (var i = 0; i < 8; i++)
        {
            var day = candidate.DayOfWeek;
            if ((days.Count == 0 || days.Contains(day)) && candidate > localNow)
                return candidate.ToUniversalTime();

            candidate = candidate.AddDays(1);
        }

        return utcNow.AddDays(1);
    }

    private static HashSet<DayOfWeek> ParseDays(string? scheduleDays)
    {
        var set = new HashSet<DayOfWeek>();
        if (string.IsNullOrWhiteSpace(scheduleDays))
            return set;

        foreach (var part in scheduleDays.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<DayOfWeek>(part, true, out var day))
                set.Add(day);
            else if (int.TryParse(part, out var n) && n is >= 0 and <= 6)
                set.Add((DayOfWeek)n);
        }

        return set;
    }
}
