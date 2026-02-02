using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace QueryRouter.Data.Vector;

/// <summary>
/// FAISS-like vector store using in-memory storage with file persistence.
/// Replaces Qdrant for local vector search without Docker dependency.
/// </summary>
public class FaissVectorStore
{
    private readonly ILogger<FaissVectorStore> _logger;
    private readonly string _indexDirectory;
    
    private float[][] _embeddings = Array.Empty<float[]>();
    private Dictionary<string, int> _idToIndex = new();
    private List<VectorPoint> _metadata = new();
    
    private const string EmbeddingsFile = "embeddings.bin";
    private const string MetadataFile = "metadata.json";
    
    public FaissVectorStore(ILogger<FaissVectorStore> logger)
    {
        _logger = logger;
        _indexDirectory = Path.Combine(Directory.GetCurrentDirectory(), "vector_indexes");
        Directory.CreateDirectory(_indexDirectory);
    }

    /// <summary>
    /// Initialize the vector store by loading existing indexes or creating new ones
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            var embeddingsPath = Path.Combine(_indexDirectory, EmbeddingsFile);
            var metadataPath = Path.Combine(_indexDirectory, MetadataFile);

            if (File.Exists(embeddingsPath) && File.Exists(metadataPath))
            {
                _logger.LogInformation("Loading existing vector indexes from {Directory}", _indexDirectory);
                
                // Load embeddings
                _embeddings = await LoadEmbeddingsAsync(embeddingsPath);
                
                // Load metadata
                var json = await File.ReadAllTextAsync(metadataPath);
                _metadata = JsonSerializer.Deserialize<List<VectorPoint>>(json) ?? new();
                
                // Build ID to index mapping
                _idToIndex = _metadata
                    .Select((point, index) => new { point.ClientReferenceId, Index = index })
                    .ToDictionary(x => x.ClientReferenceId, x => x.Index);
                
                _logger.LogInformation("Loaded {Count} vectors from disk", _embeddings.Length);
                return true;
            }
            
            _logger.LogInformation("No existing indexes found. Ready for ingestion.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FAISS vector store");
            return false;
        }
    }

    /// <summary>
    /// Upsert vector points into the store
    /// </summary>
    public async Task<bool> UpsertPointsAsync(List<VectorPoint> points)
    {
        try
        {
            _logger.LogInformation("Upserting {Count} vector points", points.Count);
            
            // Clear existing data
            _embeddings = new float[points.Count][];
            _metadata = new List<VectorPoint>(points);
            _idToIndex.Clear();
            
            // Build embeddings array and ID mapping
            for (int i = 0; i < points.Count; i++)
            {
                _embeddings[i] = points[i].Vector;
                _idToIndex[points[i].ClientReferenceId] = i;
            }
            
            // Normalize vectors for cosine similarity
            NormalizeVectors();
            
            // Save to disk
            await SaveIndexesAsync();
            
            _logger.LogInformation("Successfully upserted {Count} vectors", points.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert vector points");
            return false;
        }
    }

    /// <summary>
    /// Search for similar vectors using cosine similarity
    /// </summary>
    public Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, int topK = 10, float threshold = 0.3f)
    {
        try
        {
            if (_embeddings.Length == 0)
            {
                _logger.LogWarning("No vectors in store. Returning empty results.");
                return Task.FromResult(new List<VectorSearchResult>());
            }
            
            // Normalize query vector
            var normalizedQuery = NormalizeVector(queryVector);
            
            // Calculate cosine similarities
            var similarities = new List<(int index, float score)>();
            
            for (int i = 0; i < _embeddings.Length; i++)
            {
                var score = CosineSimilarity(normalizedQuery, _embeddings[i]);
                
                if (score >= threshold)
                {
                    similarities.Add((i, score));
                }
            }
            
            // Sort by score descending and take top K
            var topResults = similarities
                .OrderByDescending(x => x.score)
                .Take(topK)
                .Select(x => new VectorSearchResult
                {
                    Id = _metadata[x.index].ClientReferenceId,
                    Score = x.score,
                    Metadata = _metadata[x.index]
                })
                .ToList();
            
            _logger.LogInformation("Vector search returned {Count} results (threshold: {Threshold})", 
                topResults.Count, threshold);
            
            return Task.FromResult(topResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search failed");
            return Task.FromResult(new List<VectorSearchResult>());
        }
    }

    /// <summary>
    /// Get total number of vectors in the store
    /// </summary>
    public Task<int> GetPointCountAsync()
    {
        return Task.FromResult(_embeddings.Length);
    }

    /// <summary>
    /// Calculate cosine similarity between two normalized vectors
    /// </summary>
    private float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same dimension");
        
        float dotProduct = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
        }
        
        return dotProduct; // Already normalized, so just dot product
    }

    /// <summary>
    /// Normalize a vector to unit length (L2 normalization)
    /// </summary>
    private float[] NormalizeVector(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(x => x * x));
        
        if (magnitude < 1e-10)
            return vector;
        
        return vector.Select(x => (float)(x / magnitude)).ToArray();
    }

    /// <summary>
    /// Normalize all stored vectors
    /// </summary>
    private void NormalizeVectors()
    {
        for (int i = 0; i < _embeddings.Length; i++)
        {
            _embeddings[i] = NormalizeVector(_embeddings[i]);
        }
    }

    /// <summary>
    /// Save indexes to disk
    /// </summary>
    private async Task SaveIndexesAsync()
    {
        var embeddingsPath = Path.Combine(_indexDirectory, EmbeddingsFile);
        var metadataPath = Path.Combine(_indexDirectory, MetadataFile);
        
        // Save embeddings as binary
        await SaveEmbeddingsAsync(embeddingsPath, _embeddings);
        
        // Save metadata as JSON
        var json = JsonSerializer.Serialize(_metadata, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(metadataPath, json);
        
        _logger.LogInformation("Saved indexes to {Directory}", _indexDirectory);
    }

    /// <summary>
    /// Save embeddings to binary file
    /// </summary>
    private async Task SaveEmbeddingsAsync(string path, float[][] embeddings)
    {
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        await using var bw = new BinaryWriter(fs);
        
        // Write dimensions
        bw.Write(embeddings.Length);
        bw.Write(embeddings.Length > 0 ? embeddings[0].Length : 0);
        
        // Write vectors
        foreach (var embedding in embeddings)
        {
            foreach (var value in embedding)
            {
                bw.Write(value);
            }
        }
    }

    /// <summary>
    /// Load embeddings from binary file
    /// </summary>
    private async Task<float[][]> LoadEmbeddingsAsync(string path)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);
        
        // Read dimensions
        var count = br.ReadInt32();
        var dimension = br.ReadInt32();
        
        // Read vectors
        var embeddings = new float[count][];
        for (int i = 0; i < count; i++)
        {
            embeddings[i] = new float[dimension];
            for (int j = 0; j < dimension; j++)
            {
                embeddings[i][j] = br.ReadSingle();
            }
        }
        
        return embeddings;
    }
}

/// <summary>
/// Result from vector search
/// </summary>
public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public float Score { get; set; }
    public VectorPoint Metadata { get; set; } = new();
}
