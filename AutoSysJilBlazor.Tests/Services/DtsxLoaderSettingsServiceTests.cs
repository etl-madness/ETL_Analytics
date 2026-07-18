using AutoSysJilBlazor.Models;
using AutoSysJilBlazor.Services;
using AutoSysJilBlazor.Tests.TestDoubles;

namespace AutoSysJilBlazor.Tests.Services;

[TestFixture]
public class DtsxLoaderSettingsServiceTests
{
    private string _tempRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "dtsx-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Test]
    public async Task GetAsync_WhenSettingsFileDoesNotExist_ReturnsDefaultSettings()
    {
        var env = new TestWebHostEnvironment { ContentRootPath = _tempRoot };
        var service = new DtsxLoaderSettingsService(env);

        var settings = await service.GetAsync();

        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.IsVerbose, Is.False);
        Assert.That(settings.IsSql, Is.False);
        Assert.That(settings.IsLite, Is.False);
        Assert.That(settings.IsTruncate, Is.False);
    }

    [Test]
    public async Task SaveAsync_ThenGetAsync_RoundTripsSettings_AndUpdatesLastUpdatedUtc()
    {
        var env = new TestWebHostEnvironment { ContentRootPath = _tempRoot };
        var service = new DtsxLoaderSettingsService(env);

        var settings = new DtsxLoaderSettings
        {
            IsVerbose = true,
            Path = @"C:\repo\dtsx",
            IsSql = true,
            IsLite = true,
            IsTruncate = true,
            Extension = ".dtsx",
            OutputDirectory = @"C:\out",
            LastUpdatedUtc = DateTime.UtcNow.AddDays(-7)
        };

        await service.SaveAsync(settings);
        var reloaded = await service.GetAsync();

        Assert.That(reloaded.IsVerbose, Is.True);
        Assert.That(reloaded.Path, Is.EqualTo(@"C:\repo\dtsx"));
        Assert.That(reloaded.IsSql, Is.True);
        Assert.That(reloaded.IsLite, Is.True);
        Assert.That(reloaded.IsTruncate, Is.True);
        Assert.That(reloaded.Extension, Is.EqualTo(".dtsx"));
        Assert.That(reloaded.OutputDirectory, Is.EqualTo(@"C:\out"));
        Assert.That(reloaded.LastUpdatedUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
    }

    [Test]
    public void BuildArguments_IncludesOnlyConfiguredFlagsAndValues()
    {
        var settings = new DtsxLoaderSettings
        {
            IsVerbose = true,
            Path = @"C:\etl",
            IsSql = true,
            IsLite = false,
            IsTruncate = true,
            Extension = ".dtsx",
            OutputDirectory = @"C:\out"
        };

        var args = DtsxLoaderSettingsService.BuildArguments(settings);

        Assert.That(args, Is.EqualTo(new[]
        {
            "-v",
            "-p \"C:\\etl\"",
            "-s",
            "-t",
            "-x \".dtsx\"",
            "-o \"C:\\out\""
        }));
    }

    [Test]
    public void BuildCommandPreview_BuildsExpectedPreview()
    {
        var settings = new DtsxLoaderSettings
        {
            IsVerbose = true,
            Path = @"C:\etl",
            IsSql = true
        };

        var preview = DtsxLoaderSettingsService.BuildCommandPreview(settings);

        Assert.That(preview, Is.EqualTo("DTSXDataLoader.Console -v -p \"C:\\etl\" -s"));
    }
}
