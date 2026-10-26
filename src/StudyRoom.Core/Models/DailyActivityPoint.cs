namespace StudyRoom.Core.Models;

public sealed class DailyActivityPoint
{
    public string DayLabel { get; set; } = string.Empty;
    public double Hours { get; set; }
    public DateTime Date { get; set; }
}
