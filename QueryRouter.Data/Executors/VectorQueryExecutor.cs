using Microsoft.Extensions.Logging;
using QueryRouter.Core.Models;
using QueryRouter.Data.Vector;

namespace QueryRouter.Data.Executors;

public class VectorQueryExecutor
{
    private readonly ILogger<VectorQueryExecutor> _logger;
    private readonly AzureOpenAIEmbeddings _embeddings;
    private readonly FaissVectorStore _vectorStore;
    private readonly BM25Scorer _bm25Scorer;
    
    private const int RRF_K = 60; // Reciprocal Rank Fusion constant

    public VectorQueryExecutor(
        ILogger<VectorQueryExecutor> logger,
        AzureOpenAIEmbeddings embeddings,
        FaissVectorStore vectorStore,
        BM25Scorer bm25Scorer)
    {
        _logger = logger;
        _embeddings = embeddings;
        _vectorStore = vectorStore;
        _bm25Scorer = bm25Scorer;
    }

    public async Task<List<RequirementResult>> ExecuteAsync(VectorIntent vectorIntent)
    {
        try
        {
            _logger.LogInformation("Executing hybrid vector search for concept: {Concept}", vectorIntent.SemanticConcept);

            // Generate embedding for the search query
            var queryEmbedding = await _embeddings.GenerateEmbeddingAsync(vectorIntent.SemanticConcept);
            
            if (queryEmbedding == null)
            {
                _logger.LogError("Failed to generate query embedding");
                return new List<RequirementResult>();
            }

            // Vector search using FAISS
            var vectorResults = await _vectorStore.SearchAsync(queryEmbedding, topK: vectorIntent.TopK * 2, threshold: 0.3f);
            _logger.LogInformation("Vector search returned {Count} results", vectorResults.Count);

            // BM25 keyword search
            var bm25Results = _bm25Scorer.Search(vectorIntent.SemanticConcept, topK: vectorIntent.TopK * 2);
            _logger.LogInformation("BM25 search returned {Count} results", bm25Results.Count);

            // Reciprocal Rank Fusion (RRF) to combine results
            var rrfScores = ReciprocRankFusion(
                vectorResults.Select(r => (r.Id, r.Score)).ToList(),
                bm25Results.Select(r => (r.docId, (float)r.score)).ToList()
            );

            // Get top K results after fusion
            var topResults = rrfScores
                .OrderByDescending(x => x.Value)
                .Take(vectorIntent.TopK)
                .ToList();

            _logger.LogInformation("RRF fusion returned {Count} final results", topResults.Count);

            // Convert to RequirementResult
            var results = new List<RequirementResult>();
            foreach (var (id, score) in topResults)
            {
                var vectorResult = vectorResults.FirstOrDefault(r => r.Id == id);
                if (vectorResult != null)
                {
                    results.Add(new RequirementResult
                    {
                        ClientReferenceId = vectorResult.Metadata.ClientReferenceId,
                        NormalizedText = vectorResult.Metadata.NormalizedText,
                        RawText = vectorResult.Metadata.RawText,
                        ConstraintType = vectorResult.Metadata.ConstraintType,
                        RequirementType = vectorResult.Metadata.RequirementType,
                        Criticality = vectorResult.Metadata.Criticality,
                        Score = score,
                        Source = "VECTOR"
                    });
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing vector query");
            return new List<RequirementResult>();
        }
    }

    /// <summary>
    /// Reciprocal Rank Fusion (RRF) to combine vector and BM25 results
    /// Formula: RRF_score(d) = Σ 1 / (k + rank(d))
    /// </summary>
    private Dictionary<string, float> ReciprocRankFusion(
        List<(string id, float score)> vectorResults,
        List<(string id, float score)> bm25Results)
    {
        var rrfScores = new Dictionary<string, float>();

        // Add vector search scores
        for (int rank = 0; rank < vectorResults.Count; rank++)
        {
            var id = vectorResults[rank].id;
            var rrfScore = 1.0f / (RRF_K + rank + 1);
            rrfScores[id] = rrfScores.GetValueOrDefault(id, 0) + rrfScore;
        }

        // Add BM25 scores
        for (int rank = 0; rank < bm25Results.Count; rank++)
        {
            var id = bm25Results[rank].id;
            var rrfScore = 1.0f / (RRF_K + rank + 1);
            rrfScores[id] = rrfScores.GetValueOrDefault(id, 0) + rrfScore;
        }

        return rrfScores;
    }
}
