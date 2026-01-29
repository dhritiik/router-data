namespace QueryRouter.Core.Models;

/// <summary>
/// Represents the intent for Vector database operations
/// </summary>
public class VectorIntent
{
    public string SemanticConcept { get; set; } = string.Empty;
    public int TopK { get; set; } = 10;
}
