using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QueryRouter.Core.Models;
using QueryRouter.Data.SQL;

namespace QueryRouter.Data.Executors;

public class SqlQueryExecutor
{
    private readonly ILogger<SqlQueryExecutor> _logger;
    private readonly PosDbContext _dbContext;

    public SqlQueryExecutor(ILogger<SqlQueryExecutor> logger, PosDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<List<RequirementResult>> ExecuteAsync(SqlIntent sqlIntent)
    {
        try
        {
            _logger.LogInformation("Executing SQL query with {FilterCount} filters", sqlIntent.Filters.Count);

            var query = _dbContext.Requirements.AsQueryable();

            // Apply filters
            foreach (var filter in sqlIntent.Filters)
            {
                if (filter.Contains("requirement_type ="))
                {
                    var type = ExtractValue(filter);
                    query = query.Where(r => r.RequirementType.ToLower() == type.ToLower());
                }
                else if (filter.Contains("constraint.type ="))
                {
                    var type = ExtractValue(filter);
                    query = query.Where(r => r.ConstraintType.ToLower() == type.ToLower());
                }
                else if (filter.Contains("constraint_subcategories CONTAINS"))
                {
                    var subcategory = ExtractValue(filter);
                    query = query.Where(r => r.ConstraintSubcategories.ToLower().Contains(subcategory.ToLower()));
                }
                else if (filter.Contains("systems CONTAINS"))
                {
                    var system = ExtractValue(filter);
                    query = query.Where(r => r.Systems.ToLower().Contains(system.ToLower()));
                }
            }

            // Check if aggregation is requested
            var hasAggregation = sqlIntent.Aggregations.Any(a => 
                a.Contains("COUNT", StringComparison.OrdinalIgnoreCase) || 
                a.Contains("SUM", StringComparison.OrdinalIgnoreCase));

            if (hasAggregation)
            {
                // Return aggregated count result
                var count = await query.CountAsync();
                _logger.LogInformation("SQL aggregation query returned count: {Count}", count);

                return new List<RequirementResult>
                {
                    new RequirementResult
                    {
                        ClientReferenceId = "AGGREGATION_RESULT",
                        NormalizedText = $"Total count: {count}",
                        RawText = $"Aggregation query returned {count} matching records",
                        ConstraintType = "aggregation",
                        Score = 1.0,
                        Source = "SQL_AGGREGATION"
                    }
                };
            }

            // Execute query for individual results
            var results = await query.Take(50).ToListAsync();

            _logger.LogInformation("SQL query returned {Count} results", results.Count);

            return results.Select(r => new RequirementResult
            {
                ClientReferenceId = r.ClientReferenceId,
                NormalizedText = r.NormalizedText,
                RawText = r.RawText,
                ConstraintType = r.ConstraintType,
                ConstraintSubcategories = JsonSerializer.Deserialize<List<string>>(r.ConstraintSubcategories) ?? new(),
                RequirementType = r.RequirementType,
                Criticality = r.Criticality,
                Systems = JsonSerializer.Deserialize<List<string>>(r.Systems) ?? new(),
                Score = 1.0 // SQL results have perfect match
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL query");
            return new List<RequirementResult>();
        }
    }

    private string ExtractValue(string filter)
    {
        var parts = filter.Split('=', StringSplitOptions.TrimEntries);
        if (parts.Length > 1)
        {
            return parts[1].Trim();
        }

        parts = filter.Split("CONTAINS", StringSplitOptions.TrimEntries);
        if (parts.Length > 1)
        {
            return parts[1].Trim();
        }

        return string.Empty;
    }
}

public class RequirementResult
{
    public string ClientReferenceId { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string ConstraintType { get; set; } = string.Empty;
    public List<string> ConstraintSubcategories { get; set; } = new();
    public string RequirementType { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public List<string> Systems { get; set; } = new();
    public List<string> Regulations { get; set; } = new();
    public double Score { get; set; }
    public string Source { get; set; } = "SQL";
}
