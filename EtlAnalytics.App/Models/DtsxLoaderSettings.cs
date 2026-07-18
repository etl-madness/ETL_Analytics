namespace EtlAnalytics.App.Models;

public class DtsxLoaderSettings
{
    // Mirrors DTSXDataLoader.Console Models/Options.cs
    public bool IsVerbose { get; set; }
    public string? Path { get; set; }
    public bool IsSql { get; set; }
    public bool IsLite { get; set; }
    public bool IsTruncate { get; set; }
    public string? Extension { get; set; }
    public string? OutputDirectory { get; set; }

    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
