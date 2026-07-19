using System;

namespace EtlAnalytics.RulesEngine.Models;

public class BusinessRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RuleType RuleType { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public int? ConnectionId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
