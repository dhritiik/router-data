using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QueryRouter.Core.Models;
using QueryRouter.Core.Services;
using QueryRouter.Data.SQL;

namespace QueryRouter.Data.Executors;

public class SqlQueryExecutor
{
    private readonly ILogger<SqlQueryExecutor> _logger;
    private readonly PosDbContext _dbContext;
    private readonly AzureOpenAIService _openAIService;
    private readonly DatabaseSchemaProvider _schemaProvider;

    public SqlQueryExecutor(
        ILogger<SqlQueryExecutor> logger,
        PosDbContext dbContext,
        AzureOpenAIService openAIService,
        DatabaseSchemaProvider schemaProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _openAIService = openAIService;
        _schemaProvider = schemaProvider;
    }

    public async Task<List<RequirementResult>> ExecuteAsync(QueryRoutingResult routing, string originalQuery, string? traceId = null, string? spanId = null)
    {
        try
        {
            var sqlIntent = routing.SqlIntent;
            if (sqlIntent == null)
            {
                _logger.LogWarning("No SQL intent provided");
                return new List<RequirementResult>();
            }

            string sqlQuery;

            // Try to use LLM to generate SQL if needed
            if (sqlIntent.IsLlmGenerated || string.IsNullOrEmpty(sqlIntent.GeneratedQuery))
            {
                try
                {
                    _logger.LogInformation("Attempting to generate SQL query using LLM for: {Query}", originalQuery);
                    
                    var schema = _schemaProvider.GetSchemaDescription();
                    sqlQuery = await _openAIService.GenerateSqlQueryAsync(originalQuery, schema, traceId, spanId);
                    
                    // Store generated SQL in routing result
                    routing.GeneratedSql = sqlQuery;
                    sqlIntent.GeneratedQuery = sqlQuery;
                }
                catch (Exception llmEx)
                {
                    _logger.LogWarning(llmEx, "LLM SQL generation failed, using fallback SQL generation");
                    
                    // Fallback: Generate simple SQL from intent
                    sqlQuery = GenerateFallbackSql(sqlIntent, originalQuery);
                    routing.GeneratedSql = sqlQuery;
                    sqlIntent.GeneratedQuery = sqlQuery;
                }
            }
            else
            {
                sqlQuery = sqlIntent.GeneratedQuery;
                _logger.LogInformation("Using pre-generated SQL query: {SQL}", sqlQuery);
            }

            _logger.LogInformation("Executing SQL: {SQL}", sqlQuery);

            // Execute the SQL query
            var results = await ExecuteRawSqlAsync(sqlQuery);

            _logger.LogInformation("SQL query returned {Count} results", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL query execution failed");
            return new List<RequirementResult>();
        }
    }

    private string GenerateFallbackSql(SqlIntent sqlIntent, string originalQuery)
    {
        var lowerQuery = originalQuery.ToLower();
        
        // Check for aggregation queries
        if (sqlIntent.Aggregations.Any(a => a.Contains("COUNT", StringComparison.OrdinalIgnoreCase)))
        {
            // Simple count query
            if (lowerQuery.Contains("security"))
            {
                return "SELECT COUNT(*) FROM Requirements WHERE RequirementType = 'security'";
            }
            else if (lowerQuery.Contains("functional"))
            {
                return "SELECT COUNT(*) FROM Requirements WHERE RequirementType = 'functional'";
            }
            else if (lowerQuery.Contains("high") && lowerQuery.Contains("criticality"))
            {
                return "SELECT COUNT(*) FROM Requirements WHERE Criticality = 'high'";
            }
            else
            {
                // Default: count all
                return "SELECT COUNT(*) FROM Requirements";
            }
        }
        
        // Default: return all requirements with limit
        return "SELECT * FROM Requirements LIMIT 50";
    }

    private async Task<List<RequirementResult>> ExecuteRawSqlAsync(string sqlQuery)
    {
        try
        {
            // Check if it's an aggregation query (COUNT, SUM, AVG, etc.)
            var isAggregation = sqlQuery.Contains("COUNT", StringComparison.OrdinalIgnoreCase) ||
                               sqlQuery.Contains("SUM", StringComparison.OrdinalIgnoreCase) ||
                               sqlQuery.Contains("AVG", StringComparison.OrdinalIgnoreCase) ||
                               sqlQuery.Contains("MAX", StringComparison.OrdinalIgnoreCase) ||
                               sqlQuery.Contains("MIN", StringComparison.OrdinalIgnoreCase);

            if (isAggregation)
            {
                // Execute aggregation query
                var connection = _dbContext.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = sqlQuery;
                
                var result = await command.ExecuteScalarAsync();
                var count = result != null ? Convert.ToInt32(result) : 0;

                _logger.LogInformation("Aggregation query returned: {Count}", count);

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
            else
            {
                // Execute regular SELECT query
                var requirements = await _dbContext.Requirements
                    .FromSqlRaw(sqlQuery)
                    .ToListAsync();

                return requirements.Select(r => new RequirementResult
                {
                    ClientReferenceId = r.ClientReferenceId,
                    NormalizedText = r.NormalizedText,
                    RawText = r.RawText,
                    ConstraintType = r.ConstraintType,
                    RequirementType = r.RequirementType,
                    Criticality = r.Criticality,
                    Score = 1.0,
                    Source = "SQL"
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing raw SQL: {SQL}", sqlQuery);
            throw;
        }
    }
}
