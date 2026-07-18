namespace EtlAnalytics.App.Models;

public class AutoSysRuntimeJob
{
    public int JobRunId
    {
        get; set;
    }
    public string JobName { get; set; } = string.Empty;
    public string? JobStatus
    {
        get; set;
    }
    public decimal? RunTimeInMinutes
    {
        get; set;
    }
    public DateTime InsertedAt
    {
        get; set;
    }
}
