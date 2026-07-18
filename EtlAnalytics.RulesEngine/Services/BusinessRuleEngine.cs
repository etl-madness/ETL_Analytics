using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Dapper;
using Microsoft.Data.SqlClient;
using EtlAnalytics.RulesEngine.Models;
using EtlAnalytics.RulesEngine.Interfaces;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Configuration;

namespace EtlAnalytics.RulesEngine.Services;

public class BusinessRuleEngine<TContext> where TContext : RuleExecutionContext
{
    private readonly string _connectionString;
    private readonly IBusinessRuleStore _ruleStore;
    private readonly IRuleDbProvider _dbProvider;

    public BusinessRuleEngine(IConfiguration configuration, IBusinessRuleStore ruleStore, IRuleDbProvider dbProvider)
    {
        _connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _ruleStore = ruleStore;
        _dbProvider = dbProvider;
    }

    public async Task<object?> ExecuteRuleAsync(
        BusinessRule rule,
        TContext? globals = null,
        Action<string>? appendLog = null)
    {
        if (globals != null)
        {
            globals.RunBundle = async (name) =>
            {
                var bundle = await _ruleStore.GetBusinessRuleBundleByNameAsync(name);
                if (bundle == null)
                {
                    appendLog?.Invoke($"[WARN] RunBundle: Bundle '{name}' not found.");
                    return null;
                }
                appendLog?.Invoke($"[INFO] Triggering nested bundle: {name}");
                return await ExecuteBundleAsync(bundle, globals, appendLog);
            };
        }

        appendLog?.Invoke($"[INFO] Starting execution of rule: {rule.Name} ({rule.RuleType})");

        try
        {
            if (rule.RuleType == RuleType.TSQL)
            {
                return await ExecuteTsqlAsync(rule.Code, globals, appendLog);
            }
            else if (rule.RuleType == RuleType.CSharp)
            {
                return await ExecuteCSharpAsync(rule.Code, globals, appendLog);
            }
            else
            {
                throw new NotSupportedException($"Rule type {rule.RuleType} is not supported.");
            }
        }
        catch (Exception ex)
        {
            appendLog?.Invoke($"[ERR] Execution failed: {ex.Message}");
            throw;
        }
    }

    public async Task<object?> ExecuteBundleAsync(
        BusinessRuleBundle bundle,
        TContext baseContext,
        Action<string>? appendLog = null)
    {
        appendLog?.Invoke($"[BUNDLE] --- Starting Bundle: {bundle.Name} ---");
        object? lastResult = null;

        foreach (var item in bundle.Items.OrderBy(i => i.SequenceOrder))
        {
            var rule = await _ruleStore.GetBusinessRuleByIdAsync(item.RuleId);
            if (rule == null)
            {
                appendLog?.Invoke($"[ERR] Rule ID {item.RuleId} not found. Skipping.");
                continue;
            }

            appendLog?.Invoke($"[BUNDLE] Step {item.SequenceOrder}: {rule.Name}");
            
            // Pipe results
            baseContext.PreviousResult = lastResult;
            
            try
            {
                lastResult = await ExecuteRuleAsync(rule, baseContext, appendLog);
                // Store in history
                baseContext.StepResults[item.SequenceOrder] = lastResult;
            }
            catch (Exception ex)
            {
                appendLog?.Invoke($"[BUNDLE] [FATAL] Step failed: {ex.Message}. Aborting bundle.");
                break;
            }
        }

        appendLog?.Invoke($"[BUNDLE] --- Bundle Finished: {bundle.Name} ---");
        return lastResult;
    }

    private async Task<object?> ExecuteTsqlAsync(string code, TContext? context, Action<string>? appendLog)
    {
        appendLog?.Invoke("[SQL] Executing T-SQL script...");
        using var connection = _dbProvider.CreateConnection(_connectionString);
        
        var parameters = new DynamicParameters();
        var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        
        string previousJson = context != null ? JsonSerializer.Serialize(context.PreviousResult, jsonOptions) : "[]";
        string stepResultsJson = context != null ? JsonSerializer.Serialize(context.StepResults, jsonOptions) : "{}";

        parameters.Add("PreviousResultJson", previousJson);
        parameters.Add("StepResultsJson", stepResultsJson);

        var results = await connection.QueryAsync<dynamic>(code, parameters);
        var resultList = results.ToList();
        
        appendLog?.Invoke($"[SQL] Execution completed. {resultList.Count} rows returned.");
        return resultList;
    }

    private async Task<object?> ExecuteCSharpAsync(string code, TContext? globals, Action<string>? appendLog)
    {
        appendLog?.Invoke("[CS] Compiling and executing C# script...");
        
        var options = ScriptOptions.Default
            .AddReferences(typeof(System.Linq.Enumerable).Assembly)
            .AddReferences(typeof(RuleExecutionContext).Assembly);

        // Add reference to the assembly containing TContext if it's different
        if (typeof(TContext).Assembly != typeof(RuleExecutionContext).Assembly)
        {
            options = options.AddReferences(typeof(TContext).Assembly);
        }

        options = options.AddImports("System", "System.Collections.Generic", "System.Linq", "System.Text", "EtlAnalytics.RulesEngine.Models");

        try
        {
            var result = await CSharpScript.EvaluateAsync(code, options, globals, typeof(TContext));
            appendLog?.Invoke("[CS] Execution completed successfully.");
            return result;
        }
        catch (CompilationErrorException ex)
        {
            appendLog?.Invoke($"[ERR] Compilation Error: {string.Join(Environment.NewLine, ex.Diagnostics)}");
            throw;
        }
    }
}
