using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace QueryRouter.Data.Graph;

public class Neo4jGraphStore
{
    private readonly ILogger<Neo4jGraphStore> _logger;
    private readonly IDriver _driver;

    public Neo4jGraphStore(ILogger<Neo4jGraphStore> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        var uri = configuration["NEO4J_URI"] ?? "bolt://localhost:7687";
        var username = configuration["NEO4J_USERNAME"] ?? "neo4j";
        var password = configuration["NEO4J_PASSWORD"] ?? "password";
        
        // Configure driver with no encryption for local Docker instance
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password), config =>
        {
            config.WithEncryptionLevel(EncryptionLevel.None);
        });
        
        _logger.LogInformation("Neo4j driver initialized at {Uri}", uri);
    }

    public async Task<bool> ClearDatabaseAsync()
    {
        try
        {
            await using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync("MATCH (n) DETACH DELETE n");
            });
            
            _logger.LogInformation("Neo4j database cleared");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Neo4j database");
            return false;
        }
    }

    public async Task<bool> CreateRequirementNodeAsync(string clientRefId, string text, string constraintType, string requirementType)
    {
        try
        {
            await using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                var query = @"
                    CREATE (r:Requirement {
                        client_ref: $clientRef,
                        text: $text,
                        constraint_type: $constraintType,
                        requirement_type: $requirementType
                    })";
                
                await tx.RunAsync(query, new
                {
                    clientRef = clientRefId,
                    text,
                    constraintType,
                    requirementType
                });
            });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating requirement node");
            return false;
        }
    }

    public async Task<bool> CreateSystemNodeAsync(string systemName)
    {
        try
        {
            await using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                var query = "MERGE (s:System {name: $name})";
                await tx.RunAsync(query, new { name = systemName });
            });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating system node");
            return false;
        }
    }

    public async Task<bool> CreateRegulationNodeAsync(string regulationName)
    {
        try
        {
            await using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                var query = "MERGE (r:Regulation {name: $name})";
                await tx.RunAsync(query, new { name = regulationName });
            });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating regulation node");
            return false;
        }
    }

    public async Task<bool> CreateRelationshipAsync(string fromClientRef, string toNode, string relationshipType, string nodeType = "System")
    {
        try
        {
            await using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                var query = $@"
                    MATCH (r:Requirement {{client_ref: $fromRef}})
                    MATCH (n:{nodeType} {{name: $toNode}})
                    MERGE (r)-[:{relationshipType}]->(n)";
                
                await tx.RunAsync(query, new { fromRef = fromClientRef, toNode });
            });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relationship");
            return false;
        }
    }

    public async Task<List<Dictionary<string, object>>> TraverseAsync(string startNode, int depth = 2)
    {
        try
        {
            await using var session = _driver.AsyncSession();
            var results = await session.ExecuteReadAsync(async tx =>
            {
                var query = $@"
                    MATCH path = (r:Requirement)-[*1..{depth}]-(connected)
                    WHERE r.client_ref CONTAINS $startNode OR connected.name CONTAINS $startNode
                    RETURN r.client_ref as client_ref, r.text as text, r.constraint_type as constraint_type
                    LIMIT 50";
                
                var cursor = await tx.RunAsync(query, new { startNode });
                return await cursor.ToListAsync();
            });

            return results.Select(record => new Dictionary<string, object>(record.Values)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error traversing graph");
            return new List<Dictionary<string, object>>();
        }
    }

    public async Task<long> GetNodeCountAsync()
    {
        try
        {
            await using var session = _driver.AsyncSession();
            var count = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync("MATCH (n:Requirement) RETURN count(n) as count");
                var record = await cursor.SingleAsync();
                return record["count"].As<long>();
            });
            
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting node count");
            return 0;
        }
    }

    public void Dispose()
    {
        _driver?.Dispose();
    }
}
