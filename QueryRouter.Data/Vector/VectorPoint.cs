namespace QueryRouter.Data.Vector;

/// <summary>
/// Represents a vector point with metadata for storage and retrieval
/// </summary>
public class VectorPoint
{
    public Guid Id { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
    public string ClientReferenceId { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string ConstraintType { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
}
