using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataIngestion.Core.Models;

public class ProposalData
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("proposal_id")]
    public string? ProposalId { get; set; }

    [JsonPropertyName("rfp_id")]
    public string RfpId { get; set; } = string.Empty;

    [JsonPropertyName("customer_id")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("sf_id")]
    public string SfId { get; set; } = string.Empty;

    [JsonPropertyName("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("industry")]
    public string Industry { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("product_id")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("version_id")]
    public string VersionId { get; set; } = string.Empty;

    [JsonPropertyName("version_name")]
    public string? VersionName { get; set; }

    [JsonPropertyName("received_date")]
    public string ReceivedDate { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("requirements")]
    public List<Requirement> Requirements { get; set; } = new();
}

public class Requirement
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("client_reference_id")]
    public string ClientReferenceId { get; set; } = string.Empty;

    [JsonPropertyName("raw_text")]
    public string RawText { get; set; } = string.Empty;

    [JsonPropertyName("normalized_text")]
    public string NormalizedText { get; set; } = string.Empty;

    [JsonPropertyName("confidence_score")]
    public double ConfidenceScore { get; set; }

    [JsonPropertyName("requirement_version")]
    public string RequirementVersion { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public RequirementAction Action { get; set; } = new();

    [JsonPropertyName("object")]
    public RequirementObject Object { get; set; } = new();

    [JsonPropertyName("constraint")]
    public RequirementConstraint Constraint { get; set; } = new();

    [JsonPropertyName("metrics")]
    public RequirementMetrics Metrics { get; set; } = new();

    [JsonPropertyName("classification")]
    public RequirementClassification Classification { get; set; } = new();

    [JsonPropertyName("entities")]
    public RequirementEntities Entities { get; set; } = new();

    [JsonPropertyName("provenance")]
    public RequirementProvenance Provenance { get; set; } = new();

    [JsonPropertyName("relationships")]
    public RequirementRelationships Relationships { get; set; } = new();

    [JsonPropertyName("review")]
    public RequirementReview Review { get; set; } = new();

    [JsonPropertyName("estimation")]
    public RequirementEstimation Estimation { get; set; } = new();
}

public class RequirementAction
{
    [JsonPropertyName("verb")]
    public string Verb { get; set; } = string.Empty;

    [JsonPropertyName("modality")]
    public string Modality { get; set; } = string.Empty;
}

public class RequirementObject
{
    [JsonPropertyName("primary")]
    public string Primary { get; set; } = string.Empty;

    [JsonPropertyName("secondary")]
    public List<string> Secondary { get; set; } = new();
}

public class RequirementConstraint
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("subcategories")]
    public List<string> Subcategories { get; set; } = new();
}

public class RequirementMetrics
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(StringOrNumberConverter))]
    public string? Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}

// Custom converter to handle value as either string or number
public class StringOrNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }
        
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble().ToString();
        }
        
        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

public class RequirementClassification
{
    [JsonPropertyName("requirement_type")]
    public string RequirementType { get; set; } = string.Empty;

    [JsonPropertyName("criticality")]
    public string Criticality { get; set; } = string.Empty;

    [JsonPropertyName("fit_gap")]
    public string? FitGap { get; set; }

    [JsonPropertyName("mvp")]
    public string? Mvp { get; set; }

    [JsonPropertyName("phase")]
    public string? Phase { get; set; }
}

public class RequirementEntities
{
    [JsonPropertyName("systems")]
    public List<string> Systems { get; set; } = new();

    [JsonPropertyName("standards")]
    public List<string> Standards { get; set; } = new();

    [JsonPropertyName("regulations")]
    public List<string> Regulations { get; set; } = new();

    [JsonPropertyName("regions")]
    public List<string> Regions { get; set; } = new();

    [JsonPropertyName("dates")]
    public List<string> Dates { get; set; } = new();

    [JsonPropertyName("monetary_values")]
    public MonetaryValue MonetaryValues { get; set; } = new();
}

public class MonetaryValue
{
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }
}

public class RequirementProvenance
{
    [JsonPropertyName("document_id")]
    public string? DocumentId { get; set; }

    [JsonPropertyName("document_name")]
    public string? DocumentName { get; set; }

    [JsonPropertyName("section")]
    public string? Section { get; set; }

    [JsonPropertyName("page_number")]
    public int? PageNumber { get; set; }

    [JsonPropertyName("text_span")]
    public TextSpan TextSpan { get; set; } = new();

    [JsonPropertyName("extracted_by")]
    public ExtractedBy ExtractedBy { get; set; } = new();
}

public class TextSpan
{
    [JsonPropertyName("start")]
    public int? Start { get; set; }

    [JsonPropertyName("end")]
    public int? End { get; set; }
}

public class ExtractedBy
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("prompt_version")]
    public string? PromptVersion { get; set; }
}

public class RequirementRelationships
{
    [JsonPropertyName("depends_on")]
    public List<string>? DependsOn { get; set; }

    [JsonPropertyName("contradicts")]
    public List<string>? Contradicts { get; set; }

    [JsonPropertyName("implies")]
    public List<string>? Implies { get; set; }

    [JsonPropertyName("similar_to")]
    public List<string>? SimilarTo { get; set; }

    [JsonPropertyName("duplicate_of")]
    public string? DuplicateOf { get; set; }

    [JsonPropertyName("belongs_to_cluster")]
    public string? BelongsToCluster { get; set; }
}

public class RequirementReview
{
    [JsonPropertyName("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [JsonPropertyName("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [JsonPropertyName("dn_interpretation")]
    public string? DnInterpretation { get; set; }

    [JsonPropertyName("dn_assumptions")]
    public string? DnAssumptions { get; set; }

    [JsonPropertyName("comments")]
    public string? Comments { get; set; }

    [JsonPropertyName("changes")]
    public List<string> Changes { get; set; } = new();
}

public class RequirementEstimation
{
    [JsonPropertyName("t_shirt_size")]
    public string? TShirtSize { get; set; }

    [JsonPropertyName("complexity")]
    public string? Complexity { get; set; }

    [JsonPropertyName("risk_level")]
    public string? RiskLevel { get; set; }
}
