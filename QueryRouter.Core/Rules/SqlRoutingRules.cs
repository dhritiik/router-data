using QueryRouter.Core.Models;

namespace QueryRouter.Core.Rules;

/// <summary>
/// Rules for routing queries to SQL database
/// </summary>
public class SqlRoutingRules
{
    private static readonly string[] SqlKeywords = new[]
    {
        "list", "show", "count", "get", "filter", "where", "group by", "sum", "average",
        "total", "report", "dashboard", "all", "by", "with", "having"
    };

    private static readonly string[] StructuredFields = new[]
    {
        "proposal_id", "rfp_id", "customer_name", "industry", "region", "product_name",
        "constraint.type", "constraint_subcategories"
    };

    private static readonly string[] ConstraintTypes = new[]
    {
        "functional", "operational", "compliance", "performance", "security", "usability"
    };

    private static readonly string[] Subcategories = new[]
    {
        "payments", "ui", "security", "promotions", "finance", "software", "version", "observation"
    };

    private static readonly string[] SystemKeywords = new[]
    {
        "pos", "frontend", "backend", "monitoring", "siem", "self-checkout", "sco"
    };

    public QueryRoutingResult? Analyze(string query)
    {
        var filters = new List<string>();
        var joins = new List<string>();
        var aggregations = new List<string>();
        double confidence = 0.0;
        
        var queryLower = query.ToLower();

        // Check for SQL keywords
        var sqlKeywordMatches = SqlKeywords.Count(kw => queryLower.Contains(kw));
        if (sqlKeywordMatches > 0)
        {
            confidence += 0.3;
        }

        // Detect if systems are mentioned
        var systemMentioned = SystemKeywords.Any(sys => queryLower.Contains(sys));

        // Check for requirement types (primary field from classification)
        var matchedRequirementTypes = new HashSet<string>();
        foreach (var reqType in ConstraintTypes) // Reusing same list for requirement types
        {
            if (queryLower.Contains(reqType))
            {
                if (systemMentioned)
                {
                    // If system mentioned, use constraint.type instead
                    filters.Add($"constraint.type = {reqType}");
                }
                else
                {
                    // Default: use requirement_type from classification
                    filters.Add($"requirement_type = {reqType}");
                }
                matchedRequirementTypes.Add(reqType);
                confidence += 0.25;
            }
        }

        // Check for subcategories (always check, but skip if already matched as requirement type)
        foreach (var subcategory in Subcategories)
        {
            // Use word boundary to avoid matching "ui" in "requirements"
            var pattern = $@"\b{subcategory}\b";
            if (System.Text.RegularExpressions.Regex.IsMatch(queryLower, pattern) && !matchedRequirementTypes.Contains(subcategory))
            {
                filters.Add($"constraint_subcategories CONTAINS {subcategory}");
                confidence += 0.2;
            }
        }

        // Check for system filters (only when explicitly mentioned)
        if (systemMentioned)
        {
            foreach (var system in SystemKeywords)
            {
                if (queryLower.Contains(system))
                {
                    filters.Add($"systems CONTAINS {system}");
                    confidence += 0.15;
                }
            }
        }

        // Check for aggregation operations
        if (queryLower.Contains("count") || queryLower.Contains("total") || queryLower.Contains("sum"))
        {
            aggregations.Add("COUNT/SUM");
            confidence += 0.2;
        }

        if (queryLower.Contains("group") || queryLower.Contains("by"))
        {
            aggregations.Add("GROUP BY");
            confidence += 0.15;
        }

        // Only return SQL route if we have reasonable confidence
        if (confidence >= 0.5)
        {
            return new QueryRoutingResult
            {
                Route = RouteType.SQL,
                Confidence = Math.Min(confidence, 0.95),
                Reasoning = systemMentioned 
                    ? "Query involves constraint type with system filtering"
                    : "Query involves requirement type classification or subcategory filtering",
                SqlIntent = new SqlIntent
                {
                    Filters = filters,
                    Joins = joins,
                    Aggregations = aggregations
                }
            };
        }

        return null;
    }
}
