using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EtlAnalytics.RulesEngine.Models;

public class RuleExecutionContext
{
    public DateTime ExecutionTime { get; set; } = DateTime.Now;
    public object? PreviousResult { get; set; }
    public Dictionary<int, object?> StepResults { get; set; } = new();
    public Action<string>? Log { get; set; }
    public Func<string, Task<object?>>? RunBundle { get; set; }
}
