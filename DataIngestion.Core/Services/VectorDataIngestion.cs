using DataIngestion.Core.Models;
using Microsoft.Extensions.Logging;
using QueryRouter.Data.Vector;

namespace DataIngestion.Core.Services;

public class VectorDataIngestion
{
    private readonly ILogger<VectorDataIngestion> _logger;
    private readonly AzureOpenAIEmbeddings _embeddings;
    private readonly QdrantVectorStore _vectorStore;

    public VectorDataIngestion(
        ILogger<VectorDataIngestion> logger,
        AzureOpenAIEmbeddings embeddings,
        QdrantVectorStore vectorStore)
    {
        _logger = logger;
        _embeddings = embeddings;
        _vectorStore = vectorStore;
    }

    public async Task<bool> IngestAsync(ProposalData proposalData)
    {
        try
        {
            _logger.LogInformation("Starting vector ingestion of {Count} requirements", proposalData.Requirements.Count);

            // Initialize collection
            var initialized = await _vectorStore.InitializeCollectionAsync();
            if (!initialized)
            {
                _logger.LogError("Failed to initialize Qdrant collection");
                return false;
            }

            // Prepare texts for embedding
            var texts = proposalData.Requirements
                .Select(r => r.NormalizedText)
                .ToList();

            // Generate embeddings in batches
            _logger.LogInformation("Generating embeddings for {Count} requirements", texts.Count);
            var embeddings = await _embeddings.GenerateBatchEmbeddingsAsync(texts, batchSize: 50);

            if (embeddings.Count != proposalData.Requirements.Count)
            {
                _logger.LogError("Embedding count mismatch: expected {Expected}, got {Actual}", 
                    proposalData.Requirements.Count, embeddings.Count);
                return false;
            }

            // Create vector points
            var points = new List<VectorPoint>();
            for (int i = 0; i < proposalData.Requirements.Count; i++)
            {
                var req = proposalData.Requirements[i];
                points.Add(new VectorPoint
                {
                    Id = Guid.NewGuid(),
                    Vector = embeddings[i],
                    ClientReferenceId = req.ClientReferenceId,
                    NormalizedText = req.NormalizedText,
                    RawText = req.RawText,
                    ConstraintType = req.Constraint.Type,
                    RequirementType = req.Classification.RequirementType,
                    Criticality = req.Classification.Criticality
                });
            }

            // Upsert to Qdrant
            var success = await _vectorStore.UpsertPointsAsync(points);
            
            if (success)
            {
                var count = await _vectorStore.GetPointCountAsync();
                _logger.LogInformation("Successfully ingested {Count} vectors into Qdrant", count);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during vector ingestion");
            return false;
        }
    }
}
