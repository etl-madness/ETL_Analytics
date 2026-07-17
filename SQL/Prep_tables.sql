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
CREATE PROCEDURE DBO.GetCurrentConflictingJobs 
AS
BEGIN
    BEGIN TRY
        DECLARE @LatestJobToPackage Datetime
        DECLARE @LatestJilJobs Datetime
        DECLARE @LatestPackageToObject Datetime
        DECLARE @LatestRunTimeJobs Datetime

        SELECT @LatestJobToPackage=MAX(FORMAT(ImportedAt,'yyyy-MM-dd HH:mm:ss')) from [dbo].[AutoSysJobToPackage] (NOLOCK)
        SELECT @LatestJilJobs=MAX(FORMAT(ImportedAt,'yyyy-MM-dd HH:mm:ss')) from  [dbo].[AutoSysJilJobs] (NOLOCK)
        SELECT @LatestPackageToObject=MAX(ImportedAt) from  [dbo].[AutoSysPackageToObject](NOLOCK)

        -- select @LatestJilJobs, @LatestJobToPackage
        ;WITH Days AS
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
                LTRIM(RTRIM(s.value)) AS DayName,
                j.ImportedAt
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
             where FORMAT(j.ImportedAt,'yyyy-MM-dd HH:mm:ss')= @LatestJilJobs

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
        ),
        Packages as (
        SELECT  [JobName]
              ,[PackageName]
              ,[ImportedAt] = FORMAT(ImportedAt,'yyyy-MM-dd HH:mm:ss')
          FROM [TRAIN].[dbo].[AutoSysJobToPackage]
          WHERE   FORMAT(ImportedAt,'yyyy-MM-dd HH:mm:ss')= @LatestJobToPackage
        ),
        Combined AS (
        SELECT Runs.JobName,
            Runs.DayName,
            Runs.StartTime,
            [EndTime] = DATEADD(MINUTE,AvgRuntime.AvgRunTimeMinutes, Runs.StartTime),
            Packages.PackageName,
            AvgRuntime.AvgRunTimeMinutes 
        FROM Runs
        left join Packages on Packages.JobName=Runs.JobName
        left join AvgRuntime on Runs.JobName=AvgRuntime.JobName
        ) ,
        MappedDataObjects AS (
            SELECT    [PackageName]
              ,[database_server]
              ,[database]
              ,[schema]
              ,[object_name]
              ,[object_type]
              ,[ImportedAt]
          FROM [TRAIN].[dbo].[AutoSysPackageToObject]
  
        ),
        ObjectPackageCounts AS
        (
            SELECT
                m.[database_server],
                m.[database],
                m.[schema],
                m.[object_name],
                m.[object_type],
                COUNT(DISTINCT m.[PackageName]) AS SharedPackageCount
            FROM MappedDataObjects m
            GROUP BY
                m.[database_server],
                m.[database],
                m.[schema],
                m.[object_name],
                m.[object_type]
            --HAVING COUNT(DISTINCT m.[PackageName]) > 1
        ) ,
        PackageObjectRelationship AS (SELECT
            c.[JobName],
            c.[DayName],
            c.[StartTime],
            c.[EndTime],
            c.[PackageName],
            c.[AvgRunTimeMinutes],
            m.[database_server],
            m.[database],
            m.[schema],
            m.[object_name],
            m.[object_type],
            opc.[SharedPackageCount]
        FROM Combined c
        LEFT JOIN MappedDataObjects m
            ON m.[PackageName] = c.[PackageName]
        INNER JOIN ObjectPackageCounts opc
            ON ISNULL(opc.[database_server], '') = ISNULL(m.[database_server], '')
           AND ISNULL(opc.[database], '') = ISNULL(m.[database], '')
           AND ISNULL(opc.[schema], '') = ISNULL(m.[schema], '')
           AND ISNULL(opc.[object_name], '') = ISNULL(m.[object_name], '')
           AND ISNULL(opc.[object_type], '') = ISNULL(m.[object_type], '')
 
            ) 
             SELECT DISTINCT
            p1.[JobName],
            p1.[DayName],
            p1.[StartTime],
            p1.[EndTime],
            p1.[PackageName],
            p1.[AvgRunTimeMinutes],
            p1.[database_server],
            p1.[database],
            p1.[schema],
            p1.[object_name],
            p1.[object_type],
            p1.[SharedPackageCount]
        FROM PackageObjectRelationship p1
        WHERE EXISTS
        (
            SELECT 1
            FROM PackageObjectRelationship p2
            WHERE p2.[PackageName] <> p1.[PackageName]
              AND p2.[DayName] = p1.[DayName]
              AND ISNULL(p2.[database_server], '') = ISNULL(p1.[database_server], '')
              AND ISNULL(p2.[database], '') = ISNULL(p1.[database], '')
              AND ISNULL(p2.[schema], '') = ISNULL(p1.[schema], '')
              AND ISNULL(p2.[object_name], '') = ISNULL(p1.[object_name], '')
              AND ISNULL(p2.[object_type], '') = ISNULL(p1.[object_type], '')
              AND (
                    p1.[StartTime] = p2.[StartTime]
                    OR p1.[EndTime] BETWEEN p2.StartTime AND  p2.[EndTime]
                  )
        )
        ORDER BY
        p1.[DayName],
        p1.[StartTime],

            p1.[database_server],
            p1.[database],
            p1.[schema],
            p1.[object_name],
            p1.[object_type],
    
    
            p1.[PackageName],
            p1.[JobName];
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
 
 
CREATE PROCEDURE [dbo].[GetCurrentConflictingJobsByJilImportDate] 
@LatestJilJobs Datetime
AS
BEGIN
    BEGIN TRY
        DECLARE @LatestJobToPackage Datetime
        DECLARE @LatestPackageToObject Datetime
        DECLARE @LatestRunTimeJobs Datetime

        SELECT @LatestJobToPackage=MAX(FORMAT(ImportedAt,'yyyy-MM-dd HH:mm:ss')) from [dbo].[AutoSysJobToPackage] (NOLOCK)
        SELECT @LatestPackageToObject=MAX(ImportedAt) from  [dbo].[AutoSysPackageToObject](NOLOCK)

        -- select @LatestJilJobs, @LatestJobToPackage
        ;WITH Days AS
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
                LTRIM(RTRIM(s.value)) AS DayName,
                j.ImportedAt
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
             where FORMAT(j.ImportedAt,'yyyy-MM-dd HH:mm:ss')= @LatestJilJobs

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
        ),
        Packages as (
        SELECT  [JobName]
              ,[PackageName]
              ,[ImportedAt] = FORMAT(ImportedAt,'yyyy-MM-dd HH:mm:ss')
          FROM [TRAIN].[dbo].[AutoSysJobToPackage]
          WHERE   FORMAT(ImportedAt,'yyyy-MM-dd HH:mm:ss')= @LatestJobToPackage
        ),
        Combined AS (
        SELECT Runs.JobName,
            Runs.DayName,
            Runs.StartTime,
            [EndTime] = DATEADD(MINUTE,AvgRuntime.AvgRunTimeMinutes, Runs.StartTime),
            Packages.PackageName,
            AvgRuntime.AvgRunTimeMinutes 
        FROM Runs
        left join Packages on Packages.JobName=Runs.JobName
        left join AvgRuntime on Runs.JobName=AvgRuntime.JobName
        ) ,
        MappedDataObjects AS (
            SELECT    [PackageName]
              ,[database_server]
              ,[database]
              ,[schema]
              ,[object_name]
              ,[object_type]
              ,[ImportedAt]
          FROM [TRAIN].[dbo].[AutoSysPackageToObject]
  
        ),
        ObjectPackageCounts AS
        (
            SELECT
                m.[database_server],
                m.[database],
                m.[schema],
                m.[object_name],
                m.[object_type],
                COUNT(DISTINCT m.[PackageName]) AS SharedPackageCount
            FROM MappedDataObjects m
            GROUP BY
                m.[database_server],
                m.[database],
                m.[schema],
                m.[object_name],
                m.[object_type]
            --HAVING COUNT(DISTINCT m.[PackageName]) > 1
        ) ,
        PackageObjectRelationship AS (SELECT
            c.[JobName],
            c.[DayName],
            c.[StartTime],
            c.[EndTime],
            c.[PackageName],
            c.[AvgRunTimeMinutes],
            m.[database_server],
            m.[database],
            m.[schema],
            m.[object_name],
            m.[object_type],
            opc.[SharedPackageCount]
        FROM Combined c
        LEFT JOIN MappedDataObjects m
            ON m.[PackageName] = c.[PackageName]
        INNER JOIN ObjectPackageCounts opc
            ON ISNULL(opc.[database_server], '') = ISNULL(m.[database_server], '')
           AND ISNULL(opc.[database], '') = ISNULL(m.[database], '')
           AND ISNULL(opc.[schema], '') = ISNULL(m.[schema], '')
           AND ISNULL(opc.[object_name], '') = ISNULL(m.[object_name], '')
           AND ISNULL(opc.[object_type], '') = ISNULL(m.[object_type], '')
 
            ) 
             SELECT DISTINCT
            p1.[JobName],
            p1.[DayName],
            p1.[StartTime],
            p1.[EndTime],
            p1.[PackageName],
            p1.[AvgRunTimeMinutes],
            p1.[database_server],
            p1.[database],
            p1.[schema],
            p1.[object_name],
            p1.[object_type],
            p1.[SharedPackageCount]
        FROM PackageObjectRelationship p1
        WHERE EXISTS
        (
            SELECT 1
            FROM PackageObjectRelationship p2
            WHERE p2.[PackageName] <> p1.[PackageName]
              AND p2.[DayName] = p1.[DayName]
              AND ISNULL(p2.[database_server], '') = ISNULL(p1.[database_server], '')
              AND ISNULL(p2.[database], '') = ISNULL(p1.[database], '')
              AND ISNULL(p2.[schema], '') = ISNULL(p1.[schema], '')
              AND ISNULL(p2.[object_name], '') = ISNULL(p1.[object_name], '')
              AND ISNULL(p2.[object_type], '') = ISNULL(p1.[object_type], '')
              AND (
                    p1.[StartTime] = p2.[StartTime]
                    OR p1.[EndTime] BETWEEN p2.StartTime AND  p2.[EndTime]
                  )
        )
        ORDER BY
        p1.[DayName],
        p1.[StartTime],

            p1.[database_server],
            p1.[database],
            p1.[schema],
            p1.[object_name],
            p1.[object_type],
    
    
            p1.[PackageName],
            p1.[JobName];
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
 END
 
 
GO

 

CREATE TABLE [dbo].[DTSX_Attributes](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[CreationName] [nvarchar](max) NULL,
	[Description] [nvarchar](max) NULL,
	[Filename] [nvarchar](max) NULL,
	[Package] [nvarchar](max) NULL,
	[ParentNodeDtsId] [nvarchar](max) NULL,
	[ParentNodeName] [nvarchar](max) NULL,
	[ParentNodeType] [nvarchar](max) NULL,
	[ParentUniqueId] [nvarchar](250) NULL,
	[UniqueId] [nvarchar](250) NULL,
	[ParentRefId] [nvarchar](max) NULL,
	[RefId] [nvarchar](max) NULL,
	[XPath] [nvarchar](max) NULL,
	[ElementXPath] [nvarchar](max) NULL,
	[AttributeName] [nvarchar](max) NULL,
	[AttributeType] [nvarchar](max) NULL,
	[AttributeValue] [nvarchar](max) NULL,
	[LoadDate] [datetime] NOT NULL DEFAULT CURRENT_TIMESTAMP,
);

 

CREATE TABLE [dbo].[DTSX_Elements](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[CreationName] [nvarchar](max) NULL,
	[Description] [nvarchar](max) NULL,
	[Filename] [nvarchar](max) NULL,
	[Package] [nvarchar](max) NULL,
	[ParentNodeDtsId] [nvarchar](max) NULL,
	[ParentNodeName] [nvarchar](max) NULL,
	[ParentNodeType] [nvarchar](max) NULL,
	[ParentUniqueId] [nvarchar](250) NULL,
	[UniqueId] [nvarchar](250) NULL,
	[ParentRefId] [nvarchar](max) NULL,
	[RefId] [nvarchar](max) NULL,
	[XPath] [nvarchar](max) NULL,
	[DtsId] [nvarchar](max) NULL,
	[Name] [nvarchar](max) NULL,
	[NodeType] [nvarchar](max) NULL,
	[Value] [nvarchar](max) NULL,
	[XmlType] [nvarchar](max) NULL,
	[LoadDate] [datetime] NOT NULL DEFAULT CURRENT_TIMESTAMP,
);

 

CREATE TABLE [dbo].[DTSX_Mapper](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[Description] [nvarchar](max) NULL,
	[Package] [nvarchar](max) NULL,
	[RefId] [nvarchar](max) NULL,
	[SqlStatement] [nvarchar](max) NULL,
	[ConnectionString] [nvarchar](max) NULL,
	[ConnectionName] [nvarchar](max) NULL,
	[ConnectionDtsId] [nvarchar](max) NULL,
	[ConnectionType] [nvarchar](max) NULL,
	[ConnectionRefId] [nvarchar](max) NULL,
	[Name] [nvarchar](max) NULL,
	[ComponentType] [nvarchar](max) NULL,
	[LoadDate] [datetime] NOT NULL DEFAULT CURRENT_TIMESTAMP,
);
 

CREATE TABLE [dbo].[DTSX_Variables](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[CreationName] [nvarchar](max) NULL,
	[Description] [nvarchar](max) NULL,
	[Filename] [nvarchar](max) NULL,
	[Package] [nvarchar](max) NULL,
	[ParentNodeDtsId] [nvarchar](max) NULL,
	[ParentNodeName] [nvarchar](max) NULL,
	[ParentNodeType] [nvarchar](max) NULL,
	[ParentUniqueId] [nvarchar](250) NULL,
	[UniqueId] [nvarchar](250) NULL,
	[ParentRefId] [nvarchar](max) NULL,
	[RefId] [nvarchar](max) NULL,
	[XPath] [nvarchar](max) NULL,
	[EvaluateAsExpression] [nvarchar](max) NULL,
	[IncludeInDebugDump] [nvarchar](max) NULL,
	[VariableDataType] [nvarchar](max) NULL,
	[VariableDtsxId] [nvarchar](max) NULL,
	[VariableExpression] [nvarchar](max) NULL,
	[VariableName] [nvarchar](max) NULL,
	[VariableNameSpace] [nvarchar](max) NULL,
	[VariableValue] [nvarchar](max) NULL,
	[LoadDate] [datetime] NOT NULL DEFAULT CURRENT_TIMESTAMP,
);
GO
CREATE VIEW DTSX_SQL_View
AS

SELECT   m.[Id]
      ,m.[Description]
      ,m.[Package]
      ,m.[RefId]
	  ,'Pipeline' =   CASE
		  WHEN SUBSTRING(m.[RefId], 1, LEN(m.[RefId]) - CHARINDEX('\', REVERSE(m.[RefId]))) = 'Package' THEN m.[RefId]
		  ELSE SUBSTRING(m.[RefId], 1, LEN(m.[RefId]) - CHARINDEX('\', REVERSE(m.[RefId]))) 
		  END

      ,'SQL Source' = m.[SqlStatement]
	  ,'Resolved SQL' = CASE 
		  WHEN v.VariableValue  IS NULL THEN m.SqlStatement
		  ELSE v.VariableValue
		  END
      ,m.[ConnectionString]
      ,m.[ConnectionName]
      ,m.[ConnectionDtsId]
      ,m.[ConnectionType]
      ,m.[ConnectionRefId]
      ,m.[Name]
      ,m.[ComponentType]
      ,m.[LoadDate]
  FROM [dbo].[DTSX_Mapper] m
  LEFT JOIN [dbo].[DTSX_Variables] v on v.Package=m.Package and CONCAT(v.[VariableNameSpace],'::',v.[VariableName]) = m.SqlStatement
GO

-- Main table for business rules
IF OBJECT_ID('dbo.BusinessRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BusinessRules (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(255) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        RuleType NVARCHAR(50) NOT NULL, -- TSQL, CSharp
        Code NVARCHAR(MAX) NOT NULL,
        Version INT NOT NULL DEFAULT 1,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
-- History table for versioning
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
        CONSTRAINT FK_BusinessRuleHistory_RuleId FOREIGN KEY (RuleId) REFERENCES dbo.BusinessRules(Id)
    );
END;
GO