using Microsoft.Extensions.Logging;
using QueryRouter.Core.Models;
using QueryRouter.Core.Services;

namespace QueryRouter.Core.Analyzers;

public class QueryAnalyzer : IQueryAnalyzer
{
    private readonly ILogger<QueryAnalyzer> _logger;
    private readonly AzureOpenAIService _openAIService;
    private readonly LangfuseService? _langfuse;

    public QueryAnalyzer(
        ILogger<QueryAnalyzer> logger,
        AzureOpenAIService openAIService,
        LangfuseService? langfuse = null)
    {
        _logger = logger;
        _openAIService = openAIService;
        _langfuse = langfuse;
    }

    public async Task<QueryRoutingResult> AnalyzeAsync(string query, string? traceId = null, string? parentSpanId = null)
    {
        _logger.LogInformation("Analyzing query with LLM: {Query}", query);
        
        var startTime = DateTime.UtcNow;
        var spanId = Guid.NewGuid().ToString();

        try
        {
            // Log Span Start
            if (_langfuse != null && !string.IsNullOrEmpty(traceId))
            {
                // We don't send start event explicitly in our simple service, we send the whole span at the end
                // But we need to keep track of start time
            }

            // Use LLM to analyze query intent
            var llmResponse = await _openAIService.AnalyzeQueryIntentAsync(query, traceId, spanId);

            // Map LLM response to QueryRoutingResult
            var result = new QueryRoutingResult
            {
                Route = ParseRoute(llmResponse.Route),
                Confidence = llmResponse.Confidence,
                Reasoning = llmResponse.Reasoning,
                LlmReasoning = llmResponse.Reasoning
            };

            // Map SQL intent
            if (llmResponse.SqlIntent != null)
            {
                result.SqlIntent = new SqlIntent
                {
                    Filters = llmResponse.SqlIntent.Filters,
                    Aggregations = llmResponse.SqlIntent.Aggregations,
                    IsLlmGenerated = true
                };
            }

            // Map Vector intent
            if (llmResponse.VectorIntent != null)
            {
                result.VectorIntent = new VectorIntent
                {
                    SemanticConcept = llmResponse.VectorIntent.SemanticConcept,
                    TopK = llmResponse.VectorIntent.TopK
                };
            }

            // Map Graph intent
            if (llmResponse.GraphIntent != null)
            {
                result.GraphIntent = new GraphIntent
                {
                    StartNode = llmResponse.GraphIntent.StartNode,
                    Relationship = llmResponse.GraphIntent.Relationship,
                    TargetNode = llmResponse.GraphIntent.TargetNode
                };
            }

            _logger.LogInformation("Query routed to {Route} with confidence {Confidence}", 
                result.Route, result.Confidence);
            _logger.LogInformation("LLM Reasoning: {Reasoning}", result.Reasoning);

            // Log Span End
            if (_langfuse != null && !string.IsNullOrEmpty(traceId))
            {
                _langfuse.CreateSpan(traceId, spanId, "QueryAnalyzer", startTime, DateTime.UtcNow);
                _langfuse.Score(traceId, "routing-confidence", result.Confidence);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query with LLM, falling back to default routing");
            
            // Log Span End (Error)
            if (_langfuse != null && !string.IsNullOrEmpty(traceId))
            {
                _langfuse.CreateSpan(traceId, spanId, "QueryAnalyzer (Failed)", startTime, DateTime.UtcNow);
                _langfuse.Score(traceId, "routing-success", 0);
            }

            // Fallback to simple routing if LLM fails
            return FallbackRouting(query);
        }
    }

    private RouteType ParseRoute(string routeString)
    {
        return routeString.ToUpper() switch
        {
            "SQL" => RouteType.SQL,
            "VECTOR" => RouteType.VECTOR,
            "GRAPH" => RouteType.GRAPH,
            "HYBRID" => RouteType.HYBRID,
            _ => RouteType.VECTOR // Default to vector search
        };
    }

    private QueryRoutingResult FallbackRouting(string query)
    {
        _logger.LogWarning("Using fallback routing for query: {Query}", query);

        var lowerQuery = query.ToLower();

        // Simple keyword-based fallback
        if (lowerQuery.Contains("count") || lowerQuery.Contains("how many") || lowerQuery.Contains("list all"))
        {
            return new QueryRoutingResult
            {
                Route = RouteType.SQL,
                Confidence = 0.6,
                Reasoning = "Fallback: Query contains aggregation keywords",
                SqlIntent = new SqlIntent
                {
                    Aggregations = new List<string> { "COUNT" }
                }
            };
        }

        if (lowerQuery.Contains("connected") || lowerQuery.Contains("related to") || lowerQuery.Contains("relationship"))
        {
            return new QueryRoutingResult
            {
                Route = RouteType.GRAPH,
                Confidence = 0.6,
                Reasoning = "Fallback: Query contains relationship keywords",
                GraphIntent = new GraphIntent()
            };
        }

        // Default to vector search
        return new QueryRoutingResult
        {
            Route = RouteType.VECTOR,
            Confidence = 0.5,
            Reasoning = "Fallback: Default to semantic search",
            VectorIntent = new VectorIntent
            {
                SemanticConcept = query,
                TopK = 10
            }
        };
    }
}
