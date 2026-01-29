using System.Text.Json;
using DataIngestion.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataIngestion.Core.Services;

public class JsonLoader
{
    private readonly ILogger<JsonLoader> _logger;

    public JsonLoader(ILogger<JsonLoader> logger)
    {
        _logger = logger;
    }

    public async Task<ProposalData?> LoadRequirementsAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Loading requirements from {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found: {FilePath}", filePath);
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(filePath);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var proposalData = JsonSerializer.Deserialize<ProposalData>(jsonContent, options);

            if (proposalData == null)
            {
                _logger.LogError("Failed to deserialize JSON from {FilePath}", filePath);
                return null;
            }

            _logger.LogInformation(
                "Successfully loaded {Count} requirements from {FilePath}",
                proposalData.Requirements.Count,
                filePath
            );

            return proposalData;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while loading {FilePath}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading {FilePath}", filePath);
            return null;
        }
    }

    public async Task<bool> ValidateRequirementsAsync(ProposalData proposalData)
    {
        _logger.LogInformation("Validating requirements data");

        var issues = new List<string>();

        if (proposalData.Requirements.Count == 0)
        {
            issues.Add("No requirements found in the data");
        }

        var duplicateRefs = proposalData.Requirements
            .GroupBy(r => r.ClientReferenceId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateRefs.Any())
        {
            issues.Add($"Found {duplicateRefs.Count} duplicate client reference IDs");
        }

        var emptyTexts = proposalData.Requirements
            .Where(r => string.IsNullOrWhiteSpace(r.NormalizedText))
            .Count();

        if (emptyTexts > 0)
        {
            issues.Add($"Found {emptyTexts} requirements with empty normalized text");
        }

        if (issues.Any())
        {
            foreach (var issue in issues)
            {
                _logger.LogWarning("Validation issue: {Issue}", issue);
            }
            return false;
        }

        _logger.LogInformation("Validation passed successfully");
        return true;
    }
}
