namespace EtlAnalytics.RulesEngine.Models;

public class BusinessRuleBundleItem
{
    public int Id { get; set; }
    public int BundleId { get; set; }
    public int RuleId { get; set; }
    public int SequenceOrder { get; set; }
    
    // UI Helpers
    public string? RuleName { get; set; }
    public RuleType? RuleType { get; set; }
}
