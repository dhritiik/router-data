namespace QueryRouter.Core.Models;

/// <summary>
/// Represents the intent for SQL database operations
/// </summary>
public class SqlIntent
{
    public List<string> Filters { get; set; } = new();
    public List<string> Joins { get; set; } = new();
    public List<string> Aggregations { get; set; } = new();
}
