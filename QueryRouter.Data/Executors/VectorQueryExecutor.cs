using Microsoft.Extensions.Logging;
using QueryRouter.Core.Models;
using QueryRouter.Data.Vector;

namespace QueryRouter.Data.Executors;

public class VectorQueryExecutor
{
    private readonly ILogger<VectorQueryExecutor> _logger;
    private readonly AzureOpenAIEmbeddings _embeddings;
    private readonly QdrantVectorStore _vectorStore;

    public VectorQueryExecutor(
        ILogger<VectorQueryExecutor> logger,
        AzureOpenAIEmbeddings embeddings,
        QdrantVectorStore vectorStore)
    {
        _logger = logger;
        _embeddings = embeddings;
        _vectorStore = vectorStore;
    }

    public async Task<List<RequirementResult>> ExecuteAsync(VectorIntent vectorIntent)
    {
        try
        {
            _logger.LogInformation("Executing vector search for concept: {Concept}", vectorIntent.SemanticConcept);

            // Generate embedding for the search query
            var queryEmbedding = await _embeddings.GenerateEmbeddingAsync(vectorIntent.SemanticConcept);
            
            if (queryEmbedding == null)
            {
                _logger.LogError("Failed to generate query embedding");
                return new List<RequirementResult>();
            }

            // Search Qdrant
            var results = await _vectorStore.SearchAsync(queryEmbedding, vectorIntent.TopK);

            _logger.LogInformation("Vector search returned {Count} results", results.Count);

            return results.Select(r => new RequirementResult
            {
                ClientReferenceId = r.Payload["client_reference_id"].StringValue,
                NormalizedText = r.Payload["normalized_text"].StringValue,
                RawText = r.Payload["raw_text"].StringValue,
                ConstraintType = r.Payload["constraint_type"].StringValue,
                RequirementType = r.Payload["requirement_type"].StringValue,
                Criticality = r.Payload["criticality"].StringValue,
                Score = r.Score,
                Source = "VECTOR"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing vector query");
            return new List<RequirementResult>();
        }
    }
}
