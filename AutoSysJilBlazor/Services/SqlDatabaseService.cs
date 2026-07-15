using AutoSysJilBlazor.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AutoSysJilBlazor.Services;

public class SqlDatabaseService
{
    static SqlDatabaseService()
    {
        // Map snake_case SQL columns (e.g. database_server) to C# PascalCase properties.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private readonly string _connectionString;

    public SqlDatabaseService(IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Environment variable 'DB_CONNECTION_STRING' is not set.");
    }

    public async Task<int> CreateTableIfNotExistsAsync(string tableName = "dbo.AutoSysJilJobs")
    {
        const string sql = @"
            IF OBJECT_ID(@TableName, 'U') IS NULL
            BEGIN
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
            END;
        ";

        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { TableName = tableName });
    }

    public async Task<int> InsertJobsAsync(IEnumerable<JilJob> jobs, string tableName = "dbo.AutoSysJilJobs")
    {
        const string sql = @"
            INSERT INTO dbo.AutoSysJilJobs (
                JobName, JobType, Command, Machine, Owner, Permission,
                DateConditions, DaysOfWeek, StartMins, StartTimes, Timezone,
                Description, StdOutFile, StdErrFile, AlarmIfFail, Application, RawText, ImportedAt
            )
            VALUES (
                @JobName, @JobType, @Command, @Machine, @Owner, @Permission,
                @DateConditions, @DaysOfWeek, @StartMins, @StartTimes, @Timezone,
                @Description, @StdOutFile, @StdErrFile, @AlarmIfFail, @Application, @RawText, @ImportedAt
            );
        ";

        var importedAt = DateTime.UtcNow;
        var rows = jobs.Select(j => new
        {
            j.JobName,
            j.JobType,
            j.Command,
            j.Machine,
            j.Owner,
            j.Permission,
            j.DateConditions,
            j.DaysOfWeek,
            j.StartMins,
            j.StartTimes,
            j.Timezone,
            j.Description,
            j.StdOutFile,
            j.StdErrFile,
            j.AlarmIfFail,
            j.Application,
            j.RawText,
            ImportedAt = importedAt
        }).ToList();

        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, rows);
    }

    public async Task<IReadOnlyList<DateTime>> GetImportTimesAsync(string tableName = "dbo.AutoSysJilJobs")
    {
        const string sql = @"
            SELECT DISTINCT CAST(ImportedAt AS datetime2(0)) AS ImportedAt
            FROM dbo.AutoSysJilJobs
            ORDER BY ImportedAt DESC;
        ";

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<DateTime>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<JilJob>> QueryJobsByImportTimeAsync(DateTime importedAt, string tableName = "dbo.AutoSysJilJobs")
    {
        const string sql = @"
            SELECT
                JobName, JobType, Command, Machine, Owner, Permission,
                DateConditions, DaysOfWeek, StartMins, StartTimes, Timezone,
                Description, StdOutFile, StdErrFile, AlarmIfFail, Application, RawText
            FROM dbo.AutoSysJilJobs
            WHERE CAST(ImportedAt AS datetime2(0)) = CAST(@ImportedAt AS datetime2(0))
            ORDER BY JobName;
        ";

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<JilJob>(sql, new { ImportedAt = importedAt });
        return rows.ToList();
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetRuntimeDurationsAsync(DateTime? day = null)
    {
        const string sql = @"
            SELECT JobName, JobStatus, AVG(RunTimeInMinutes) as RunTimeInMinutes
            FROM dbo.AutoSysRuntimeJobs
            WHERE (@Day IS NULL OR CAST(InsertedAt AS date) = CAST(@Day AS date)) and JobStatus='ENABLED'
            GROUP BY JobName, JobStatus, RunTimeInMinutes;";

        await using var connection = new SqlConnection(_connectionString);
        var rows = (await connection.QueryAsync<AutoSysRuntimeJob>(sql, new { Day = day?.Date })).ToList();
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.JobName) && r.RunTimeInMinutes.HasValue)
            .GroupBy(r => r.JobName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Max(x => x.RunTimeInMinutes!.Value), StringComparer.OrdinalIgnoreCase);
    }

    public async Task CreateJobToPackageTableIfNotExistsAsync()
    {
        const string sql = @"
            IF OBJECT_ID('dbo.AutoSysJobToPackage', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AutoSysJobToPackage (
                    JobName VARCHAR(255) NOT NULL,
                    PackageName VARCHAR(255) NOT NULL,
                    ImportedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;
        ";
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);
    }

    public async Task<IReadOnlyList<AutoSysJobToPackage>> GetJobToPackagesAsync()
    {
        const string sql = @"
            SELECT JobName, PackageName, ImportedAt
            FROM dbo.AutoSysJobToPackage
            ORDER BY ImportedAt DESC, JobName;
        ";
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<AutoSysJobToPackage>(sql);
        return rows.ToList();
    }

    public async Task<int> InsertJobToPackageAsync(AutoSysJobToPackage entry)
    {
        const string sql = @"
            INSERT INTO dbo.AutoSysJobToPackage (JobName, PackageName, ImportedAt)
            VALUES (@JobName, @PackageName, @ImportedAt);
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, entry);
    }

    public async Task<int> InsertJobToPackagesAsync(IEnumerable<AutoSysJobToPackage> entries)
    {
        const string sql = @"
            INSERT INTO dbo.AutoSysJobToPackage (JobName, PackageName, ImportedAt)
            VALUES (@JobName, @PackageName, @ImportedAt);
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, entries);
    }

    public async Task<int> UpdateJobToPackageAsync(string oldJobName, string oldPackageName, AutoSysJobToPackage entry)
    {
        const string sql = @"
            UPDATE dbo.AutoSysJobToPackage
            SET JobName = @JobName, PackageName = @PackageName, ImportedAt = @ImportedAt
            WHERE JobName = @OldJobName AND PackageName = @OldPackageName;
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new
        {
            JobName = entry.JobName,
            PackageName = entry.PackageName,
            ImportedAt = entry.ImportedAt,
            OldJobName = oldJobName,
            OldPackageName = oldPackageName
        });
    }

    public async Task<int> DeleteJobToPackageAsync(AutoSysJobToPackage entry)
    {
        const string sql = @"
            DELETE FROM dbo.AutoSysJobToPackage
            WHERE JobName = @JobName AND PackageName = @PackageName;
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { JobName = entry.JobName, PackageName = entry.PackageName });
    }

    public async Task<int> TruncateJobToPackageAsync()
    {
        const string sql = @"
            TRUNCATE TABLE dbo.AutoSysJobToPackage;
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql);
    }

    // ─── AutoSysPackageToObject ──────────────────────────────────────────────

    public async Task CreatePackageToObjectTableIfNotExistsAsync()
    {
        const string sql = @"
            IF OBJECT_ID('dbo.AutoSysPackageToObject', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AutoSysPackageToObject (
                    PackageName    VARCHAR(MAX) NULL,
                    database_server VARCHAR(MAX) NULL,
                    [database]     VARCHAR(MAX) NULL,
                    [schema]       VARCHAR(MAX) NULL,
                    object_name    VARCHAR(MAX) NULL,
                    object_type    VARCHAR(MAX) NULL,
                    ImportedAt     DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;
        ";
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);
    }

    public async Task<IReadOnlyList<AutoSysPackageToObject>> GetPackageToObjectsAsync()
    {
        const string sql = @"
            SELECT
                PackageName,
                database_server AS DatabaseServer,
                [database]      AS [Database],
                [schema]        AS [Schema],
                object_name     AS ObjectName,
                object_type     AS ObjectType,
                ImportedAt
            FROM dbo.AutoSysPackageToObject
            ORDER BY ImportedAt DESC, PackageName;
        ";
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<AutoSysPackageToObject>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DateTime>> GetPackageToObjectImportTimesAsync()
    {
        const string sql = @"
            SELECT DISTINCT CAST(ImportedAt AS DATETIME2(0)) AS ImportedAt
            FROM [TRAIN].[dbo].[AutoSysPackageToObject]
            ORDER BY ImportedAt DESC;
        ";

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<DateTime>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DataConflictRow>> GetNumberOfPackagesTablesAreInAsync(DateTime importedAt)
    {
        var p = new DynamicParameters();
        p.Add("@ImportedAt", importedAt);

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<DataConflictRow>(
            "dbo.GetNumberOfPackagesTablesAreIn",
            p,
            commandType: System.Data.CommandType.StoredProcedure);

        return rows.ToList();
    }

    public async Task<int> InsertPackageToObjectAsync(AutoSysPackageToObject entry)
    {
        const string sql = @"
            INSERT INTO dbo.AutoSysPackageToObject
                (PackageName, database_server, [database], [schema], object_name, object_type, ImportedAt)
            VALUES
                (@PackageName, @DatabaseServer, @Database, @Schema, @ObjectName, @ObjectType, @ImportedAt);
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, entry);
    }

    public async Task<int> InsertPackageToObjectsAsync(IEnumerable<AutoSysPackageToObject> entries)
    {
        const string sql = @"
            INSERT INTO dbo.AutoSysPackageToObject
                (PackageName, database_server, [database], [schema], object_name, object_type, ImportedAt)
            VALUES
                (@PackageName, @DatabaseServer, @Database, @Schema, @ObjectName, @ObjectType, @ImportedAt);
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, entries);
    }

    public async Task<int> UpdatePackageToObjectAsync(AutoSysPackageToObject original, AutoSysPackageToObject entry)
    {
        const string sql = @"
            UPDATE dbo.AutoSysPackageToObject
            SET
                PackageName     = @PackageName,
                database_server = @DatabaseServer,
                [database]      = @Database,
                [schema]        = @Schema,
                object_name     = @ObjectName,
                object_type     = @ObjectType,
                ImportedAt      = @ImportedAt
            WHERE
                (PackageName     = @OldPackageName     OR (PackageName IS NULL     AND @OldPackageName IS NULL))
                AND (database_server = @OldDatabaseServer OR (database_server IS NULL AND @OldDatabaseServer IS NULL))
                AND ([database]      = @OldDatabase      OR ([database] IS NULL      AND @OldDatabase IS NULL))
                AND ([schema]        = @OldSchema        OR ([schema] IS NULL        AND @OldSchema IS NULL))
                AND (object_name     = @OldObjectName    OR (object_name IS NULL     AND @OldObjectName IS NULL))
                AND (object_type     = @OldObjectType    OR (object_type IS NULL     AND @OldObjectType IS NULL));
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new
        {
            PackageName = entry.PackageName,
            DatabaseServer = entry.DatabaseServer,
            Database = entry.Database,
            Schema = entry.Schema,
            ObjectName = entry.ObjectName,
            ObjectType = entry.ObjectType,
            ImportedAt = entry.ImportedAt,
            OldPackageName = original.PackageName,
            OldDatabaseServer = original.DatabaseServer,
            OldDatabase = original.Database,
            OldSchema = original.Schema,
            OldObjectName = original.ObjectName,
            OldObjectType = original.ObjectType
        });
    }

    public async Task<int> DeletePackageToObjectAsync(AutoSysPackageToObject entry)
    {
        const string sql = @"
            DELETE FROM dbo.AutoSysPackageToObject
            WHERE
                (PackageName     = @PackageName     OR (PackageName IS NULL     AND @PackageName IS NULL))
                AND (database_server = @DatabaseServer OR (database_server IS NULL AND @DatabaseServer IS NULL))
                AND ([database]      = @Database      OR ([database] IS NULL      AND @Database IS NULL))
                AND ([schema]        = @Schema        OR ([schema] IS NULL        AND @Schema IS NULL))
                AND (object_name     = @ObjectName    OR (object_name IS NULL     AND @ObjectName IS NULL))
                AND (object_type     = @ObjectType    OR (object_type IS NULL     AND @ObjectType IS NULL));
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new
        {
            PackageName = entry.PackageName,
            DatabaseServer = entry.DatabaseServer,
            Database = entry.Database,
            Schema = entry.Schema,
            ObjectName = entry.ObjectName,
            ObjectType = entry.ObjectType
        });
    }

    public async Task<int> TruncatePackageToObjectAsync()
    {
        const string sql = @"TRUNCATE TABLE dbo.AutoSysPackageToObject;";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql);
    }

    // ─── AutoSysRuntimeJobs ──────────────────────────────────────────────────

    public async Task CreateRuntimeJobsTableIfNotExistsAsync()
    {
        const string sql = @"
            IF OBJECT_ID('dbo.AutoSysRuntimeJobs', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AutoSysRuntimeJobs (
                    JobRunId          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    JobName           VARCHAR(255) NOT NULL,
                    JobStatus         VARCHAR(100) NULL,
                    RunTimeInMinutes  DECIMAL(8, 2) NULL,
                    InsertedAt        DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;
        ";
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);
    }

    public async Task<IReadOnlyList<AutoSysRuntimeJob>> GetRuntimeJobsAsync()
    {
        const string sql = @"
            SELECT
                JobRunId,
                JobName,
                JobStatus,
                RunTimeInMinutes,
                InsertedAt
            FROM dbo.AutoSysRuntimeJobs
            ORDER BY InsertedAt DESC, JobName;
        ";
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<AutoSysRuntimeJob>(sql);
        return rows.ToList();
    }

    public async Task<int> InsertRuntimeJobAsync(AutoSysRuntimeJob entry)
    {
        const string sql = @"
            INSERT INTO dbo.AutoSysRuntimeJobs
                (JobName, JobStatus, RunTimeInMinutes, InsertedAt)
            VALUES
                (@JobName, @JobStatus, @RunTimeInMinutes, @InsertedAt);
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, entry);
    }

    public async Task<int> InsertRuntimeJobsAsync(IEnumerable<AutoSysRuntimeJob> entries)
    {
        const string sql = @"
            INSERT INTO dbo.AutoSysRuntimeJobs
                (JobName, JobStatus, RunTimeInMinutes, InsertedAt)
            VALUES
                (@JobName, @JobStatus, @RunTimeInMinutes, @InsertedAt);
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, entries);
    }

    public async Task<int> UpdateRuntimeJobAsync(AutoSysRuntimeJob entry)
    {
        const string sql = @"
            UPDATE dbo.AutoSysRuntimeJobs
            SET
                JobName          = @JobName,
                JobStatus        = @JobStatus,
                RunTimeInMinutes = @RunTimeInMinutes,
                InsertedAt       = @InsertedAt
            WHERE
                JobRunId         = @JobRunId;
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, entry);
    }

    public async Task<int> DeleteRuntimeJobAsync(AutoSysRuntimeJob entry)
    {
        const string sql = @"
            DELETE FROM dbo.AutoSysRuntimeJobs
            WHERE JobRunId = @JobRunId;
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { JobRunId = entry.JobRunId });
    }

    public async Task<int> TruncateRuntimeJobsAsync()
    {
        const string sql = @"TRUNCATE TABLE dbo.AutoSysRuntimeJobs;";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql);
    }

    // ─── Overlap Gantt ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<GanttRow>> GetGanttDataAsync(DateTime weekStartDate, int defaultRunTimeMinutes = 15)
    {
        var p = new DynamicParameters();
        p.Add("@WeekStartDate", weekStartDate.Date);
        p.Add("@DefaultRunTimeMinutes", defaultRunTimeMinutes);

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<GanttRow>(
            "dbo.Overlap_Jobs_Gantt",
            p,
            commandType: System.Data.CommandType.StoredProcedure);
        return rows.ToList();
    }
}


