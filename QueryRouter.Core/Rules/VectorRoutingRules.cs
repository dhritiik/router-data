using QueryRouter.Core.Models;

namespace QueryRouter.Core.Rules;

/// <summary>
/// Rules for routing queries to Vector database for semantic similarity
/// </summary>
public class VectorRoutingRules
{
    private static readonly string[] SemanticKeywords = new[]
    {
        "similar", "like", "related to", "similar to", "find requirements similar",
        "show requirements similar", "group", "paraphrase", "conceptually",
        "fuzzy", "meaning", "semantically"
    };

    private static readonly Dictionary<string, string[]> SemanticConcepts = new()
    {
        ["offline"] = new[] { "offline", "sync", "synchronization", "local", "queue", "deferred" },
        ["audit"] = new[] { "audit", "log", "logging", "traceability", "tamper", "activity" },
        ["payment_security"] = new[] { "pan", "cvv", "cardholder", "card data", "mask", "sensitive payment" },
        ["age_verification"] = new[] { "age", "verification", "regulated products", "age check" },
        ["transaction_speed"] = new[] { "fast", "transaction", "processing", "performance", "speed" },
        ["ui_customization"] = new[] { "ui", "customization", "interface", "display", "screen" }
    };

    public QueryRoutingResult? Analyze(string query)
    {
        double confidence = 0.0;
        string semanticConcept = string.Empty;
        int topK = 10;

        // Check for semantic keywords
        var semanticKeywordMatches = SemanticKeywords.Count(kw => query.Contains(kw));
        if (semanticKeywordMatches > 0)
        {
            confidence += 0.4 * semanticKeywordMatches;
        }
        else
        {
            // If no explicit semantic keywords, not a vector query
            return null;
        }

        // Identify the semantic concept being searched
        foreach (var concept in SemanticConcepts)
        {
            var matches = concept.Value.Count(term => query.Contains(term));
            if (matches > 0)
            {
                semanticConcept = concept.Key;
                confidence += 0.3;
                break;
            }
        }

        // Extract the actual requirement text to search for
        if (string.IsNullOrEmpty(semanticConcept))
        {
            // Try to extract the concept from the query
            var afterSimilarTo = ExtractAfterKeyword(query, "similar to");
            if (!string.IsNullOrEmpty(afterSimilarTo))
            {
                semanticConcept = afterSimilarTo;
                confidence += 0.2;
            }
        }

        // Check for grouping operations (also semantic)
        if (query.Contains("group") && query.Contains("similar"))
        {
            topK = 20; // More results for grouping
            confidence += 0.15;
        }

        // Only return vector route if we have good confidence
        if (confidence >= 0.6)
        {
            return new QueryRoutingResult
            {
                Route = RouteType.VECTOR,
                Confidence = Math.Min(confidence, 0.95),
                Reasoning = "Query requires semantic similarity matching on requirement text",
                VectorIntent = new VectorIntent
                {
                    SemanticConcept = semanticConcept,
                    TopK = topK
                }
            };
        }

        return null;
    }

    private string ExtractAfterKeyword(string query, string keyword)
    {
        var index = query.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var afterKeyword = query.Substring(index + keyword.Length).Trim();
            // Take up to the next clause or end
            var endIndex = afterKeyword.IndexOfAny(new[] { ',', ';', '.' });
            if (endIndex > 0)
            {
                afterKeyword = afterKeyword.Substring(0, endIndex);
            }
            return afterKeyword.Trim();
        }
        return string.Empty;
    }
}
