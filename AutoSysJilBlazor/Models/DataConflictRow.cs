namespace AutoSysJilBlazor.Models;

public class DataConflictRow
{
    public string? PackageName
    {
        get; set;
    }
    public string? DatabaseServer
    {
        get; set;
    }
    public string? Database
    {
        get; set;
    }
    public string? Schema
    {
        get; set;
    }
    public string? ObjectName
    {
        get; set;
    }
    public string? ObjectType
    {
        get; set;
    }
    public DateTime ImportedAt
    {
        get; set;
    }
    public int ExistsInPackageCount
    {
        get; set;
    }
    public int IsInMultiplePackages
    {
        get; set;
    }
}