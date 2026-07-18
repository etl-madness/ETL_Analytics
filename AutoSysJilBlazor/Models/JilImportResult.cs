using System.Collections.Generic;

namespace AutoSysJilBlazor.Models;

public class JilImportResult
{
    public List<JilJob> Jobs { get; set; } = new();
    public string SqlTableDefinition { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
}
