using System.Data;

namespace EtlAnalytics.RulesEngine.Interfaces;

public interface IRuleDbProvider
{
    string ProviderType { get; }
    IDbConnection CreateConnection(string connectionString);
}
