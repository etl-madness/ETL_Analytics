using System.Data;
using Microsoft.Data.SqlClient;
using EtlAnalytics.RulesEngine.Interfaces;

namespace EtlAnalytics.RulesEngine.Providers;

public class SqlServerRuleDbProvider : IRuleDbProvider
{
    public IDbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}
