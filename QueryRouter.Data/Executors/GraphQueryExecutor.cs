using Microsoft.Extensions.Logging;
using QueryRouter.Core.Models;
using QueryRouter.Data.Graph;

namespace QueryRouter.Data.Executors;

public class GraphQueryExecutor
{
    private readonly ILogger<GraphQueryExecutor> _logger;
    private readonly Neo4jGraphStore _graphStore;

    public GraphQueryExecutor(ILogger<GraphQueryExecutor> logger, Neo4jGraphStore graphStore)
    {
        _logger = logger;
        _graphStore = graphStore;
    }

    public async Task<List<RequirementResult>> ExecuteAsync(GraphIntent graphIntent)
    {
        try
        {
            _logger.LogInformation("Executing graph traversal from node: {Node} with depth: {Depth}", 
                graphIntent.StartNode, graphIntent.Depth);

            var results = await _graphStore.TraverseAsync(graphIntent.StartNode, graphIntent.Depth);

            _logger.LogInformation("Graph traversal returned {Count} results", results.Count);

            return results.Select(r => new RequirementResult
            {
                ClientReferenceId = r.GetValueOrDefault("client_ref")?.ToString() ?? "",
                NormalizedText = r.GetValueOrDefault("text")?.ToString() ?? "",
                ConstraintType = r.GetValueOrDefault("constraint_type")?.ToString() ?? "",
                Score = 1.0,
                Source = "GRAPH"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing graph query");
            return new List<RequirementResult>();
        }
    }
}
