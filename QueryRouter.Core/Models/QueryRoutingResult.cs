using System.Text.Json.Serialization;

namespace QueryRouter.Core.Models;

/// <summary>
/// Represents the result of query routing analysis
/// </summary>
public class QueryRoutingResult
{
    [JsonPropertyName("route")]
    public RouteType Route { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;

    [JsonPropertyName("sqlIntent")]
    public SqlIntent? SqlIntent { get; set; }

    [JsonPropertyName("vectorIntent")]
    public VectorIntent? VectorIntent { get; set; }

    [JsonPropertyName("graphIntent")]
    public GraphIntent? GraphIntent { get; set; }
}
