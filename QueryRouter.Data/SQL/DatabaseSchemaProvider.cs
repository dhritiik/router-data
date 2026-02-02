namespace QueryRouter.Data.SQL;

/// <summary>
/// Provides database schema information for LLM-based SQL generation
/// </summary>
public class DatabaseSchemaProvider
{
    public string GetSchemaDescription()
    {
        return @"
Table: Requirements

Columns:
- Id (INTEGER, PRIMARY KEY) - Auto-incrementing unique identifier
- ClientReferenceId (TEXT) - External reference ID (e.g., 'REQ-001')
- NormalizedText (TEXT) - Cleaned requirement text
- RawText (TEXT) - Original requirement text
- RequirementType (TEXT) - Type of requirement
  Values: 'functional', 'non-functional', 'security', 'performance', 'usability', 'reliability'
- Criticality (TEXT) - Importance level
  Values: 'high', 'medium', 'low'
- ConstraintType (TEXT) - Type of constraint
  Values: 'functional', 'security', 'performance', 'usability', 'reliability', 'compliance'
- ConstraintSubcategories (TEXT) - JSON array of subcategories
  Example: '[""authentication"", ""authorization""]'
- Systems (TEXT) - JSON array of related systems
  Values: 'POS', 'backend', 'frontend', 'payment gateway', 'database', 'API'
  Example: '[""POS"", ""backend""]'
- Standards (TEXT) - JSON array of standards
  Example: '[""ISO 27001"", ""PCI-DSS""]'
- Regulations (TEXT) - JSON array of regulations
  Example: '[""GDPR"", ""CCPA""]'

Important Notes:
1. Systems, Standards, Regulations, and ConstraintSubcategories are stored as JSON arrays
2. To query JSON arrays, use JSON_EACH function:
   SELECT * FROM Requirements, JSON_EACH(Requirements.Systems) WHERE JSON_EACH.value = 'POS'
3. For multiple JSON array filters, use multiple JSON_EACH joins
4. Text search should use LIKE with wildcards: NormalizedText LIKE '%keyword%'
5. Always use LIMIT to prevent large result sets (default: 50)

Common Query Patterns:
- Count by type: SELECT COUNT(*) FROM Requirements WHERE RequirementType = 'security'
- Filter by system: SELECT * FROM Requirements, JSON_EACH(Requirements.Systems) WHERE JSON_EACH.value = 'POS'
- Multiple filters: SELECT * FROM Requirements WHERE RequirementType = 'functional' AND Criticality = 'high'
- Text search: SELECT * FROM Requirements WHERE NormalizedText LIKE '%payment%'
";
    }

    public string GetTableNames()
    {
        return "Requirements";
    }

    public List<string> GetRequirementTypes()
    {
        return new List<string>
        {
            "functional",
            "non-functional",
            "security",
            "performance",
            "usability",
            "reliability"
        };
    }

    public List<string> GetCriticalityLevels()
    {
        return new List<string>
        {
            "high",
            "medium",
            "low"
        };
    }

    public List<string> GetSystemNames()
    {
        return new List<string>
        {
            "POS",
            "backend",
            "frontend",
            "payment gateway",
            "database",
            "API"
        };
    }
}
