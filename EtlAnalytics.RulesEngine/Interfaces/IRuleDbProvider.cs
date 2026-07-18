using System.Data;

namespace EtlAnalytics.RulesEngine.Interfaces;

public interface IRuleDbProvider
{
    IDbConnection CreateConnection(string connectionString);
}
