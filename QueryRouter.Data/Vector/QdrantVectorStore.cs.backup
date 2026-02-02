using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace QueryRouter.Data.Vector;

public class QdrantVectorStore
{
    private readonly ILogger<QdrantVectorStore> _logger;
    private readonly QdrantClient _client;
    private const string CollectionName = "pos_requirements";
    private const int VectorSize = 3072; // text-embedding-3-large dimension

    public QdrantVectorStore(ILogger<QdrantVectorStore> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        var host = configuration["QDRANT_HOST"] ?? "localhost";
        var port = int.Parse(configuration["QDRANT_PORT"] ?? "6334");
        
        _client = new QdrantClient(host, port);
        
        _logger.LogInformation("Qdrant client initialized at {Host}:{Port}", host, port);
    }

    public async Task<bool> InitializeCollectionAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Qdrant collection: {Collection}", CollectionName);

            // Check if collection exists
            var collections = await _client.ListCollectionsAsync();
            var exists = collections.Any(c => c == CollectionName);

            if (exists)
            {
                _logger.LogInformation("Collection {Collection} already exists, deleting...", CollectionName);
                await _client.DeleteCollectionAsync(CollectionName);
            }

            // Create collection
            await _client.CreateCollectionAsync(
                collectionName: CollectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)VectorSize,
                    Distance = Distance.Cosine
                }
            );

            _logger.LogInformation("Collection {Collection} created successfully", CollectionName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Qdrant collection");
            return false;
        }
    }

    public async Task<bool> UpsertPointsAsync(List<VectorPoint> points, int batchSize = 100)
    {
        try
        {
            _logger.LogInformation("Upserting {Count} points to Qdrant", points.Count);

            for (int i = 0; i < points.Count; i += batchSize)
            {
                var batch = points.Skip(i).Take(batchSize).ToList();
                
                var qdrantPoints = batch.Select(p => new PointStruct
                {
                    Id = new PointId { Uuid = p.Id.ToString() },
                    Vectors = p.Vector,
                    Payload =
                    {
                        ["client_reference_id"] = p.ClientReferenceId,
                        ["normalized_text"] = p.NormalizedText,
                        ["raw_text"] = p.RawText,
                        ["constraint_type"] = p.ConstraintType,
                        ["requirement_type"] = p.RequirementType,
                        ["criticality"] = p.Criticality
                    }
                }).ToList();

                await _client.UpsertAsync(CollectionName, qdrantPoints);
                
                _logger.LogInformation("Upserted batch {Current}/{Total}", 
                    Math.Min(i + batchSize, points.Count), 
                    points.Count);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting points to Qdrant");
            return false;
        }
    }

    public async Task<List<ScoredPoint>> SearchAsync(float[] queryVector, int limit = 10)
    {
        try
        {
            var results = await _client.SearchAsync(
                collectionName: CollectionName,
                vector: queryVector,
                limit: (ulong)limit
            );

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Qdrant");
            return new List<ScoredPoint>();
        }
    }

    public async Task<long> GetPointCountAsync()
    {
        try
        {
            var info = await _client.GetCollectionInfoAsync(CollectionName);
            return (long)info.PointsCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting point count");
            return 0;
        }
    }
}

public class VectorPoint
{
    public Guid Id { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
    public string ClientReferenceId { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string ConstraintType { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
}
