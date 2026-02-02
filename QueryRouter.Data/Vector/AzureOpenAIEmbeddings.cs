using Azure;
using Azure.AI.OpenAI;
using OpenAI.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QueryRouter.Data.Vector;

public class AzureOpenAIEmbeddings
{
    private readonly ILogger<AzureOpenAIEmbeddings> _logger;
    private readonly AzureOpenAIClient _client;
    private readonly string _deploymentName;

    public AzureOpenAIEmbeddings(ILogger<AzureOpenAIEmbeddings> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        var endpoint = configuration["AZURE_OPENAI_ENDPOINT"] 
            ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT not configured");
        var apiKey = configuration["AZURE_OPENAI_API_KEY"] 
            ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY not configured");
        _deploymentName = configuration["AZURE_OPENAI_EMB_DEPLOYMENT"] 
            ?? throw new InvalidOperationException("AZURE_OPENAI_EMB_DEPLOYMENT not configured");

        _client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        
        _logger.LogInformation("Azure OpenAI Embeddings initialized with deployment: {Deployment}", _deploymentName);
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_deploymentName);
            var response = await embeddingClient.GenerateEmbeddingAsync(text);
            
            var embedding = response.Value.ToFloats().ToArray();
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for text: {Text}", text.Substring(0, Math.Min(50, text.Length)));
            return null;
        }
    }

    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts, int batchSize = 100)
    {
        var embeddings = new List<float[]>();
        
        _logger.LogInformation("Generating embeddings for {Count} texts in batches of {BatchSize}", texts.Count, batchSize);

        var embeddingClient = _client.GetEmbeddingClient(_deploymentName);

        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();
            
            try
            {
                var response = await embeddingClient.GenerateEmbeddingsAsync(batch);
                
                foreach (var item in response.Value)
                {
                    embeddings.Add(item.ToFloats().ToArray());
                }
                
                _logger.LogInformation("Generated embeddings for batch {Current}/{Total}", 
                    Math.Min(i + batchSize, texts.Count), 
                    texts.Count);
                
                // Rate limiting
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings at index {Index}", i);
                // Add null embeddings for failed batch
                for (int j = 0; j < batch.Count; j++)
                {
                    embeddings.Add(new float[3072]); // Empty embedding
                }
            }
        }

        return embeddings;
    }
}
