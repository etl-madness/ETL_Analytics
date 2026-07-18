using System;

namespace EtlAnalytics.RulesEngine.Models;

public class BusinessRuleHistory
{
    public int Id { get; set; }
    public int RuleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.Now;
    public string ChangedBy { get; set; } = "System";
}
