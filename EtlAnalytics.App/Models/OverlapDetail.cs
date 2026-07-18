namespace EtlAnalytics.App.Models;

public class OverlapDetail
{
    public string JobNameA { get; set; } = string.Empty;
    public string JobNameB { get; set; } = string.Empty;
    public TimeSpan OverlapStart { get; set; }
    public TimeSpan OverlapEnd { get; set; }
    public double OverlapDurationMinutes => (OverlapEnd - OverlapStart).TotalMinutes;
}
