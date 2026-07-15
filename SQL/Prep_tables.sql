DROP TABLE IF EXISTS [dbo].[AutoSysJobToPackage];
GO

CREATE TABLE [dbo].[AutoSysJobToPackage](
	[JobName] [varchar](255) NOT NULL,
	[PackageName] [varchar](255) NOT NULL,
	[ImportedAt] [datetime2](7) NOT NULL
);
GO

ALTER TABLE [dbo].[AutoSysJobToPackage] ADD  DEFAULT (sysutcdatetime()) FOR [ImportedAt]
GO


DROP TABLE IF EXISTS [dbo].[AutoSysJilJobs];
GO

CREATE TABLE [dbo].[AutoSysJilJobs](
	[JobName] [varchar](255) NOT NULL,
	[JobType] [varchar](100) NULL,
	[Command] [varchar](max) NULL,
	[Machine] [varchar](255) NULL,
	[Owner] [varchar](255) NULL,
	[Permission] [varchar](255) NULL,
	[DateConditions] [varchar](50) NULL,
	[DaysOfWeek] [varchar](100) NULL,
	[StartMins] [varchar](50) NULL,
	[StartTimes] [varchar](100) NULL,
	[Timezone] [varchar](100) NULL,
	[Description] [varchar](max) NULL,
	[StdOutFile] [varchar](500) NULL,
	[StdErrFile] [varchar](500) NULL,
	[AlarmIfFail] [varchar](50) NULL,
	[Application] [varchar](255) NULL,
	[RawText] [varchar](max) NULL,
	[ImportedAt] [datetime2](7) NOT NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[AutoSysJilJobs] ADD  DEFAULT (sysutcdatetime()) FOR [ImportedAt]
GO

DROP TABLE IF EXISTS [dbo].[AutoSysRuntimeJobs];
GO

CREATE TABLE [dbo].[AutoSysRuntimeJobs](
	[JobRunId] [int] IDENTITY(1,1) NOT NULL,
	[JobName] [varchar](255) NOT NULL,
	[JobStatus] [varchar](100) NULL,
	[RunTimeInMinutes] [decimal](8, 2) NULL,
	[InsertedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[JobRunId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[AutoSysRuntimeJobs] ADD  CONSTRAINT [DF_AutoSysJilJobs_InsertedAt]  DEFAULT (sysutcdatetime()) FOR [InsertedAt]
GO

DROP TABLE IF EXISTS [dbo].[AutoSysPackageToObject];
GO

CREATE TABLE [dbo].[AutoSysPackageToObject](
	[PackageName] [varchar](max) NULL,
	[database_server] [varchar](max) NULL,
	[database] [varchar](max) NULL,
	[schema] [varchar](max) NULL,
	[object_name] [varchar](max) NULL,
	[object_type] [varchar](max) NULL,
	[ImportedAt] [datetime2](7) NOT NULL

) 
GO
ALTER TABLE [dbo].[AutoSysPackageToObject] ADD  DEFAULT (sysutcdatetime()) FOR [ImportedAt]
GO



CREATE PROCEDURE [dbo].[Overlap_Jobs_Gantt]
 @WeekStartDate date = '2026-07-13', -- Must be a Monday
 @DefaultRunTimeMinutes int = 15
 AS
 BEGIN

DROP TABLE IF EXISTS #Timeline;

WITH Days AS
(
    SELECT *
    FROM (VALUES
        ('mon',0),
        ('tue',1),
        ('wed',2),
        ('thu',3),
        ('fri',4),
        ('sat',5),
        ('sun',6)
    ) d(DayName, DayOffset)
),
Hours AS
(
    SELECT Hr
    FROM (VALUES
        (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),
        (12),(13),(14),(15),(16),(17),(18),(19),(20),(21),(22),(23)
    ) h(Hr)
),
JobDays AS
(
    SELECT
        j.JobName,
        j.StartMins,
        j.StartTimes,
        LTRIM(RTRIM(s.value)) AS DayName
    FROM dbo.AutoSysJilJobs j
    CROSS APPLY STRING_SPLIT
    (
        CASE
            WHEN LOWER(ISNULL(j.DaysOfWeek,'')) = 'all'
                THEN 'mon,tue,wed,thu,fri,sat,sun'
            ELSE LOWER(j.DaysOfWeek)
        END,
        ','
    ) s
),
Runs AS
(
    ---------------------------------------------------------
    -- Hourly schedules (StartMins)
    ---------------------------------------------------------
    SELECT
        jd.JobName,
        jd.DayName,
        CAST
        (
            CONCAT
            (
                RIGHT('00' + CAST(h.Hr AS varchar(2)),2),
                ':',
                RIGHT('00' + jd.StartMins,2)
            ) AS time
        ) AS StartTime
    FROM JobDays jd
    CROSS JOIN Hours h
    WHERE NULLIF(LTRIM(RTRIM(jd.StartMins)), '') IS NOT NULL

    UNION ALL

    ---------------------------------------------------------
    -- Explicit StartTimes schedules
    ---------------------------------------------------------
    SELECT
        jd.JobName,
        jd.DayName,
        CAST(LTRIM(RTRIM(s.value)) AS time) AS StartTime
    FROM JobDays jd
    CROSS APPLY STRING_SPLIT(jd.StartTimes, ',') s
    WHERE NULLIF(LTRIM(RTRIM(jd.StartTimes)), '') IS NOT NULL
),
AvgRuntime AS
(
    SELECT
        JobName,
        AVG(RunTimeInMinutes) AS AvgRunTimeMinutes
    FROM dbo.AutoSysRuntimeJobs
    GROUP BY JobName
)
SELECT
    r.JobName,
    p.PackageName,
    o.database_server,
    o.[database],
    o.[schema],
    o.object_name,

    CONCAT
    (
        ISNULL(o.database_server,''),
        '.',
        ISNULL(o.[database],''),
        '.',
        ISNULL(o.[schema],''),
        '.',
        ISNULL(o.object_name,'')
    ) AS ObjectKey,

    dt.StartDateTime,

    DATEADD
    (
        MINUTE,
        ISNULL(ar.AvgRunTimeMinutes,@DefaultRunTimeMinutes),
        dt.StartDateTime
    ) AS EndDateTime,

    ISNULL(ar.AvgRunTimeMinutes,@DefaultRunTimeMinutes) AS RunTimeMinutes

INTO #Timeline
FROM Runs r
JOIN Days d
    ON d.DayName = r.DayName
LEFT JOIN AvgRuntime ar
    ON ar.JobName = r.JobName
LEFT JOIN dbo.AutoSysJobToPackage p
    ON p.JobName = r.JobName
LEFT JOIN dbo.AutoSysPackageToObject o
    ON o.PackageName = p.PackageName
CROSS APPLY
(
    SELECT
        StartDateTime =
            DATEADD
            (
                SECOND,
                DATEDIFF
                (
                    SECOND,
                    CAST('00:00:00' AS time),
                    r.StartTime
                ),
                CAST(DATEADD(DAY,d.DayOffset,@WeekStartDate) AS datetime)
            )
) dt;

-------------------------------------------------------------------
-- Final Gantt Dataset
-------------------------------------------------------------------

SELECT
    t.JobName,
    t.PackageName,
    t.database_server,
    t.[database],
    t.[schema],
    t.object_name,
    t.ObjectKey,
    t.StartDateTime,
    t.EndDateTime,
    t.RunTimeMinutes,

    CASE
        WHEN COUNT(o.JobName) > 0 THEN 1
        ELSE 0
    END AS OverlapFlag,

    COUNT(o.JobName) AS OverlapCount

FROM #Timeline t
LEFT JOIN #Timeline o
       ON o.JobName <> t.JobName
      AND ISNULL(o.database_server,'') = ISNULL(t.database_server,'')
      AND ISNULL(o.[database],'')      = ISNULL(t.[database],'')
      AND ISNULL(o.[schema],'')        = ISNULL(t.[schema],'')
      AND ISNULL(o.object_name,'')     = ISNULL(t.object_name,'')

      AND o.StartDateTime < t.EndDateTime
      AND o.EndDateTime   > t.StartDateTime

GROUP BY
    t.JobName,
    t.PackageName,
    t.database_server,
    t.[database],
    t.[schema],
    t.object_name,
    t.ObjectKey,
    t.StartDateTime,
    t.EndDateTime,
    t.RunTimeMinutes

ORDER BY
    t.ObjectKey,
    t.StartDateTime,
    t.JobName;

DROP TABLE #Timeline;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[Overlap_Packages_Gantt]
 @WeekStartDate date = '2026-07-13', -- Must be a Monday
 @DefaultRunTimeMinutes int = 15
 AS
 BEGIN

DROP TABLE IF EXISTS #Timeline;

WITH Days AS
(
    SELECT *
    FROM (VALUES
        ('mon',0),
        ('tue',1),
        ('wed',2),
        ('thu',3),
        ('fri',4),
        ('sat',5),
        ('sun',6)
    ) d(DayName, DayOffset)
),
Hours AS
(
    SELECT Hr
    FROM (VALUES
        (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),
        (12),(13),(14),(15),(16),(17),(18),(19),(20),(21),(22),(23)
    ) h(Hr)
),
JobDays AS
(
    SELECT
        j.JobName,
        j.StartMins,
        j.StartTimes,
        LTRIM(RTRIM(s.value)) AS DayName
    FROM dbo.AutoSysJilJobs j
    CROSS APPLY STRING_SPLIT
    (
        CASE
            WHEN LOWER(ISNULL(j.DaysOfWeek,'')) = 'all'
                THEN 'mon,tue,wed,thu,fri,sat,sun'
            ELSE LOWER(j.DaysOfWeek)
        END,
        ','
    ) s
),
Runs AS
(
    ---------------------------------------------------------
    -- Hourly schedules (StartMins)
    ---------------------------------------------------------
    SELECT
        jd.JobName,
        jd.DayName,
        CAST
        (
            CONCAT
            (
                RIGHT('00' + CAST(h.Hr AS varchar(2)),2),
                ':',
                RIGHT('00' + jd.StartMins,2)
            ) AS time
        ) AS StartTime
    FROM JobDays jd
    CROSS JOIN Hours h
    WHERE NULLIF(LTRIM(RTRIM(jd.StartMins)), '') IS NOT NULL

    UNION ALL

    ---------------------------------------------------------
    -- Explicit StartTimes schedules
    ---------------------------------------------------------
    SELECT
        jd.JobName,
        jd.DayName,
        CAST(LTRIM(RTRIM(s.value)) AS time) AS StartTime
    FROM JobDays jd
    CROSS APPLY STRING_SPLIT(jd.StartTimes, ',') s
    WHERE NULLIF(LTRIM(RTRIM(jd.StartTimes)), '') IS NOT NULL
),
AvgRuntime AS
(
    SELECT
        JobName,
        AVG(RunTimeInMinutes) AS AvgRunTimeMinutes
    FROM dbo.AutoSysRuntimeJobs
    GROUP BY JobName
)
SELECT
    IDENTITY(int,1,1) AS TimelineId,
    p.PackageName,
    o.database_server,
    o.[database],
    o.[schema],
    o.object_name,

    CONCAT
    (
        ISNULL(o.database_server,''),
        '.',
        ISNULL(o.[database],''),
        '.',
        ISNULL(o.[schema],''),
        '.',
        ISNULL(o.object_name,'')
    ) AS ObjectKey,

    dt.StartDateTime,

    DATEADD
    (
        MINUTE,
        ISNULL(ar.AvgRunTimeMinutes,@DefaultRunTimeMinutes),
        dt.StartDateTime
    ) AS EndDateTime,

    ISNULL(ar.AvgRunTimeMinutes,@DefaultRunTimeMinutes) AS RunTimeMinutes

INTO #Timeline
FROM Runs r
JOIN Days d
    ON d.DayName = r.DayName
LEFT JOIN AvgRuntime ar
    ON ar.JobName = r.JobName
LEFT JOIN dbo.AutoSysJobToPackage p
    ON p.JobName = r.JobName
LEFT JOIN dbo.AutoSysPackageToObject o
    ON o.PackageName = p.PackageName
CROSS APPLY
(
    SELECT
        StartDateTime =
            DATEADD
            (
                SECOND,
                DATEDIFF
                (
                    SECOND,
                    CAST('00:00:00' AS time),
                    r.StartTime
                ),
                CAST(DATEADD(DAY,d.DayOffset,@WeekStartDate) AS datetime)
            )
) dt;

-------------------------------------------------------------------
-- Final package-level Gantt dataset (JobName omitted)
-------------------------------------------------------------------

SELECT
    t.PackageName,
    t.database_server,
    t.[database],
    t.[schema],
    t.object_name,
    t.ObjectKey,
    t.StartDateTime,
    t.EndDateTime,
    t.RunTimeMinutes,

    CASE
        WHEN COUNT(DISTINCT o.PackageName) > 0 THEN 1
        ELSE 0
    END AS OverlapFlag,

    COUNT(DISTINCT o.PackageName) AS OverlapCount

FROM #Timeline t
LEFT JOIN #Timeline o
       ON o.TimelineId <> t.TimelineId
      AND ISNULL(o.PackageName,'') <> ISNULL(t.PackageName,'')
      AND ISNULL(o.database_server,'') = ISNULL(t.database_server,'')
      AND ISNULL(o.[database],'')      = ISNULL(t.[database],'')
      AND ISNULL(o.[schema],'')        = ISNULL(t.[schema],'')
      AND ISNULL(o.object_name,'')     = ISNULL(t.object_name,'')

      AND o.StartDateTime < t.EndDateTime
      AND o.EndDateTime   > t.StartDateTime

GROUP BY
    t.PackageName,
    t.database_server,
    t.[database],
    t.[schema],
    t.object_name,
    t.ObjectKey,
    t.StartDateTime,
    t.EndDateTime,
    t.RunTimeMinutes

ORDER BY
    t.ObjectKey,
    t.StartDateTime,
    t.PackageName;

DROP TABLE #Timeline;
END;
GO
CREATE PROCEDURE [dbo].[GetNumberOfPackagesTablesAreIn]
@ImportedAt DATETIME2(0)
AS
BEGIN
    BEGIN TRY

        SELECT
            a.[PackageName],
            a.[database_server],
            a.[database],
            a.[schema],
            a.[object_name],
            a.[object_type],
            a.[ImportedAt],
            pc.ExistsInPackageCount,
            CASE WHEN pc.ExistsInPackageCount > 1 THEN 1 ELSE 0 END AS IsInMultiplePackages
        FROM [TRAIN].[dbo].[AutoSysPackageToObject] AS a
        CROSS APPLY (
            SELECT COUNT(DISTINCT p.[PackageName]) AS ExistsInPackageCount
            FROM [TRAIN].[dbo].[AutoSysPackageToObject] AS p
            WHERE p.[database_server] = a.[database_server]
              AND p.[database] = a.[database]
              AND p.[schema] = a.[schema]
              AND p.[object_name] = a.[object_name]
              AND CAST(p.ImportedAt AS DATETIME2(0)) = @ImportedAt
        ) AS pc;

    END TRY
    BEGIN CATCH
        -- Return error context and rethrow to preserve original error semantics
        SELECT
            ERROR_NUMBER()   AS ErrorNumber,
            ERROR_SEVERITY() AS ErrorSeverity,
            ERROR_STATE()    AS ErrorState,
            ERROR_PROCEDURE()AS ErrorProcedure,
            ERROR_LINE()     AS ErrorLine,
            ERROR_MESSAGE()  AS ErrorMessage;

        THROW; -- re-throw the original error
    END CATCH
END;
GO