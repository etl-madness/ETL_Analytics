using EtlAnalytics.App.Models;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.Tests.Models;

[TestFixture]
public class ModelCoverageTests
{
    [Test]
    public void AllModelClasses_HaveParameterlessConstructor_AndCanBeInstantiated()
    {
        var blazorModels = typeof(DtsxLoaderSettings).Assembly
            .GetTypes()
            .Where(t => t.IsClass && t.Namespace == "EtlAnalytics.App.Models" && !t.IsAbstract);

        var libraryModels = typeof(JilJob).Assembly
            .GetTypes()
            .Where(t => t.IsClass && t.Namespace == "EtlAnalytics.RulesEngine.Models" && !t.IsAbstract);

        var modelTypes = blazorModels.Concat(libraryModels).ToList();

        Assert.That(modelTypes, Is.Not.Empty);

        foreach (var modelType in modelTypes)
        {
            Assert.That(modelType.GetConstructor(Type.EmptyTypes), Is.Not.Null, $"Expected {modelType.Name} to have a parameterless constructor.");
            Assert.DoesNotThrow(() => Activator.CreateInstance(modelType), $"Expected {modelType.Name} to be instantiable.");
        }
    }

    [Test]
    public void JilJob_AdditionalProperties_IsCaseInsensitive()
    {
        var job = new JilJob();
        job.AdditionalProperties["SomeKey"] = "value";

        Assert.That(job.AdditionalProperties["somekey"], Is.EqualTo("value"));
    }

    [Test]
    public void BusinessRule_Defaults_AreExpected()
    {
        var rule = new BusinessRule();

        Assert.That(rule.IsActive, Is.True);
        Assert.That(rule.Name, Is.EqualTo(string.Empty));
        Assert.That(rule.Description, Is.EqualTo(string.Empty));
        Assert.That(rule.Code, Is.EqualTo(string.Empty));
    }

    [Test]
    public void BusinessRuleBundle_Defaults_AreExpected()
    {
        var bundle = new BusinessRuleBundle();

        Assert.That(bundle.IsActive, Is.True);
        Assert.That(bundle.Items, Is.Not.Null);
        Assert.That(bundle.Items, Is.Empty);
    }

    [Test]
    public void BusinessRuleContext_Collections_AreInitialized()
    {
        var context = new BusinessRuleContext();

        Assert.That(context.Jobs, Is.Not.Null);
        Assert.That(context.JobToPackageMappings, Is.Not.Null);
        Assert.That(context.StepResults, Is.Not.Null);
    }

    [Test]
    public void GanttRow_IsOverlapping_MapsOverlapFlag()
    {
        var overlapping = new GanttRow { OverlapFlag = 1 };
        var nonOverlapping = new GanttRow { OverlapFlag = 0 };

        Assert.That(overlapping.IsOverlapping, Is.True);
        Assert.That(nonOverlapping.IsOverlapping, Is.False);
    }

    [Test]
    public void OverlapDetail_OverlapDurationMinutes_IsCalculated()
    {
        var detail = new OverlapDetail
        {
            OverlapStart = new TimeSpan(10, 0, 0),
            OverlapEnd = new TimeSpan(10, 45, 0)
        };

        Assert.That(detail.OverlapDurationMinutes, Is.EqualTo(45));
    }

    [Test]
    public void RuleType_ContainsExpectedValues()
    {
        var values = Enum.GetValues<RuleType>();

        Assert.That(values, Is.EquivalentTo(new[] { RuleType.TSQL, RuleType.CSharp }));
    }

    [Test]
    public void JilImportResult_Defaults_AreExpected()
    {
        var result = new JilImportResult();

        Assert.That(result.Jobs, Is.Not.Null);
        Assert.That(result.Jobs, Is.Empty);
        Assert.That(result.SourceName, Is.EqualTo(string.Empty));
        Assert.That(result.SqlTableDefinition, Is.EqualTo(string.Empty));
    }

    [Test]
    public void DtsxLoaderSettings_Defaults_ProvideRecentTimestamp()
    {
        var settings = new DtsxLoaderSettings();

        Assert.That(settings.LastUpdatedUtc, Is.LessThanOrEqualTo(DateTime.UtcNow));
        Assert.That(settings.LastUpdatedUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
    }
}
