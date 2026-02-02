using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace QueryRouter.Core.Services;

public class LangfuseService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LangfuseService> _logger;
    private readonly string _publicKey;
    private readonly string _secretKey;
    private readonly ConcurrentQueue<object> _eventQueue = new();
    private readonly Timer _flushTimer;
    private readonly int _batchSize = 20;

    public LangfuseService(string publicKey, string secretKey, string host, ILogger<LangfuseService> logger)
    {
        _publicKey = publicKey;
        _secretKey = secretKey;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(host)
        };
        
        // Auth header (Basic Auth with Public and Secret key)
        var authString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_publicKey}:{_secretKey}"));
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

        // Flush queue every 2 seconds
        _flushTimer = new Timer(async _ => await FlushAsync(), null, 2000, 2000);
    }

    public void CreateTrace(string traceId, string name, string userId = null)
    {
        _eventQueue.Enqueue(new
        {
            id = Guid.NewGuid().ToString(),
            type = "trace-create",
            body = new
            {
                id = traceId,
                name = name,
                userId = userId,
                timestamp = DateTime.UtcNow
            }
        });
    }

    public void CreateSpan(string traceId, string spanId, string name, DateTime startTime, DateTime endTime)
    {
        _eventQueue.Enqueue(new
        {
            id = Guid.NewGuid().ToString(),
            type = "span-create",
            body = new
            {
                id = spanId,
                traceId = traceId,
                name = name,
                startTime = startTime,
                endTime = endTime
            }
        });
    }

    public void CreateGeneration(string traceId, string parentId, string name, string model, object input, object output, TokenUsage usage, DateTime startTime, DateTime endTime)
    {
        _eventQueue.Enqueue(new
        {
            id = Guid.NewGuid().ToString(),
            type = "generation-create",
            body = new
            {
                id = Guid.NewGuid().ToString(),
                traceId = traceId,
                parentObservationId = parentId,
                name = name,
                model = model,
                input = input,
                output = output,
                usage = usage,
                startTime = startTime,
                endTime = endTime
            }
        });
    }

    public void Score(string traceId, string name, double value, string comment = null)
    {
        _eventQueue.Enqueue(new
        {
            id = Guid.NewGuid().ToString(),
            type = "score-create",
            body = new
            {
                traceId = traceId,
                name = name,
                value = value,
                comment = comment
            }
        });
    }

    private async Task FlushAsync()
    {
        if (_eventQueue.IsEmpty) return;

        var batch = new List<object>();
        while (batch.Count < _batchSize && _eventQueue.TryDequeue(out var ev))
        {
            batch.Add(ev);
        }

        if (batch.Count == 0) return;

        try
        {
            var payload = new { batch = batch };
            var response = await _httpClient.PostAsJsonAsync("/api/public/ingestion", payload);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to send Langfuse batch. Status: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing Langfuse events");
        }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        _flushTimer.Dispose(); 
        // Try to flush remaining events synchronously
        try 
        {
            FlushAsync().GetAwaiter().GetResult(); 
        }
        catch { /* Ignore errors during dispose */ }
        
        _httpClient.Dispose();
    }
}

public class TokenUsage
{
    [JsonPropertyName("promptTokens")]
    public int PromptTokens { get; set; }
    
    [JsonPropertyName("completionTokens")]
    public int CompletionTokens { get; set; }
    
    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }
}
