namespace QueryRouter.Core.Models;

/// <summary>
/// Represents a requirement result from query execution
/// </summary>
public class RequirementResult
{
    public string ClientReferenceId { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string ConstraintType { get; set; } = string.Empty;
    public List<string> ConstraintSubcategories { get; set; } = new();
    public string RequirementType { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public List<string> Systems { get; set; } = new();
    public List<string> Regulations { get; set; } = new();
    public double Score { get; set; }
    public string Source { get; set; } = "SQL";
}
