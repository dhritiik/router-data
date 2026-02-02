using DataIngestion.Core.Models;
using Microsoft.Extensions.Logging;
using QueryRouter.Data.Vector;

namespace DataIngestion.Core.Services;

public class VectorDataIngestion
{
    private readonly ILogger<VectorDataIngestion> _logger;
    private readonly AzureOpenAIEmbeddings _embeddings;
    private readonly FaissVectorStore _vectorStore;
    private readonly BM25Scorer _bm25Scorer;

    public VectorDataIngestion(
        ILogger<VectorDataIngestion> logger,
        AzureOpenAIEmbeddings embeddings,
        FaissVectorStore vectorStore,
        BM25Scorer bm25Scorer)
    {
        _logger = logger;
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _bm25Scorer = bm25Scorer;
    }

    public async Task<bool> IngestAsync(ProposalData proposalData)
    {
        try
        {
            _logger.LogInformation("Starting vector ingestion of {Count} requirements", proposalData.Requirements.Count);

            // Initialize stores
            await _vectorStore.InitializeAsync();
            await _bm25Scorer.InitializeAsync();

            // Prepare texts for embedding and BM25
            var texts = proposalData.Requirements
                .Select(r => BuildSearchableText(r))
                .ToList();

            // Build BM25 index
            _logger.LogInformation("Building BM25 index for {Count} requirements", texts.Count);
            var bm25Docs = proposalData.Requirements
                .Select((r, i) => new { Id = r.ClientReferenceId, Text = texts[i] })
                .ToDictionary(x => x.Id, x => x.Text);
            
            await _bm25Scorer.BuildIndexAsync(bm25Docs);

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

            // Upsert to FAISS vector store
            var success = await _vectorStore.UpsertPointsAsync(points);
            
            if (success)
            {
                var count = await _vectorStore.GetPointCountAsync();
                _logger.LogInformation("Successfully ingested {Count} vectors into FAISS store", count);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during vector ingestion");
            return false;
        }
    }

    private string BuildSearchableText(Requirement req)
    {
        var parts = new List<string>
        {
            req.NormalizedText,
            req.RawText
        };

        if (!string.IsNullOrEmpty(req.Action.Verb))
        {
            parts.Add($"action: {req.Action.Verb} {req.Action.Modality}");
        }

        if (!string.IsNullOrEmpty(req.Classification.RequirementType))
        {
            parts.Add($"type: {req.Classification.RequirementType}");
        }

        if (!string.IsNullOrEmpty(req.Classification.Criticality))
        {
            parts.Add($"criticality: {req.Classification.Criticality}");
        }

        if (!string.IsNullOrEmpty(req.Constraint.Description))
        {
            parts.Add($"constraint: {req.Constraint.Description}");
        }

        foreach (var system in req.Entities.Systems)
        {
            if (!string.IsNullOrEmpty(system))
                parts.Add($"system: {system}");
        }

        foreach (var standard in req.Entities.Standards)
        {
            if (!string.IsNullOrEmpty(standard))
                parts.Add($"standard: {standard}");
        }

        foreach (var regulation in req.Entities.Regulations)
        {
            if (!string.IsNullOrEmpty(regulation))
                parts.Add($"regulation: {regulation}");
        }

        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLower();
    }
}

