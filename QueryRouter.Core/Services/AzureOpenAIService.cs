using System.Text.Json;
using Azure;
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;

namespace QueryRouter.Core.Services;

/// <summary>
/// Service for interacting with Azure OpenAI for LLM-based query analysis and SQL generation
/// </summary>
public class AzureOpenAIService
{
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly AzureOpenAIClient _client;
    private readonly string _chatDeployment;
    private readonly LangfuseService? _langfuse;

    public AzureOpenAIService(ILogger<AzureOpenAIService> logger, IConfiguration configuration, LangfuseService? langfuse = null)
    {
        _logger = logger;
        _langfuse = langfuse;
        
        var endpoint = configuration["AZURE_OPENAI_ENDPOINT"] 
            ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT not configured");
        var apiKey = configuration["AZURE_OPENAI_API_KEY"] 
            ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY not configured");
        _chatDeployment = configuration["AZURE_OPENAI_GPT_DEPLOYMENT"] ?? "gpt-4";
        
        _client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        
        _logger.LogInformation("Azure OpenAI Service initialized with deployment: {Deployment}", _chatDeployment);
    }

    /// <summary>
    /// Analyze query intent using LLM to determine routing
    /// </summary>
    public async Task<QueryRoutingResponse> AnalyzeQueryIntentAsync(string query, string? traceId = null, string? spanId = null)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            var systemPrompt = """
### SYSTEM ROLE
You are an intelligent Database Query Router and Intent Extractor. Your goal is to analyze natural language user queries and determine the optimal database(s) to retrieve the answer. You have access to three specific data stores: SQL (Structured), Vector (Semantic), and Graph (Relational).

### DATABASE CONTEXT & SCHEMA
You must verify if the query targets specific columns, relationships, or semantic concepts based on the schemas defined below.

1. **SQL Database (Structured Data)**
   * **Use for:** Exact filtering, aggregations (COUNT, SUM), sorting, and queries referencing specific attributes found in the requirements JSON.
   * **Schema:**
   - **Properties**: Id (int), ClientReferenceId (string), RequirementType (string: functional, non_functional, security, compliance, operational, integration, data), Criticality (string: low, medium, high, critical, mandatory), Status (string), CreatedBy (string), CreatedDate (datetime), UpdatedBy (string), UpdatedDate (datetime)
   - **Relationships**: A requirement can belong to multiple Systems, Regulations, and Modules.

2. **Vector Database (Semantic Search)**
   * **Use for:** Vague/abstract queries, similarity searches (e.g., "similar to X"), and conceptual matching where keywords might not be exact.
   * **Content:** Stores embedding vectors of the `RawText` and `NormalizedText` of each requirement.

3. **Graph Database (Relational/Network)**
   * **Use for:** Complex dependency queries, impact analysis (e.g., "what happens if I change X"), and exploring relationships between Requirements, Systems, and Regulations.
   * **Nodes:** Requirement, System, Regulation, User, Module
   * **Edges:** DEPENDS_ON, RELATES_TO, IMPACTS, GOVERNED_BY

### ROUTING RULES
Analyze the user query and output a JSON object with the following fields:

- **route**: One of "SQL", "VECTOR", "GRAPH", or "HYBRID" (if multiple needed).
  - Default to **SQL** for filtering/aggregations.
  - Default to **VECTOR** for semantic/similarity searches.
  - Default to **GRAPH** for dependencies/lineage.

- **confidence**: A float between 0.0 and 1.0 indicating certainty.

- **reasoning**: A brief explanation of why this route was chosen.

- **sqlIntent**: (If route is SQL)
  - **tables**: List of tables involved.
  - **filters**: List of conditions (e.g., "Criticality = 'high'").
  - **aggregation**: Any COUNT/SUM/AVG needed.

- **vectorIntent**: (If route is VECTOR)
  - **semanticConcept**: The core concept to search for (e.g., "offline transaction processing").
  - **topK**: Recommended number of results (default 10).

- **graphIntent**: (If route is GRAPH)
  - **startNode**: The entry point for the traversal.
  - **traversalDepth**: How many hops to go (1-3).
  - **relationshipTypes**: List of edge types to follow (e.g., ["DEPENDS_ON"]).

### OUTPUT FORMAT
Return **only** valid JSON. Do not include markdown formatting.
""";

            var userPrompt = $"User Query: {query}";

            var chatClient = _client.GetChatClient(_chatDeployment);
            var completion = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                },
                new ChatCompletionOptions
                {
                    Temperature = 0.0f,
                    MaxOutputTokenCount = 500,
                    ResponseFormat = ChatResponseFormat.JsonObject
                }
            );
            
            var jsonResponse = completion.Value.Content[0].Text;
            var endTime = DateTime.UtcNow;

            // Log to Langfuse
            if (_langfuse != null && !string.IsNullOrEmpty(traceId))
            {
                var usage = completion.Value.Usage;
                var tokenUsage = new TokenUsage 
                { 
                    PromptTokens = usage.InputTokenCount, 
                    CompletionTokens = usage.OutputTokenCount, 
                    TotalTokens = usage.TotalTokenCount 
                };

                _langfuse.CreateGeneration(
                    traceId, 
                    spanId, 
                    "AnalyzeQueryIntent", 
                    _chatDeployment, 
                    new { systemPrompt, userPrompt }, 
                    jsonResponse, 
                    tokenUsage,
                    startTime,
                    endTime
                );
            }

            _logger.LogInformation("LLM routing response: {Response}", jsonResponse);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            return JsonSerializer.Deserialize<QueryRoutingResponse>(jsonResponse, options) 
                   ?? new QueryRoutingResponse { Route = "VECTOR", Confidence = 0.5, Reasoning = "Failed to parse JSON" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query intent with LLM");
            throw;
        }
    }

    /// <summary>
    /// Generate SQL query from natural language using LLM with schema knowledge
    /// </summary>
    public async Task<string> GenerateSqlQueryAsync(string query, string schemaDescription, string? traceId = null, string? spanId = null)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            var preciseSchema = @"
Table: Requirements
-------------------
Columns:
- ClientReferenceId (TEXT)      : Unique ID (e.g., 'POS-FR-001')
- NormalizedText (TEXT)         : The clean requirement text
- RequirementType (TEXT)        : Enum: 'functional', 'non_functional', 'security', 'compliance', 'integration'
- Criticality (TEXT)            : Enum: 'mandatory', 'high', 'medium', 'low'
- Status (TEXT)                 : Enum: 'draft', 'approved'
- ActionVerb (TEXT)             : e.g., 'support', 'integrate', 'enforce'
- ConstraintType (TEXT)         : e.g., 'functional', 'performance', 'security'
- Subcategories (JSON)          : JSON Array from constraint.subcategories, e.g., ['Version', 'Payments', 'UI']
- Systems (JSON)                : JSON Array, e.g., ['POS', 'Backend']
- Regulations (JSON)            : JSON Array, e.g., ['PCI-DSS', 'PDPA']
- Metrics_Value (REAL)          : Numeric value for constraints (e.g., 99.9, 300)
- Metrics_Unit (TEXT)           : Unit for metrics (e.g., '%', 'ms')
";

        var systemPrompt = $@"You are a SQLite expert specialized in querying Requirement datasets. 

### DATABASE SCHEMA
{preciseSchema}

### RULES
1. **JSON Arrays:** To filter by `Systems`, `Regulations`, or **`Subcategories`**, use the `EXISTS` clause with `json_each`.
   - *Example:* `SELECT * FROM Requirements r WHERE EXISTS (SELECT 1 FROM json_each(r.Subcategories) WHERE value = 'Version')`
2. **Enum Values:** ALWAYS use lowercase for `Criticality` ('high', 'mandatory') and `RequirementType` ('security').
3. **Text Search:** Use `NormalizedText LIKE '%term%'` for text matching.
4. **Safety:** Always add `LIMIT 50` unless an aggregate count is requested.
5. **Output:** Return ONLY the raw SQL. No markdown formatting, no explanations.

### EXAMPLES

Input: ""How many mandatory security requirements are there?""
SQL: SELECT COUNT(*) FROM Requirements WHERE Criticality = 'mandatory' AND RequirementType = 'security'

Input: ""Show me all Version requirements""
SQL: SELECT * FROM Requirements r WHERE EXISTS (SELECT 1 FROM json_each(r.Subcategories) WHERE value = 'Version') LIMIT 50

Input: ""Find performance requirements with latency less than 500ms""
SQL: SELECT * FROM Requirements WHERE ConstraintType = 'performance' AND Metrics_Value < 500 AND Metrics_Unit = 'ms' LIMIT 50

Input: ""List requirements related to PCI-DSS regulation""
SQL: SELECT * FROM Requirements r WHERE EXISTS (SELECT 1 FROM json_each(r.Regulations) WHERE value = 'PCI-DSS') LIMIT 50
";

        var userPrompt = $"Generate SQL for: {query}";

            var chatClient = _client.GetChatClient(_chatDeployment);
            var completion = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                },
                new ChatCompletionOptions
                {
                    Temperature = 0.0f,
                    MaxOutputTokenCount = 300
                }
            );

            var sqlQuery = completion.Value.Content[0].Text.Trim();
            var endTime = DateTime.UtcNow;

            // Log to Langfuse
            if (_langfuse != null && !string.IsNullOrEmpty(traceId))
            {
                var usage = completion.Value.Usage;
                var tokenUsage = new TokenUsage 
                { 
                    PromptTokens = usage.InputTokenCount, 
                    CompletionTokens = usage.OutputTokenCount, 
                    TotalTokens = usage.TotalTokenCount 
                };

                _langfuse.CreateGeneration(
                    traceId, 
                    spanId, 
                    "GenerateSqlQuery", 
                    _chatDeployment, 
                    new { systemPrompt, userPrompt }, 
                    sqlQuery, 
                    tokenUsage,
                    startTime,
                    endTime
                );
            }

            // Remove markdown code blocks if present
            sqlQuery = sqlQuery.Replace("```sql", "").Replace("```", "").Trim();

            _logger.LogInformation("Generated SQL: {SQL}", sqlQuery);

            return sqlQuery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SQL query with LLM");
            throw;
        }
    }

    /// <summary>
    /// Extract semantic concept from query for vector search
    /// </summary>
    public async Task<string> ExtractSemanticConceptAsync(string query, string? traceId = null, string? spanId = null)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            var systemPrompt = @"You are a Search Intent Optimizer for a technical requirements database.
Your goal is to convert a user's natural language query into a clean, keyword-dense search string optimized for vector embedding retrieval.

### RULES
1. **Remove conversational noise:** Strip words like ""find"", ""show me"", ""list"", ""get"", ""requirements for"", ""what are"".
2. **Preserve technical entities:** Keep specific terms like ""POS"", ""PCI-DSS"", ""latency"", ""Version"", ""2FA"".
3. **Focus on the 'What':** Isolate the core object, the action, or the category (e.g. Version).
4. **Output:** Return ONLY the optimized string. No quotes, no explanations.

### EXAMPLES

User Query: ""Find requirements similar to offline transaction handling""
Optimized: offline transaction processing synchronization

User Query: ""Show me all version related requirements""
Optimized: Version release management

User Query: ""What are the security requirements for payment processing?""
Optimized: payment processing security data protection

User Query: ""Show me requirements about user authentication""
Optimized: user authentication login identity management

User Query: ""Is there anything about preventing age restricted sales?""
Optimized: age restricted sales prevention compliance";

            var userPrompt = $"Query: {query}";

            var chatClient = _client.GetChatClient(_chatDeployment);
            var completion = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                },
                new ChatCompletionOptions
                {
                    Temperature = 0.0f,
                    MaxOutputTokenCount = 50
                }
            );

            var concept = completion.Value.Content[0].Text.Trim().Trim('"');
            var endTime = DateTime.UtcNow;

            // Log to Langfuse
            if (_langfuse != null && !string.IsNullOrEmpty(traceId))
            {
                var usage = completion.Value.Usage;
                var tokenUsage = new TokenUsage 
                { 
                    PromptTokens = usage.InputTokenCount, 
                    CompletionTokens = usage.OutputTokenCount, 
                    TotalTokens = usage.TotalTokenCount 
                };

                _langfuse.CreateGeneration(
                    traceId, 
                    spanId, 
                    "ExtractSemanticConcept", 
                    _chatDeployment, 
                    new { systemPrompt, userPrompt }, 
                    concept, 
                    tokenUsage,
                    startTime,
                    endTime
                );
            }

            _logger.LogInformation("Extracted semantic concept: {Concept}", concept);

            return concept;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting semantic concept");
            return query; // Fallback to original query
        }
    }
}

/// <summary>
/// Response model for LLM query routing
/// </summary>
public class QueryRoutingResponse
{
    public string Route { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public LlmSqlIntent? SqlIntent { get; set; }
    public LlmVectorIntent? VectorIntent { get; set; }
    public LlmGraphIntent? GraphIntent { get; set; }
}

public class LlmSqlIntent
{
    public List<string> Filters { get; set; } = new();
    public List<string> Aggregations { get; set; } = new();
    public bool RequiresJoin { get; set; }
}

public class LlmVectorIntent
{
    public string SemanticConcept { get; set; } = string.Empty;
    public int TopK { get; set; } = 10;
}

public class LlmGraphIntent
{
    public string StartNode { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string TargetNode { get; set; } = string.Empty;
}