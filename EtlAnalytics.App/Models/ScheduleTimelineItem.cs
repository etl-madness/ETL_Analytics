namespace EtlAnalytics.App.Models;

public class ScheduleTimelineItem
{
    public string JobName { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public TimeSpan Start
    {
        get; set;
    }
    public TimeSpan End
    {
        get; set;
    }
    public string Label { get; set; } = string.Empty;
    public bool IsOverlapping { get; set; }
    public List<string> OverlappingWith { get; set; } = new();
}
