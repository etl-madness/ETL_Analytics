namespace EtlAnalytics.App.Models;

public class GanttRow
{
    public string JobName { get; set; } = string.Empty;
    public string? PackageName { get; set; }
    public string? DatabaseServer { get; set; }
    public string? Database { get; set; }
    public string? Schema { get; set; }
    public string? ObjectName { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal RunTimeMinutes { get; set; }
    public int OverlapFlag { get; set; }
    public int OverlapCount { get; set; }

    public string Id { get; set; } = string.Empty;
    public string? ParentId { get; set; }

    public bool IsOverlapping => OverlapFlag == 1;
}
