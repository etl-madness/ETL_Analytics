using EtlAnalytics.RulesEngine.Models;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.App.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EtlAnalytics.App.Services;

public class SqlDatabaseService : IBusinessRuleStore
{
    static SqlDatabaseService()
    {
        // Map snake_case SQL columns (e.g. database_server) to C# PascalCase properties.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private readonly string _connectionString;
    private readonly IEncryptionService _encryptionService;

    public SqlDatabaseService(IConfiguration configuration, IEncryptionService encryptionService)
    {
        _connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Environment variable 'DB_CONNECTION_STRING' is not set.");
        _encryptionService = encryptionService;
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

    public async Task<int> InsertJobsAsync(
        IEnumerable<JilJob> jobs,
        string tableName = "dbo.AutoSysJilJobs",
        DateTime? importedAtUtc = null)
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

        var importedAt = importedAtUtc ?? DateTime.UtcNow;
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

    // ─── Conflicting Jobs ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ConflictingJob>> GetCurrentConflictingJobsAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<ConflictingJob>(
            "dbo.GetCurrentConflictingJobs",
            commandType: System.Data.CommandType.StoredProcedure);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DtsxSqlViewRow>> GetDtsxSqlViewRowsAsync()
    {
        const string sql = @"
            SELECT
                [Id],
                [Description],
                [Package],
                [RefId],
                [Pipeline],
                [SQL Source] AS SqlSource,
                [Resolved SQL] AS ResolvedSql,
                [ConnectionString],
                [ConnectionName],
                [ConnectionDtsId],
                [ConnectionType],
                [ConnectionRefId],
                [Name],
                [ComponentType],
                [LoadDate]
            FROM [dbo].[DTSX_SQL_View]
            ORDER BY [LoadDate] DESC;";

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<DtsxSqlViewRow>(sql);
        return rows.ToList();
    }

    // ─── Business Rules ──────────────────────────────────────────────────────

    public async Task CreateBusinessRuleTablesIfNotExistsAsync()
    {
        const string sql = @"
            IF OBJECT_ID('dbo.DbConnections', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DbConnections (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(255) NOT NULL,
                    ConnectionString NVARCHAR(MAX) NOT NULL,
                    ProviderType NVARCHAR(100) NOT NULL DEFAULT 'SqlServer',
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;

            IF OBJECT_ID('dbo.BusinessRules', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.BusinessRules (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(255) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    RuleType NVARCHAR(50) NOT NULL,
                    Code NVARCHAR(MAX) NOT NULL,
                    Version INT NOT NULL DEFAULT 1,
                    ConnectionId INT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_BusinessRules_Connection FOREIGN KEY (ConnectionId) REFERENCES dbo.DbConnections(Id)
                );
            END
            ELSE
            BEGIN
                IF COL_LENGTH('dbo.BusinessRules', 'ConnectionId') IS NULL
                BEGIN
                    ALTER TABLE dbo.BusinessRules ADD ConnectionId INT NULL;
                    ALTER TABLE dbo.BusinessRules ADD CONSTRAINT FK_BusinessRules_Connection FOREIGN KEY (ConnectionId) REFERENCES dbo.DbConnections(Id);
                END
            END;

            IF OBJECT_ID('dbo.BusinessRuleHistory', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.BusinessRuleHistory (
                    HistoryId INT IDENTITY(1,1) PRIMARY KEY,
                    RuleId INT NOT NULL,
                    Name NVARCHAR(255) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    RuleType NVARCHAR(50) NOT NULL,
                    Code NVARCHAR(MAX) NOT NULL,
                    Version INT NOT NULL,
                    ArchivedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_BusinessRuleHistory_RuleId FOREIGN KEY (RuleId) REFERENCES dbo.BusinessRules(Id) ON DELETE CASCADE
                );
            END;

            IF OBJECT_ID('dbo.BusinessRuleBundles', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.BusinessRuleBundles (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(255) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;

            IF OBJECT_ID('dbo.BusinessRuleBundleItems', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.BusinessRuleBundleItems (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    BundleId INT NOT NULL,
                    RuleId INT NOT NULL,
                    SequenceOrder INT NOT NULL,
                    CONSTRAINT FK_BundleItems_Bundle FOREIGN KEY (BundleId) REFERENCES dbo.BusinessRuleBundles(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_BundleItems_Rule FOREIGN KEY (RuleId) REFERENCES dbo.BusinessRules(Id)
                );
            END;
        ";
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);

        // Seed example rules if table is empty
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.BusinessRules;");
        if (count == 0)
        {
            const string seedSql = @"
                INSERT INTO dbo.BusinessRules (Name, Description, RuleType, Code, Version, IsActive, CreatedAt, UpdatedAt)
                VALUES 
                ('Unmapped Jobs Audit', 'Finds all AutoSys jobs that do not have a corresponding SSIS package mapping.', 'TSQL', 
                 'SELECT j.JobName, j.Application, j.Machine \nFROM dbo.AutoSysJilJobs j \nLEFT JOIN dbo.AutoSysJobToPackage m ON j.JobName = m.JobName \nWHERE m.PackageName IS NULL \nORDER BY j.JobName', 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
                
                ('Naming Convention Validator', 'A C# script to demonstrate using the application context (Jobs) to validate data.', 'CSharp', 
                 '// Access the application context directly\nLog($""Processing {Jobs.Count} jobs from the latest import..."");\n\nvar violations = Jobs.Where(j => !j.JobName.StartsWith(""JOB_"")).ToList();\n\nreturn new { \n    TotalJobs = Jobs.Count, \n    InvalidCount = violations.Count, \n    ViolationList = violations.Select(v => v.JobName).Take(10),\n    Status = violations.Any() ? ""Warning"" : ""Passed""\n};', 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            ";
            await connection.ExecuteAsync(seedSql);
        }
    }

    public async Task<IReadOnlyList<BusinessRule>> GetBusinessRulesAsync()
    {
        const string sql = "SELECT * FROM dbo.BusinessRules WHERE IsActive = 1 ORDER BY Name;";
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<BusinessRule>(sql);
        return rows.ToList();
    }

    public async Task<BusinessRule?> GetBusinessRuleByIdAsync(int id)
    {
        const string sql = "SELECT * FROM dbo.BusinessRules WHERE Id = @Id;";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<BusinessRule>(sql, new { Id = id });
    }

    public async Task<int> InsertBusinessRuleAsync(BusinessRule rule)
    {
        const string sql = @"
            INSERT INTO dbo.BusinessRules (Name, Description, RuleType, Code, Version, ConnectionId, IsActive, CreatedAt, UpdatedAt)
            VALUES (@Name, @Description, @RuleType, @Code, 1, @ConnectionId, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, rule);
    }

    public async Task UpdateBusinessRuleAsync(BusinessRule rule)
    {
        const string archiveSql = @"
            INSERT INTO dbo.BusinessRuleHistory (RuleId, Name, Description, RuleType, Code, Version, ArchivedAt)
            SELECT Id, Name, Description, RuleType, Code, Version, SYSUTCDATETIME()
            FROM dbo.BusinessRules
            WHERE Id = @Id;
        ";

        const string updateSql = @"
            UPDATE dbo.BusinessRules
            SET Name = @Name,
                Description = @Description,
                RuleType = @RuleType,
                Code = @Code,
                ConnectionId = @ConnectionId,
                Version = Version + 1,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id;
        ";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(archiveSql, new { Id = rule.Id }, transaction);
            await connection.ExecuteAsync(updateSql, rule, transaction);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteBusinessRuleAsync(int id)
    {
        const string sql = "UPDATE dbo.BusinessRules SET IsActive = 0 WHERE Id = @Id;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<IReadOnlyList<BusinessRuleHistory>> GetBusinessRuleHistoryAsync(int ruleId)
    {
        const string sql = "SELECT * FROM dbo.BusinessRuleHistory WHERE RuleId = @RuleId ORDER BY Version DESC;";
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<BusinessRuleHistory>(sql, new { RuleId = ruleId });
        return rows.ToList();
    }

    // ─── Bundles ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BusinessRuleBundle>> GetBusinessRuleBundlesAsync()
    {
        const string sql = "SELECT * FROM dbo.BusinessRuleBundles WHERE IsActive = 1 ORDER BY Name;";
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<BusinessRuleBundle>(sql);
        return rows.ToList();
    }

    public async Task<BusinessRuleBundle?> GetBusinessRuleBundleByIdAsync(int id)
    {
        const string sql = "SELECT * FROM dbo.BusinessRuleBundles WHERE Id = @Id;";
        const string itemsSql = @"
            SELECT i.*, r.Name as RuleName, r.RuleType 
            FROM dbo.BusinessRuleBundleItems i
            JOIN dbo.BusinessRules r ON i.RuleId = r.Id
            WHERE i.BundleId = @BundleId
            ORDER BY i.SequenceOrder;
        ";
        await using var connection = new SqlConnection(_connectionString);
        var bundle = await connection.QueryFirstOrDefaultAsync<BusinessRuleBundle>(sql, new { Id = id });
        if (bundle != null)
        {
            var items = await connection.QueryAsync<BusinessRuleBundleItem>(itemsSql, new { BundleId = id });
            bundle.Items = items.ToList();
        }
        return bundle;
    }

    public async Task<int> InsertBusinessRuleBundleAsync(BusinessRuleBundle bundle)
    {
        const string sql = @"
            INSERT INTO dbo.BusinessRuleBundles (Name, Description, IsActive, CreatedAt, UpdatedAt)
            VALUES (@Name, @Description, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            var id = await connection.ExecuteScalarAsync<int>(sql, bundle, transaction);
            foreach (var item in bundle.Items)
            {
                item.BundleId = id;
                await connection.ExecuteAsync(@"
                    INSERT INTO dbo.BusinessRuleBundleItems (BundleId, RuleId, SequenceOrder)
                    VALUES (@BundleId, @RuleId, @SequenceOrder);
                ", item, transaction);
            }
            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateBusinessRuleBundleAsync(BusinessRuleBundle bundle)
    {
        const string sql = @"
            UPDATE dbo.BusinessRuleBundles
            SET Name = @Name, Description = @Description, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id;
        ";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(sql, bundle, transaction);
            await connection.ExecuteAsync("DELETE FROM dbo.BusinessRuleBundleItems WHERE BundleId = @Id;", new { Id = bundle.Id }, transaction);
            foreach (var item in bundle.Items)
            {
                item.BundleId = bundle.Id;
                await connection.ExecuteAsync(@"
                    INSERT INTO dbo.BusinessRuleBundleItems (BundleId, RuleId, SequenceOrder)
                    VALUES (@BundleId, @RuleId, @SequenceOrder);
                ", item, transaction);
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteBusinessRuleBundleAsync(int id)
    {
        const string sql = "UPDATE dbo.BusinessRuleBundles SET IsActive = 0 WHERE Id = @Id;";
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name)
    {
        const string sql = "SELECT * FROM dbo.BusinessRuleBundles WHERE Name = @Name AND IsActive = 1;";
        const string itemsSql = @"
            SELECT i.*, r.Name as RuleName, r.RuleType 
            FROM dbo.BusinessRuleBundleItems i
            JOIN dbo.BusinessRules r ON i.RuleId = r.Id
            WHERE i.BundleId = @BundleId
            ORDER BY i.SequenceOrder;
        ";
        await using var connection = new SqlConnection(_connectionString);
        var bundle = await connection.QueryFirstOrDefaultAsync<BusinessRuleBundle>(sql, new { Name = name });
        if (bundle != null)
        {
            var items = await connection.QueryAsync<BusinessRuleBundleItem>(itemsSql, new { BundleId = bundle.Id });
            bundle.Items = items.ToList();
        }
        return bundle;
    }

    public async Task<DbConnectionDefinition?> GetDbConnectionByIdAsync(int id)
    {
        const string sql = "SELECT * FROM dbo.DbConnections WHERE Id = @Id;";
        await using var connection = new SqlConnection(_connectionString);
        var dbConn = await connection.QueryFirstOrDefaultAsync<DbConnectionDefinition>(sql, new { Id = id });
        if (dbConn != null)
        {
            dbConn.ConnectionString = _encryptionService.Decrypt(dbConn.ConnectionString);
        }
        return dbConn;
    }

    public async Task<IEnumerable<DbConnectionDefinition>> GetAllDbConnectionsAsync()
    {
        const string sql = "SELECT * FROM dbo.DbConnections ORDER BY Name;";
        await using var connection = new SqlConnection(_connectionString);
        var conns = (await connection.QueryAsync<DbConnectionDefinition>(sql)).ToList();
        foreach (var conn in conns)
        {
            conn.ConnectionString = _encryptionService.Decrypt(conn.ConnectionString);
        }
        return conns;
    }

    public async Task<int> InsertDbConnectionAsync(DbConnectionDefinition conn)
    {
        var encryptedConn = new DbConnectionDefinition
        {
            Name = conn.Name,
            ProviderType = conn.ProviderType,
            ConnectionString = _encryptionService.Encrypt(conn.ConnectionString)
        };

        const string sql = @"
            INSERT INTO dbo.DbConnections (Name, ConnectionString, ProviderType, CreatedAt)
            VALUES (@Name, @ConnectionString, @ProviderType, SYSUTCDATETIME());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, encryptedConn);
    }

    public async Task UpdateDbConnectionAsync(DbConnectionDefinition conn)
    {
        var encryptedConn = new DbConnectionDefinition
        {
            Id = conn.Id,
            Name = conn.Name,
            ProviderType = conn.ProviderType,
            ConnectionString = _encryptionService.Encrypt(conn.ConnectionString)
        };

        const string sql = @"
            UPDATE dbo.DbConnections
            SET Name = @Name,
                ConnectionString = @ConnectionString,
                ProviderType = @ProviderType
            WHERE Id = @Id;
        ";
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, encryptedConn);
    }

    public async Task DeleteDbConnectionAsync(int id)
    {
        // First check if any rules are using this connection
        const string checkSql = "SELECT COUNT(*) FROM dbo.BusinessRules WHERE ConnectionId = @Id AND IsActive = 1;";
        await using var connection = new SqlConnection(_connectionString);
        var count = await connection.ExecuteScalarAsync<int>(checkSql, new { Id = id });
        if (count > 0)
        {
            throw new InvalidOperationException($"Cannot delete connection. It is being used by {count} business rules.");
        }

        const string sql = "DELETE FROM dbo.DbConnections WHERE Id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id });
    }
}


