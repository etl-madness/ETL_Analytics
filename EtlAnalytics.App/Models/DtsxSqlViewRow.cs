namespace EtlAnalytics.App.Models;

public class DtsxSqlViewRow
{
    public int Id { get; set; }
    public string? Description { get; set; }
    public string? Package { get; set; }
    public string? RefId { get; set; }
    public string? Pipeline { get; set; }
    public string? SqlSource { get; set; }
    public string? ResolvedSql { get; set; }
    public string? ConnectionString { get; set; }
    public string? ConnectionName { get; set; }
    public string? ConnectionDtsId { get; set; }
    public string? ConnectionType { get; set; }
    public string? ConnectionRefId { get; set; }
    public string? Name { get; set; }
    public string? ComponentType { get; set; }
    public DateTime? LoadDate { get; set; }
}
