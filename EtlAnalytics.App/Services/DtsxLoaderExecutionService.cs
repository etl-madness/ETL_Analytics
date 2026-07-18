using System.Diagnostics;
using EtlAnalytics.App.Models;

namespace EtlAnalytics.App.Services;

public class DtsxLoaderExecutionService
{
    private readonly IWebHostEnvironment _environment;

    public DtsxLoaderExecutionService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<int> RunAsync(
        DtsxLoaderSettings settings,
        Action<string> appendLog,
        CancellationToken cancellationToken)
    {
        var repoRoot = Directory.GetParent(_environment.ContentRootPath)?.FullName;
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new InvalidOperationException("Unable to resolve repository root path.");
        }

        var projectPath = Path.Combine(repoRoot, "DTSXDataLoader", "DTSXDataLoader.Console", "DTSXDataLoader.Console.csproj");
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("DTSXDataLoader.Console project not found.", projectPath);
        }

        var args = DtsxLoaderSettingsService.BuildArguments(settings);
        var argsText = string.Join(" ", args);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -- {argsText}".Trim(),
            WorkingDirectory = Path.GetDirectoryName(projectPath) ?? repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                appendLog($"[OUT] {e.Data}");
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                appendLog($"[ERR] {e.Data}");
            }
        };

        appendLog($"> dotnet {startInfo.Arguments}");

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start DTSXDataLoader process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore cancellation kill failures.
            }
        });

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
