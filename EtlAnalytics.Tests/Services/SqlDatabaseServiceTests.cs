using EtlAnalytics.App.Services;
using EtlAnalytics.RulesEngine.Models;
using Microsoft.Extensions.Configuration;

namespace EtlAnalytics.Tests.Services;

[TestFixture]
public class SqlDatabaseServiceTests
{
    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder().Build();

    [Test]
    public void Constructor_WithoutDbConnectionString_ThrowsInvalidOperationException()
    {
        var original = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", null);

            Assert.Throws<InvalidOperationException>(() => _ = new SqlDatabaseService(CreateConfiguration()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", original);
        }
    }

    [Test]
    public void Constructor_WithDbConnectionString_DoesNotThrow()
    {
        var original = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        try
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", "Server=(local);Database=master;Trusted_Connection=True;");

            Assert.DoesNotThrow(() => _ = new SqlDatabaseService(CreateConfiguration()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", original);
        }
    }
}
