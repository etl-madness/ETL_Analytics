namespace EtlAnalytics.RulesEngine.Models;

public class DbConnectionDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string ProviderType { get; set; } = "SqlServer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
