using AutoSysJilBlazor.Models;

namespace AutoSysJilBlazor.Services;

public static class JilParser
{
    public static JilImportResult Parse(string content, string sourceName)
    {
        var jobs = new List<JilJob>();
        var blocks = SplitBlocks(content);

        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block))
            {
                continue;
            }

            var job = new JilJob();
            job.RawText = block.Trim();
            var lines = block
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (!line.Contains(':'))
                {
                    continue;
                }

                var parts = line.Split(new[] { ':' }, 2);
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (value.StartsWith('"') && !value.EndsWith('"'))
                {
                    value = value[1..];

                    while (i + 1 < lines.Count)
                    {
                        i++;
                        var continuationLine = lines[i];

                        if (continuationLine.EndsWith('"'))
                        {
                            value += "\r\n" + continuationLine[..^1];
                            break;
                        }

                        value += "\r\n" + continuationLine;
                    }
                }
                else
                {
                    if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
                    {
                        value = value[1..^1];
                    }
                }

                switch (key.ToLowerInvariant())
                {
                    case "insert_job":
                        job.JobName = value;
                        break;
                    case "job_type":
                        job.JobType = value;
                        break;
                    case "command":
                        job.Command = value;
                        break;
                    case "machine":
                        job.Machine = value;
                        break;
                    case "owner":
                        job.Owner = value;
                        break;
                    case "permission":
                        job.Permission = value;
                        break;
                    case "date_conditions":
                        job.DateConditions = value;
                        break;
                    case "days_of_week":
                        job.DaysOfWeek = value;
                        break;
                    case "start_mins":
                        job.StartMins = value;
                        break;
                    case "start_times":
                        job.StartTimes = value;
                        break;
                    case "timezone":
                        job.Timezone = value;
                        break;
                    case "description":
                        job.Description = value;
                        break;
                    case "std_out_file":
                        job.StdOutFile = value;
                        break;
                    case "std_err_file":
                        job.StdErrFile = value;
                        break;
                    case "alarm_if_fail":
                        job.AlarmIfFail = value;
                        break;
                    case "application":
                        job.Application = value;
                        break;
                    default:
                        job.AdditionalProperties[key] = value;
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(job.JobName))
            {
                jobs.Add(job);
            }
        }

        return new JilImportResult
        {
            Jobs = jobs,
            SqlTableDefinition = BuildSqlTableDefinition(),
            SourceName = sourceName
        };
    }

    private static List<string> SplitBlocks(string content)
    {
        var blocks = new List<string>();
        var current = new List<string>();

        foreach (var line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("insert_job:", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Count > 0)
                {
                    blocks.Add(string.Join(Environment.NewLine, current));
                }

                current = new List<string> { trimmed };
            }
            else if (current.Count > 0)
            {
                current.Add(trimmed);
            }
        }

        if (current.Count > 0)
        {
            blocks.Add(string.Join(Environment.NewLine, current));
        }

        return blocks;
    }

    private static string BuildSqlTableDefinition()
    {
        return """
CREATE TABLE dbo.AutoSysJilJobs (
    JobName NVARCHAR(255) NOT NULL,
    JobType NVARCHAR(100) NULL,
    Command NVARCHAR(MAX) NULL,
    Machine NVARCHAR(255) NULL,
    Owner NVARCHAR(255) NULL,
    Permission NVARCHAR(255) NULL,
    DateConditions NVARCHAR(50) NULL,
    DaysOfWeek NVARCHAR(100) NULL,
    StartMins NVARCHAR(50) NULL,
    StartTimes NVARCHAR(100) NULL,
    Timezone NVARCHAR(100) NULL,
    Description NVARCHAR(MAX) NULL,
    StdOutFile NVARCHAR(500) NULL,
    StdErrFile NVARCHAR(500) NULL,
    AlarmIfFail NVARCHAR(50) NULL,
    Application NVARCHAR(255) NULL,
    RawText NVARCHAR(MAX) NULL,
    ImportedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
""";
    }
}
