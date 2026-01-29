using DataIngestion.Core.Models;
using Microsoft.Extensions.Logging;
using QueryRouter.Data.Graph;

namespace DataIngestion.Core.Services;

public class GraphDataIngestion
{
    private readonly ILogger<GraphDataIngestion> _logger;
    private readonly Neo4jGraphStore _graphStore;

    public GraphDataIngestion(ILogger<GraphDataIngestion> logger, Neo4jGraphStore graphStore)
    {
        _logger = logger;
        _graphStore = graphStore;
    }

    public async Task<bool> IngestAsync(ProposalData proposalData)
    {
        try
        {
            _logger.LogInformation("Starting graph ingestion of {Count} requirements", proposalData.Requirements.Count);

            // Clear existing data
            await _graphStore.ClearDatabaseAsync();

            // Create requirement nodes
            foreach (var req in proposalData.Requirements)
            {
                await _graphStore.CreateRequirementNodeAsync(
                    req.ClientReferenceId,
                    req.NormalizedText,
                    req.Constraint.Type,
                    req.Classification.RequirementType
                );
            }

            _logger.LogInformation("Created {Count} requirement nodes", proposalData.Requirements.Count);

            // Create system and regulation nodes and relationships
            var systemsCreated = new HashSet<string>();
            var regulationsCreated = new HashSet<string>();

            foreach (var req in proposalData.Requirements)
            {
                // Create system nodes and relationships
                foreach (var system in req.Entities.Systems)
                {
                    if (!string.IsNullOrWhiteSpace(system) && !systemsCreated.Contains(system))
                    {
                        await _graphStore.CreateSystemNodeAsync(system);
                        systemsCreated.Add(system);
                    }
                    
                    if (!string.IsNullOrWhiteSpace(system))
                    {
                        await _graphStore.CreateRelationshipAsync(req.ClientReferenceId, system, "USES_SYSTEM", "System");
                    }
                }

                // Create regulation nodes and relationships
                foreach (var regulation in req.Entities.Regulations)
                {
                    if (!string.IsNullOrWhiteSpace(regulation) && !regulationsCreated.Contains(regulation))
                    {
                        await _graphStore.CreateRegulationNodeAsync(regulation);
                        regulationsCreated.Add(regulation);
                    }
                    
                    if (!string.IsNullOrWhiteSpace(regulation))
                    {
                        await _graphStore.CreateRelationshipAsync(req.ClientReferenceId, regulation, "COMPLIES_WITH", "Regulation");
                    }
                }
            }

            var nodeCount = await _graphStore.GetNodeCountAsync();
            _logger.LogInformation("Successfully ingested graph with {Count} requirement nodes, {Systems} systems, {Regulations} regulations", 
                nodeCount, systemsCreated.Count, regulationsCreated.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during graph ingestion");
            return false;
        }
    }
}
