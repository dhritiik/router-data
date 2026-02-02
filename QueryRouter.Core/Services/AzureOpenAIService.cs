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

    public AzureOpenAIService(ILogger<AzureOpenAIService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
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
    public async Task<QueryRoutingResponse> AnalyzeQueryIntentAsync(string query)
    {
        try
        {
            var systemPrompt = @"You are a query router for a multi-database system with:

1. **SQL Database (SQLite)** - Use for:
   - Counting, aggregations (COUNT, SUM, AVG)
   - Filtering by specific attributes (requirement_type, criticality, constraint_type, systems)
   - Exact matches and structured queries
   - Examples: ""How many security requirements?"", ""List all high criticality requirements in POS""

2. **Vector Database (FAISS)** - Use for:
   - Semantic similarity searches
   - Finding similar requirements
   - Conceptual queries without exact keywords
   - Examples: ""Find requirements similar to payment processing"", ""Requirements about offline transactions""

3. **Graph Database (Neo4j)** - Use for:
   - Relationship traversals
   - Connected entities (systems, standards, regulations)
   - Multi-hop queries
   - Examples: ""What systems are connected to PCI-DSS?"", ""Show all requirements related to GDPR and their systems""

Analyze the query and respond with JSON in this exact format:
{
  ""route"": ""SQL"" | ""VECTOR"" | ""GRAPH"" | ""HYBRID"",
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""brief explanation"",
  ""sqlIntent"": {
    ""filters"": [""field=value""],
    ""aggregations"": [""COUNT""],
    ""requiresJoin"": false
  } or null,
  ""vectorIntent"": {
    ""semanticConcept"": ""main concept"",
    ""topK"": 10
  } or null,
  ""graphIntent"": {
    ""startNode"": ""node type"",
    ""relationship"": ""relationship type"",
    ""targetNode"": ""node type""
  } or null
}

Only return valid JSON, no other text.";

            var userPrompt = $"Query: {query}";

            var chatClient = _client.GetChatClient(_chatDeployment);
            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt)
                },
                new ChatCompletionOptions
                {
                    Temperature = 0.1f,
                    MaxOutputTokenCount = 500
                }
            );
            
            var content = response.Value.Content[0].Text;

            _logger.LogInformation("LLM routing response: {Response}", content);

            var routingResponse = JsonSerializer.Deserialize<QueryRoutingResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return routingResponse ?? throw new InvalidOperationException("Failed to parse LLM response");
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
    public async Task<string> GenerateSqlQueryAsync(string query, string schemaDescription)
    {
        try
        {
            var systemPrompt = $@"You are a SQL expert. Generate SQLite queries based on natural language requests.

Database Schema:
{schemaDescription}

Rules:
1. Use proper SQLite syntax
2. For JSON array columns (Systems, Standards, Regulations, ConstraintSubcategories), use JSON_EACH:
   Example: SELECT * FROM Requirements, JSON_EACH(Requirements.Systems) WHERE JSON_EACH.value = 'POS'
3. Column names are case-sensitive
4. Use LIKE for partial text matching with wildcards: LIKE '%text%'
5. Always use LIMIT for safety (default 50)
6. For aggregations, return COUNT(*) or other aggregate functions
7. Return ONLY the SQL query, no explanations or markdown

Examples:
Query: ""How many security requirements?""
SQL: SELECT COUNT(*) FROM Requirements WHERE RequirementType = 'security'

Query: ""Show high criticality requirements in POS system""
SQL: SELECT * FROM Requirements, JSON_EACH(Requirements.Systems) WHERE Criticality = 'high' AND JSON_EACH.value = 'POS' LIMIT 50

Query: ""Count functional requirements with performance constraints""
SQL: SELECT COUNT(*) FROM Requirements WHERE RequirementType = 'functional' AND ConstraintType = 'performance'";

            var userPrompt = $"Generate SQL for: {query}";

            var chatClient = _client.GetChatClient(_chatDeployment);
            var response = await chatClient.CompleteChatAsync(
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

            var sqlQuery = response.Value.Content[0].Text.Trim();

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
    public async Task<string> ExtractSemanticConceptAsync(string query)
    {
        try
        {
            var systemPrompt = @"Extract the main semantic concept from the query for vector similarity search.
Return only the key concept phrase, no explanations.

Examples:
Query: ""Find requirements similar to offline transaction handling""
Concept: ""offline transaction handling""

Query: ""What are the security requirements for payment processing?""
Concept: ""payment processing security""

Query: ""Show me requirements about user authentication""
Concept: ""user authentication""";

            var userPrompt = $"Query: {query}";

            var chatClient = _client.GetChatClient(_chatDeployment);
            var response = await chatClient.CompleteChatAsync(
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

            var concept = response.Value.Content[0].Text.Trim().Trim('"');

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
