using DataIngestion.Core.Models;
using DataIngestion.Core.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QueryRouter.Data.Graph;
using QueryRouter.Data.SQL;
using QueryRouter.Data.Vector;

namespace DataIngestion.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Load .env file
        Env.Load();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        // Setup DI
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Configuration
        services.AddSingleton<IConfiguration>(configuration);

        // Database contexts
        services.AddDbContext<PosDbContext>(options =>
            options.UseSqlite("Data Source=pos_requirements.db"));

        // Services
        services.AddSingleton<AzureOpenAIEmbeddings>();
        services.AddSingleton<FaissVectorStore>();
        services.AddSingleton<BM25Scorer>();
        services.AddSingleton<Neo4jGraphStore>();
        services.AddScoped<JsonLoader>();
        services.AddScoped<SqlDataIngestion>();
        services.AddScoped<VectorDataIngestion>();
        services.AddScoped<GraphDataIngestion>();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("=== POS Requirements Data Ingestion Tool ===");
            
            var command = args.Length > 0 ? args[0] : "ingest-all";
            var filePath = args.Length > 1 ? args[1] : "/Users/dhritikothari/Desktop/router-data/requirements.json";

            logger.LogInformation("Command: {Command}", command);
            logger.LogInformation("File: {File}", filePath);

            // Load JSON
            var jsonLoader = serviceProvider.GetRequiredService<JsonLoader>();
            var proposalData = await jsonLoader.LoadRequirementsAsync(filePath);

            if (proposalData == null)
            {
                logger.LogError("Failed to load requirements data");
                return 1;
            }

            var isValid = await jsonLoader.ValidateRequirementsAsync(proposalData);
            if (!isValid)
            {
                logger.LogWarning("Validation warnings found, but continuing...");
            }

            bool success = false;

            switch (command.ToLower())
            {
                case "ingest-sql":
                    success = await IngestSqlAsync(serviceProvider, proposalData);
                    break;

                case "ingest-vector":
                    success = await IngestVectorAsync(serviceProvider, proposalData);
                    break;

                case "ingest-graph":
                    success = await IngestGraphAsync(serviceProvider, proposalData);
                    break;

                case "ingest-all":
                    logger.LogInformation("Ingesting into all databases...");
                    var sqlSuccess = await IngestSqlAsync(serviceProvider, proposalData);
                    var vectorSuccess = await IngestVectorAsync(serviceProvider, proposalData);
                    var graphSuccess = await IngestGraphAsync(serviceProvider, proposalData);
                    success = sqlSuccess && vectorSuccess && graphSuccess;
                    break;

                default:
                    logger.LogError("Unknown command: {Command}. Use: ingest-sql, ingest-vector, ingest-graph, or ingest-all", command);
                    return 1;
            }

            if (success)
            {
                logger.LogInformation("✅ Ingestion completed successfully!");
                return 0;
            }
            else
            {
                logger.LogError("❌ Ingestion failed");
                return 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error during ingestion");
            return 1;
        }
    }

    static async Task<bool> IngestSqlAsync(ServiceProvider serviceProvider, ProposalData proposalData)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("\n=== SQL Database Ingestion ===");
        
        var sqlIngestion = serviceProvider.GetRequiredService<SqlDataIngestion>();
        var success = await sqlIngestion.IngestAsync(proposalData);
        
        if (success)
        {
            var count = await sqlIngestion.GetRequirementCountAsync();
            logger.LogInformation("✅ SQL: {Count} requirements ingested", count);
        }
        
        return success;
    }

    static async Task<bool> IngestVectorAsync(ServiceProvider serviceProvider, ProposalData proposalData)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("\n=== Vector Database Ingestion ===");
        
        var vectorIngestion = serviceProvider.GetRequiredService<VectorDataIngestion>();
        var success = await vectorIngestion.IngestAsync(proposalData);
        
        if (success)
        {
            logger.LogInformation("✅ Vector: Embeddings generated and stored in FAISS");
        }
        
        return success;
    }

    static async Task<bool> IngestGraphAsync(ServiceProvider serviceProvider, ProposalData proposalData)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("\n=== Graph Database Ingestion ===");
        
        var graphIngestion = serviceProvider.GetRequiredService<GraphDataIngestion>();
        var success = await graphIngestion.IngestAsync(proposalData);
        
        if (success)
        {
            logger.LogInformation("✅ Graph: Nodes and relationships created in Neo4j");
        }
        
        return success;
    }
}
