namespace QueryRouter.Core.Models;

/// <summary>
/// Represents the type of database route for query execution
/// </summary>
public enum RouteType
{
    SQL,
    VECTOR,
    GRAPH,
    HYBRID
}
