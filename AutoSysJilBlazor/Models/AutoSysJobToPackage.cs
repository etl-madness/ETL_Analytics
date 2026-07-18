using System;

namespace AutoSysJilBlazor.Models;

public class AutoSysJobToPackage
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;
}
