namespace EtlAnalytics.App.Models;

public class ConflictingJob
{
    public string JobName { get; set; } = string.Empty;
    public string DayName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public double? AvgRunTimeMinutes { get; set; }
    public string DatabaseServer { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int SharedPackageCount { get; set; }
}
