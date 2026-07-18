using AutoSysJilBlazor.Models;
using AutoSysJilBlazor.Services;
using NUnit.Framework;

namespace AutoSysJilBlazor.Tests
{
    [TestFixture]
    public class JilParserTests
    {
        [Test]
        public void Parse_SingleValidJob_ReturnsOneJob()
        {
            // Arrange
            var jilContent = @"
/* ----------------- JOB_A ----------------- */ 

insert_job: JOB_A
job_type: CMD
command: echo ""Hello World""
machine: localhost
owner: user@domain
description: ""A simple test job.""
";
            // Act
            var result = JilParser.Parse(jilContent, "test.jil");

            // Assert
            Assert.That(result.Jobs, Has.Count.EqualTo(1));
            var job = result.Jobs[0];
            Assert.That(job.JobName, Is.EqualTo("JOB_A"));
            Assert.That(job.JobType, Is.EqualTo("CMD"));
            Assert.That(job.Command, Is.EqualTo(@"echo ""Hello World"""));
            Assert.That(job.Machine, Is.EqualTo("localhost"));
            Assert.That(job.Owner, Is.EqualTo("user@domain"));
            Assert.That(job.Description, Is.EqualTo("A simple test job."));
        }

        [Test]
        public void Parse_TwoValidJobs_ReturnsTwoJobs()
        {
            // Arrange
            var jilContent = @"
/* ----------------- JOB_A ----------------- */ 

insert_job: JOB_A
job_type: CMD
command: echo ""Job A""

/* ----------------- JOB_B ----------------- */ 

insert_job: JOB_B
job_type: BOX
description: ""This is a box job""
";
            // Act
            var result = JilParser.Parse(jilContent, "test.jil");

            // Assert
            Assert.That(result.Jobs, Has.Count.EqualTo(2));
            Assert.That(result.Jobs[0].JobName, Is.EqualTo("JOB_A"));
            Assert.That(result.Jobs[1].JobName, Is.EqualTo("JOB_B"));
            Assert.That(result.Jobs[1].Description, Is.EqualTo("This is a box job"));
        }

        [Test]
        public void Parse_JobWithAllProperties_ParsesCorrectly()
        {
            // Arrange
            var jilContent = @"
/* ----------------- FULL_JOB ----------------- */ 

insert_job: FULL_JOB
job_type: CMD
command: /usr/bin/script.sh
machine: linux-server
owner: app-owner@domain
permission: gx,ge,wx,we,mx,me
date_conditions: 1
days_of_week: mo,tu,we,th,fr
start_mins: 30
start_times: ""10:00, 14:00""
timezone: UTC
description: ""This is a full job definition.""
std_out_file: /tmp/job.out
std_err_file: /tmp/job.err
alarm_if_fail: 1
application: MyApp
";
            // Act
            var result = JilParser.Parse(jilContent, "test.jil");

            // Assert
            Assert.That(result.Jobs, Has.Count.EqualTo(1));
            var job = result.Jobs[0];
            Assert.That(job.JobName, Is.EqualTo("FULL_JOB"));
            Assert.That(job.JobType, Is.EqualTo("CMD"));
            Assert.That(job.Command, Is.EqualTo("/usr/bin/script.sh"));
            Assert.That(job.Machine, Is.EqualTo("linux-server"));
            Assert.That(job.Owner, Is.EqualTo("app-owner@domain"));
            Assert.That(job.Permission, Is.EqualTo("gx,ge,wx,we,mx,me"));
            Assert.That(job.DateConditions, Is.EqualTo("1"));
            Assert.That(job.DaysOfWeek, Is.EqualTo("mo,tu,we,th,fr"));
            Assert.That(job.StartMins, Is.EqualTo("30"));
            Assert.That(job.StartTimes, Is.EqualTo("10:00, 14:00"));
            Assert.That(job.Timezone, Is.EqualTo("UTC"));
            Assert.That(job.Description, Is.EqualTo("This is a full job definition."));
            Assert.That(job.StdOutFile, Is.EqualTo("/tmp/job.out"));
            Assert.That(job.StdErrFile, Is.EqualTo("/tmp/job.err"));
            Assert.That(job.AlarmIfFail, Is.EqualTo("1"));
            Assert.That(job.Application, Is.EqualTo("MyApp"));
        }

        [Test]
        public void Parse_WithUnknownProperties_StoresInAdditionalProperties()
        {
            // Arrange
            var jilContent = @"
/* ----------------- JOB_C ----------------- */ 

insert_job: JOB_C
job_type: CMD
command: do_something
unknown_prop: some_value
another_prop: 123
";
            // Act
            var result = JilParser.Parse(jilContent, "test.jil");

            // Assert
            Assert.That(result.Jobs, Has.Count.EqualTo(1));
            var job = result.Jobs[0];
            Assert.That(job.AdditionalProperties, Has.Count.EqualTo(2));
            Assert.That(job.AdditionalProperties["unknown_prop"], Is.EqualTo("some_value"));
            Assert.That(job.AdditionalProperties["another_prop"], Is.EqualTo("123"));
        }

        [Test]
        public void Parse_EmptyInput_ReturnsNoJobs()
        {
            // Arrange
            var jilContent = "";

            // Act
            var result = JilParser.Parse(jilContent, "test.jil");

            // Assert
            Assert.That(result.Jobs, Is.Empty);
        }

        [Test]
        public void Parse_WhitespaceInput_ReturnsNoJobs()
        {
            // Arrange
            var jilContent = "   \n\r\t   \n  ";

            // Act
            var result = JilParser.Parse(jilContent, "test.jil");

            // Assert
            Assert.That(result.Jobs, Is.Empty);
        }

        [Test]
        public void Parse_InputWithNoJobs_ReturnsNoJobs()
        {
            // Arrange
            var jilContent = "/* This is a JIL file with no jobs */";

            // Act
            var result = JilParser.Parse(jilContent, "test.jil");

            // Assert
            Assert.That(result.Jobs, Is.Empty);
        }

        [Test]
        public void Parse_MultiLineDescription_ParsesCorrectly()
        {
            // Arrange
            var jilContent = @"
/* ----------------- MULTILINE_DESC ----------------- */ 

insert_job: MULTILINE_DESC
description: ""This is a
multi-line description that continues
on several lines.""
job_type: BOX
";
            // Act
            var result = JilParser.Parse(jilContent, "test.jil");

            // Assert
            Assert.That(result.Jobs, Has.Count.EqualTo(1));
            var job = result.Jobs[0];
            Assert.That(job.Description, Is.EqualTo("This is a\r\nmulti-line description that continues\r\non several lines."));
        }

        [Test]
        public void Parse_MultiLineUnknownProperty_StoresCombinedValue()
        {
            // Arrange
            var jilContent = @"
insert_job: MULTI_UNKNOWN
custom_prop: ""line one
line two
line three.""
";

            // Act
            var result = JilParser.Parse(jilContent, "custom.jil");

            // Assert
            Assert.That(result.Jobs, Has.Count.EqualTo(1));
            Assert.That(result.Jobs[0].AdditionalProperties["custom_prop"],
                Is.EqualTo("line one\r\nline two\r\nline three."));
        }

        [Test]
        public void Parse_CaseInsensitiveInsertJob_SplitsIntoSeparateJobs()
        {
            // Arrange
            var jilContent = @"
INSERT_JOB: JOB_UPPER
job_type: BOX

insert_job: JOB_LOWER
job_type: CMD
";

            // Act
            var result = JilParser.Parse(jilContent, "case.jil");

            // Assert
            Assert.That(result.Jobs, Has.Count.EqualTo(2));
            Assert.That(result.Jobs[0].JobName, Is.EqualTo("JOB_UPPER"));
            Assert.That(result.Jobs[1].JobName, Is.EqualTo("JOB_LOWER"));
        }

        [Test]
        public void Parse_SetsSourceName_AndSqlDefinition()
        {
            // Arrange
            var source = "rules_file.jil";
            var jilContent = @"
insert_job: META_JOB
job_type: BOX
";

            // Act
            var result = JilParser.Parse(jilContent, source);

            // Assert
            Assert.That(result.SourceName, Is.EqualTo(source));
            Assert.That(result.SqlTableDefinition, Does.Contain("CREATE TABLE dbo.AutoSysJilJobs"));
            Assert.That(result.SqlTableDefinition, Does.Contain("ImportedAt DATETIME2"));
        }
    }
}
