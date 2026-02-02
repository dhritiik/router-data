using System.Text.Json;
using DataIngestion.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QueryRouter.Data.SQL;

namespace DataIngestion.Core.Services;

public class SqlDataIngestion
{
    private readonly ILogger<SqlDataIngestion> _logger;
    private readonly PosDbContext _dbContext;

    public SqlDataIngestion(ILogger<SqlDataIngestion> logger, PosDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<bool> IngestAsync(ProposalData proposalData)
    {
        try
        {
            _logger.LogInformation("Starting SQL ingestion of {Count} requirements", proposalData.Requirements.Count);

            // Ensure database is created
            await _dbContext.Database.EnsureCreatedAsync();

            // Check if data already exists
            var existingCount = await _dbContext.Requirements.CountAsync();
            if (existingCount > 0)
            {
                _logger.LogInformation("SQL database already contains {Count} requirements. Skipping ingestion.", existingCount);
                _logger.LogInformation("To re-ingest, delete the database file: QueryRouter.API/pos_requirements.db");
                return true;
            }

            // Convert and insert requirements
            var entities = proposalData.Requirements.Select(r => new RequirementEntity
            {
                ClientReferenceId = r.ClientReferenceId,
                RawText = r.RawText,
                NormalizedText = r.NormalizedText,
                ConfidenceScore = r.ConfidenceScore,
                Status = r.Status,
                ActionVerb = r.Action.Verb,
                ActionModality = r.Action.Modality,
                ObjectPrimary = r.Object.Primary,
                ObjectSecondary = JsonSerializer.Serialize(r.Object.Secondary),
                ConstraintType = r.Constraint.Type,
                ConstraintDescription = r.Constraint.Description,
                ConstraintSubcategories = JsonSerializer.Serialize(r.Constraint.Subcategories),
                RequirementType = r.Classification.RequirementType,
                Criticality = r.Classification.Criticality,
                Systems = JsonSerializer.Serialize(r.Entities.Systems),
                Standards = JsonSerializer.Serialize(r.Entities.Standards),
                Regulations = JsonSerializer.Serialize(r.Entities.Regulations),
                Regions = JsonSerializer.Serialize(r.Entities.Regions),
                DocumentName = r.Provenance.DocumentName,
                Section = r.Provenance.Section,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            // Batch insert
            var batchSize = 100;
            for (int i = 0; i < entities.Count; i += batchSize)
            {
                var batch = entities.Skip(i).Take(batchSize).ToList();
                await _dbContext.Requirements.AddRangeAsync(batch);
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Inserted batch {Current}/{Total}", 
                    Math.Min(i + batchSize, entities.Count), 
                    entities.Count);
            }

            _logger.LogInformation("Successfully ingested {Count} requirements into SQL database", entities.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SQL ingestion");
            return false;
        }
    }

    public async Task<int> GetRequirementCountAsync()
    {
        return await _dbContext.Requirements.CountAsync();
    }
}
