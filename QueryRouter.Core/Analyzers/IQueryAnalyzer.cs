using QueryRouter.Core.Models;

namespace QueryRouter.Core.Analyzers;

/// <summary>
/// Interface for query analysis and routing
/// </summary>
public interface IQueryAnalyzer
{
    /// <summary>
    /// Analyzes a user query and determines the appropriate routing
    /// </summary>
    /// <param name="query">The user query to analyze</param>
    /// <returns>Query routing result with database selection and intent</returns>
    Task<QueryRoutingResult> AnalyzeAsync(string query);
}
