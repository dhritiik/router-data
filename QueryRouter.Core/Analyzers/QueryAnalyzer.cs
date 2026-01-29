using Microsoft.Extensions.Logging;
using QueryRouter.Core.Models;
using QueryRouter.Core.Rules;

namespace QueryRouter.Core.Analyzers;

/// <summary>
/// Main query analyzer that routes queries to appropriate databases
/// </summary>
public class QueryAnalyzer : IQueryAnalyzer
{
    private readonly ILogger<QueryAnalyzer> _logger;
    private readonly SqlRoutingRules _sqlRules;
    private readonly VectorRoutingRules _vectorRules;
    private readonly GraphRoutingRules _graphRules;
    private readonly HybridRoutingRules _hybridRules;

    public QueryAnalyzer(ILogger<QueryAnalyzer> logger)
    {
        _logger = logger;
        _sqlRules = new SqlRoutingRules();
        _vectorRules = new VectorRoutingRules();
        _graphRules = new GraphRoutingRules();
        _hybridRules = new HybridRoutingRules();
    }

    public async Task<QueryRoutingResult> AnalyzeQueryAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        }

        _logger.LogInformation("Analyzing query: {Query}", query);

        var normalizedQuery = query.ToLowerInvariant().Trim();

        // Check for hybrid patterns first (most complex)
        var hybridResult = _hybridRules.Analyze(normalizedQuery);
        if (hybridResult != null)
        {
            _logger.LogInformation("Query routed to HYBRID with confidence {Confidence}", hybridResult.Confidence);
            return hybridResult;
        }

        // Check for graph patterns (relationship-based)
        var graphResult = _graphRules.Analyze(normalizedQuery);
        if (graphResult != null)
        {
            _logger.LogInformation("Query routed to GRAPH with confidence {Confidence}", graphResult.Confidence);
            return graphResult;
        }

        // Check for vector patterns (semantic similarity)
        var vectorResult = _vectorRules.Analyze(normalizedQuery);
        if (vectorResult != null)
        {
            _logger.LogInformation("Query routed to VECTOR with confidence {Confidence}", vectorResult.Confidence);
            return vectorResult;
        }

        // Default to SQL for structured queries
        var sqlResult = _sqlRules.Analyze(normalizedQuery);
        if (sqlResult != null)
        {
            _logger.LogInformation("Query routed to SQL with confidence {Confidence}", sqlResult.Confidence);
            return sqlResult;
        }

        // Fallback with low confidence
        _logger.LogWarning("Unable to confidently route query, defaulting to SQL");
        return new QueryRoutingResult
        {
            Route = RouteType.SQL,
            Confidence = 0.3,
            Reasoning = "Unable to determine specific routing pattern, defaulting to SQL for structured retrieval",
            SqlIntent = new SqlIntent()
        };
    }
}
