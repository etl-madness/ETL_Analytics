using System;
using System.Collections.Generic;

namespace EtlAnalytics.App.Models;

public class JilJob
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string? JobType { get; set; }
    public string? Machine { get; set; }
    public string? Owner { get; set; }
    public string? Permission { get; set; }
    public string? DateConditions { get; set; }
    public string? DaysOfWeek { get; set; }
    public string? StartMins { get; set; }
    public string? StartTimes { get; set; }
    public string? RunWindow { get; set; }
    public string? Condition { get; set; }
    public string? Description { get; set; }
    public string? BoxName { get; set; }
    public string? Command { get; set; }
    public string? StdOutFile { get; set; }
    public string? StdErrFile { get; set; }
    public string? AlarmIfFail { get; set; }
    public string? Application { get; set; }
    public string? Group { get; set; }
    public string? JobTerminator { get; set; }
    public string? Profile { get; set; }
    public string? Timezone { get; set; }
    public string? MaxRunAlarm { get; set; }
    public string? MinRunAlarm { get; set; }
    public string? ProcessName { get; set; }
    public string? ServiceName { get; set; }
    public string? ExportTime { get; set; }
    public string? RawText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Dictionary<string, string> AdditionalProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
