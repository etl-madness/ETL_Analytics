using System.Threading.Tasks;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.RulesEngine.Interfaces;

public interface IBusinessRuleStore
{
    Task<BusinessRule?> GetBusinessRuleByIdAsync(int id);
    Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name);
    Task<DbConnectionDefinition?> GetDbConnectionByIdAsync(int id);
    Task<IEnumerable<DbConnectionDefinition>> GetAllDbConnectionsAsync();
}
