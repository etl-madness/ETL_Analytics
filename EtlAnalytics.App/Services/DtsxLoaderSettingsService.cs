using System.Text.Json;
using EtlAnalytics.App.Models;

namespace EtlAnalytics.App.Services;

public class DtsxLoaderSettingsService
{
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DtsxLoaderSettingsService(IWebHostEnvironment environment)
    {
        var settingsDirectory = System.IO.Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(settingsDirectory);
        _settingsFilePath = System.IO.Path.Combine(settingsDirectory, "dtsx-loader-settings.json");
    }

    public async Task<DtsxLoaderSettings> GetAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new DtsxLoaderSettings();
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<DtsxLoaderSettings>(stream, JsonOptions);
        return settings ?? new DtsxLoaderSettings();
    }

    public async Task SaveAsync(DtsxLoaderSettings settings)
    {
        settings.LastUpdatedUtc = DateTime.UtcNow;
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    public static string BuildCommandPreview(DtsxLoaderSettings settings)
    {
        var args = BuildArguments(settings);

        return $"DTSXDataLoader.Console {string.Join(" ", args)}".Trim();
    }

    public static IReadOnlyList<string> BuildArguments(DtsxLoaderSettings settings)
    {
        var args = new List<string>();

        if (settings.IsVerbose) args.Add("-v");
        if (!string.IsNullOrWhiteSpace(settings.Path)) args.Add($"-p \"{settings.Path}\"");
        if (settings.IsSql) args.Add("-s");
        if (settings.IsLite) args.Add("-l");
        if (settings.IsTruncate) args.Add("-t");
        if (!string.IsNullOrWhiteSpace(settings.Extension)) args.Add($"-x \"{settings.Extension}\"");
        if (!string.IsNullOrWhiteSpace(settings.OutputDirectory)) args.Add($"-o \"{settings.OutputDirectory}\"");

        return args;
    }
}
