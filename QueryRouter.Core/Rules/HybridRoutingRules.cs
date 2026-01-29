using QueryRouter.Core.Models;

namespace QueryRouter.Core.Rules;

/// <summary>
/// Rules for routing queries that require multiple databases (HYBRID)
/// </summary>
public class HybridRoutingRules
{
    private readonly SqlRoutingRules _sqlRules;
    private readonly VectorRoutingRules _vectorRules;
    private readonly GraphRoutingRules _graphRules;

    public HybridRoutingRules()
    {
        _sqlRules = new SqlRoutingRules();
        _vectorRules = new VectorRoutingRules();
        _graphRules = new GraphRoutingRules();
    }

    public QueryRoutingResult? Analyze(string query)
    {
        // Check if query has patterns from multiple databases
        var sqlResult = _sqlRules.Analyze(query);
        var vectorResult = _vectorRules.Analyze(query);
        var graphResult = _graphRules.Analyze(query);

        int databaseCount = 0;
        if (sqlResult != null) databaseCount++;
        if (vectorResult != null) databaseCount++;
        if (graphResult != null) databaseCount++;

        // Hybrid requires at least 2 databases
        if (databaseCount < 2)
        {
            return null;
        }

        double confidence = 0.7; // Base confidence for hybrid
        var reasoning = new List<string>();

        // Build combined intent
        SqlIntent? sqlIntent = null;
        VectorIntent? vectorIntent = null;
        GraphIntent? graphIntent = null;

        if (sqlResult != null)
        {
            sqlIntent = sqlResult.SqlIntent;
            reasoning.Add("SQL for structured filtering");
            confidence += 0.1;
        }

        if (vectorResult != null)
        {
            vectorIntent = vectorResult.VectorIntent;
            reasoning.Add("Vector for semantic similarity");
            confidence += 0.1;
        }

        if (graphResult != null)
        {
            graphIntent = graphResult.GraphIntent;
            reasoning.Add("Graph for relationship traversal");
            confidence += 0.1;
        }

        return new QueryRoutingResult
        {
            Route = RouteType.HYBRID,
            Confidence = Math.Min(confidence, 0.95),
            Reasoning = $"Query requires hybrid approach: {string.Join(", ", reasoning)}",
            SqlIntent = sqlIntent,
            VectorIntent = vectorIntent,
            GraphIntent = graphIntent
        };
    }
}
