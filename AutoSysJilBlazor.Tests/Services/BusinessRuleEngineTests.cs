using AutoSysJilBlazor.Models;
using AutoSysJilBlazor.Services;
using Microsoft.Extensions.Configuration;

namespace AutoSysJilBlazor.Tests.Services;

[TestFixture]
public class BusinessRuleEngineTests
{
    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder().Build();

    [Test]
    public void Constructor_WithoutDbConnectionString_ThrowsInvalidOperationException()
    {
        var original = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", null);

            Assert.Throws<InvalidOperationException>(() => _ = new BusinessRuleEngine(CreateConfiguration()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", original);
        }
    }

    [Test]
    public async Task ExecuteRuleAsync_CSharpRule_ReturnsResult_AndLogsProgress()
    {
        var original = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", "Server=(local);Database=master;Trusted_Connection=True;");
            var engine = new BusinessRuleEngine(CreateConfiguration());

            var rule = new BusinessRule
            {
                Name = "CountJobs",
                RuleType = RuleType.CSharp,
                Code = "Jobs.Count"
            };

            var context = new BusinessRuleContext
            {
                Jobs =
                {
                    new JilJob { JobName = "JOB_1" },
                    new JilJob { JobName = "JOB_2" }
                }
            };

            var logs = new List<string>();
            var result = await engine.ExecuteRuleAsync(rule, context, logs.Add);

            Assert.That(result, Is.EqualTo(2));
            Assert.That(logs.Any(x => x.Contains("Starting execution of rule: CountJobs", StringComparison.Ordinal)), Is.True);
            Assert.That(logs.Any(x => x.Contains("[CS] Execution completed successfully.", StringComparison.Ordinal)), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", original);
        }
    }

    [Test]
    public void ExecuteRuleAsync_UnsupportedRuleType_ThrowsNotSupportedException_AndLogsError()
    {
        var original = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", "Server=(local);Database=master;Trusted_Connection=True;");
            var engine = new BusinessRuleEngine(CreateConfiguration());

            var rule = new BusinessRule
            {
                Name = "Unsupported",
                RuleType = (RuleType)123,
                Code = ""
            };

            var logs = new List<string>();

            Assert.ThrowsAsync<NotSupportedException>(async () => await engine.ExecuteRuleAsync(rule, new BusinessRuleContext(), logs.Add));
            Assert.That(logs.Any(x => x.Contains("[ERR] Execution failed:", StringComparison.Ordinal)), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", original);
        }
    }

    [Test]
    public async Task ExecuteBundleAsync_OrdersBySequence_AndPipesPreviousResult()
    {
        var original = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", "Server=(local);Database=master;Trusted_Connection=True;");
            var engine = new BusinessRuleEngine(CreateConfiguration());

            var bundle = new BusinessRuleBundle
            {
                Name = "PipeBundle",
                Items =
                {
                    new BusinessRuleBundleItem { RuleId = 2, SequenceOrder = 2 },
                    new BusinessRuleBundleItem { RuleId = 1, SequenceOrder = 1 }
                }
            };

            var rules = new Dictionary<int, BusinessRule>
            {
                [1] = new() { Id = 1, Name = "Seed", RuleType = RuleType.CSharp, Code = "1" },
                [2] = new() { Id = 2, Name = "Increment", RuleType = RuleType.CSharp, Code = "(int)PreviousResult + 1" }
            };

            var context = new BusinessRuleContext();
            var result = await engine.ExecuteBundleAsync(bundle, context, id => Task.FromResult(rules.TryGetValue(id, out var rule) ? rule : null));

            Assert.That(result, Is.EqualTo(2));
            Assert.That(context.StepResults[1], Is.EqualTo(1));
            Assert.That(context.StepResults[2], Is.EqualTo(2));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", original);
        }
    }

    [Test]
    public async Task ExecuteBundleAsync_MissingRule_LogsAndContinues()
    {
        var original = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", "Server=(local);Database=master;Trusted_Connection=True;");
            var engine = new BusinessRuleEngine(CreateConfiguration());

            var bundle = new BusinessRuleBundle
            {
                Name = "MissingRuleBundle",
                Items =
                {
                    new BusinessRuleBundleItem { RuleId = 1, SequenceOrder = 1 },
                    new BusinessRuleBundleItem { RuleId = 999, SequenceOrder = 2 },
                    new BusinessRuleBundleItem { RuleId = 2, SequenceOrder = 3 }
                }
            };

            var rules = new Dictionary<int, BusinessRule>
            {
                [1] = new() { Id = 1, Name = "Seed", RuleType = RuleType.CSharp, Code = "1" },
                [2] = new() { Id = 2, Name = "Increment", RuleType = RuleType.CSharp, Code = "(int)PreviousResult + 1" }
            };

            var logs = new List<string>();
            var context = new BusinessRuleContext();
            var result = await engine.ExecuteBundleAsync(bundle, context, id => Task.FromResult(rules.TryGetValue(id, out var rule) ? rule : null), logs.Add);

            Assert.That(result, Is.EqualTo(2));
            Assert.That(logs.Any(x => x.Contains("Rule ID 999 not found. Skipping.", StringComparison.Ordinal)), Is.True);
            Assert.That(context.StepResults.Keys, Is.EquivalentTo(new[] { 1, 3 }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", original);
        }
    }
}
