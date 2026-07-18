using System.Collections.Generic;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.App.Models;

public class BusinessRuleContext : RuleExecutionContext
{
    public List<JilJob> Jobs { get; set; } = new();
    public List<AutoSysJobToPackage> JobToPackageMappings { get; set; } = new();
}
