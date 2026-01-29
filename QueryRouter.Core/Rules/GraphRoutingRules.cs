using QueryRouter.Core.Models;

namespace QueryRouter.Core.Rules;

/// <summary>
/// Rules for routing queries to Graph database for relationship traversal
/// </summary>
public class GraphRoutingRules
{
    private static readonly string[] RelationshipKeywords = new[]
    {
        "related to", "connected to", "linked to", "impacted by", "affected by",
        "depends on", "dependency", "impact", "downstream", "upstream",
        "relationship", "traverse"
    };

    private static readonly string[] GraphEntities = new[]
    {
        "pci-dss", "iras", "pdpa", "regulation", "compliance",
        "changipay", "integration", "system", "frontend", "backend",
        "self-checkout", "pos frontend", "pos backend"
    };

    private static readonly Dictionary<string, string[]> RelationshipPatterns = new()
    {
        ["COMPLIES_WITH"] = new[] { "pci-dss", "iras", "pdpa", "regulation", "compliance" },
        ["INTEGRATES_WITH"] = new[] { "changipay", "integration", "payment gateway" },
        ["AFFECTS"] = new[] { "system", "frontend", "backend", "component" },
        ["DEPENDS_ON"] = new[] { "dependency", "requires", "needs" },
        ["RELATED_TO"] = new[] { "related", "connected", "linked" }
    };

    public QueryRoutingResult? Analyze(string query)
    {
        double confidence = 0.0;
        string startNode = string.Empty;
        var relationshipTypes = new List<string>();
        int depth = 1;

        // Check for relationship keywords
        var relationshipKeywordMatches = RelationshipKeywords.Count(kw => query.Contains(kw));
        if (relationshipKeywordMatches > 0)
        {
            confidence += 0.4;
        }
        else
        {
            // If no relationship keywords, not a graph query
            return null;
        }

        // Check for graph entities
        foreach (var entity in GraphEntities)
        {
            if (query.Contains(entity))
            {
                startNode = entity;
                confidence += 0.3;
                break;
            }
        }

        // Identify relationship types
        foreach (var pattern in RelationshipPatterns)
        {
            var matches = pattern.Value.Count(term => query.Contains(term));
            if (matches > 0)
            {
                relationshipTypes.Add(pattern.Key);
                confidence += 0.2;
            }
        }

        // Check for multi-hop traversal indicators
        if (query.Contains("downstream") || query.Contains("upstream") || query.Contains("all"))
        {
            depth = 3;
            confidence += 0.15;
        }
        else if (query.Contains("direct") || query.Contains("immediate"))
        {
            depth = 1;
            confidence += 0.1;
        }
        else
        {
            depth = 2; // Default depth
        }

        // Check for impact analysis
        if (query.Contains("impact") || query.Contains("affected"))
        {
            relationshipTypes.Add("AFFECTS");
            confidence += 0.2;
        }

        // Only return graph route if we have good confidence
        if (confidence >= 0.6)
        {
            return new QueryRoutingResult
            {
                Route = RouteType.GRAPH,
                Confidence = Math.Min(confidence, 0.95),
                Reasoning = "Query requires relationship traversal and dependency analysis",
                GraphIntent = new GraphIntent
                {
                    StartNode = startNode,
                    RelationshipTypes = relationshipTypes,
                    Depth = depth
                }
            };
        }

        return null;
    }
}
