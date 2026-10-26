namespace StudyRoom.Core.Models;

public sealed class DashboardMetrics
{
    public double FocusHoursToday { get; set; }
    public string FocusTimeTrend { get; set; } = string.Empty;
    public int CompletedTasksToday { get; set; }
    public int TotalTasksToday { get; set; }
    public string TaskProgressText { get; set; } = string.Empty;
    public int StudyStreakDays { get; set; }
    public string StreakBadge { get; set; } = string.Empty;
    public double TotalStudyHours { get; set; }
    public string StudentRank { get; set; } = string.Empty;
}
