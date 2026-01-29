namespace QueryRouter.Core.Models;

/// <summary>
/// Represents the intent for Graph database operations
/// </summary>
public class GraphIntent
{
    public string StartNode { get; set; } = string.Empty;
    public List<string> RelationshipTypes { get; set; } = new();
    public int Depth { get; set; } = 1;
}
