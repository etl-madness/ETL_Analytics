using System;
using System.Collections.Generic;

namespace EtlAnalytics.RulesEngine.Models;

public class BusinessRuleBundle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<BusinessRuleBundleItem> Items { get; set; } = new();
}
