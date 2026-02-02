using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace QueryRouter.Data.Vector;

/// <summary>
/// BM25 (Best Matching 25) scorer for keyword-based search.
/// Implements the BM25 ranking function for hybrid retrieval.
/// </summary>
public class BM25Scorer
{
    private readonly ILogger<BM25Scorer> _logger;
    private readonly string _indexDirectory;
    
    // BM25 parameters
    private const double K1 = 1.5;  // Term frequency saturation parameter
    private const double B = 0.75;  // Length normalization parameter
    
    // Index data
    private Dictionary<string, Dictionary<string, int>> _invertedIndex = new();
    private Dictionary<string, int> _docLengths = new();
    private Dictionary<string, string> _docTexts = new();
    private double _avgDocLength = 0;
    private int _totalDocs = 0;
    
    private const string BM25IndexFile = "bm25_index.json";
    
    public BM25Scorer(ILogger<BM25Scorer> logger)
    {
        _logger = logger;
        _indexDirectory = Path.Combine(Directory.GetCurrentDirectory(), "vector_indexes");
        Directory.CreateDirectory(_indexDirectory);
    }

    /// <summary>
    /// Initialize BM25 scorer by loading existing index or creating new one
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            var indexPath = Path.Combine(_indexDirectory, BM25IndexFile);
            
            if (File.Exists(indexPath))
            {
                _logger.LogInformation("Loading existing BM25 index from {Path}", indexPath);
                
                var json = await File.ReadAllTextAsync(indexPath);
                var data = JsonSerializer.Deserialize<BM25IndexData>(json);
                
                if (data != null)
                {
                    _invertedIndex = data.InvertedIndex;
                    _docLengths = data.DocLengths;
                    _docTexts = data.DocTexts;
                    _avgDocLength = data.AvgDocLength;
                    _totalDocs = data.TotalDocs;
                    
                    _logger.LogInformation("Loaded BM25 index with {Count} documents", _totalDocs);
                    return true;
                }
            }
            
            _logger.LogInformation("No existing BM25 index found. Ready for building.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize BM25 scorer");
            return false;
        }
    }

    /// <summary>
    /// Build BM25 index from documents
    /// </summary>
    public async Task<bool> BuildIndexAsync(Dictionary<string, string> documents)
    {
        try
        {
            _logger.LogInformation("Building BM25 index for {Count} documents", documents.Count);
            
            _invertedIndex.Clear();
            _docLengths.Clear();
            _docTexts = new Dictionary<string, string>(documents);
            _totalDocs = documents.Count;
            
            // Build inverted index
            foreach (var (docId, text) in documents)
            {
                var tokens = Tokenize(text);
                _docLengths[docId] = tokens.Count;
                
                var termFreqs = new Dictionary<string, int>();
                foreach (var token in tokens)
                {
                    termFreqs[token] = termFreqs.GetValueOrDefault(token, 0) + 1;
                }
                
                foreach (var (term, freq) in termFreqs)
                {
                    if (!_invertedIndex.ContainsKey(term))
                    {
                        _invertedIndex[term] = new Dictionary<string, int>();
                    }
                    _invertedIndex[term][docId] = freq;
                }
            }
            
            // Calculate average document length
            _avgDocLength = _docLengths.Values.Average();
            
            // Save index
            await SaveIndexAsync();
            
            _logger.LogInformation("BM25 index built successfully. Avg doc length: {AvgLength}", _avgDocLength);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build BM25 index");
            return false;
        }
    }

    /// <summary>
    /// Search using BM25 scoring
    /// </summary>
    public List<(string docId, double score)> Search(string query, int topK = 100)
    {
        try
        {
            if (_totalDocs == 0)
            {
                _logger.LogWarning("BM25 index is empty. Returning no results.");
                return new List<(string, double)>();
            }
            
            var queryTokens = Tokenize(query);
            var scores = new Dictionary<string, double>();
            
            // Calculate BM25 score for each document
            foreach (var docId in _docLengths.Keys)
            {
                var score = CalculateBM25Score(queryTokens, docId);
                if (score > 0)
                {
                    scores[docId] = score;
                }
            }
            
            // Sort by score and return top K
            var results = scores
                .OrderByDescending(x => x.Value)
                .Take(topK)
                .Select(x => (x.Key, x.Value))
                .ToList();
            
            _logger.LogInformation("BM25 search returned {Count} results for query: {Query}", 
                results.Count, query);
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BM25 search failed");
            return new List<(string, double)>();
        }
    }

    /// <summary>
    /// Calculate BM25 score for a document given query tokens
    /// </summary>
    private double CalculateBM25Score(List<string> queryTokens, string docId)
    {
        double score = 0;
        var docLength = _docLengths[docId];
        
        foreach (var term in queryTokens.Distinct())
        {
            if (!_invertedIndex.ContainsKey(term))
                continue;
            
            var termDocs = _invertedIndex[term];
            if (!termDocs.ContainsKey(docId))
                continue;
            
            var termFreq = termDocs[docId];
            var docFreq = termDocs.Count;
            
            // IDF calculation
            var idf = Math.Log((_totalDocs - docFreq + 0.5) / (docFreq + 0.5) + 1.0);
            
            // TF calculation with BM25 normalization
            var tf = (termFreq * (K1 + 1)) / 
                     (termFreq + K1 * (1 - B + B * (docLength / _avgDocLength)));
            
            score += idf * tf;
        }
        
        return score;
    }

    /// <summary>
    /// Tokenize text into terms
    /// </summary>
    private List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();
        
        // Extract alphanumeric tokens and convert to lowercase
        var tokens = Regex.Matches(text.ToLower(), @"[a-z0-9]+")
            .Select(m => m.Value)
            .Where(t => t.Length > 1) // Filter out single characters
            .ToList();
        
        return tokens;
    }

    /// <summary>
    /// Save BM25 index to disk
    /// </summary>
    private async Task SaveIndexAsync()
    {
        var indexPath = Path.Combine(_indexDirectory, BM25IndexFile);
        
        var data = new BM25IndexData
        {
            InvertedIndex = _invertedIndex,
            DocLengths = _docLengths,
            DocTexts = _docTexts,
            AvgDocLength = _avgDocLength,
            TotalDocs = _totalDocs
        };
        
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(indexPath, json);
        _logger.LogInformation("Saved BM25 index to {Path}", indexPath);
    }
}

/// <summary>
/// Data structure for BM25 index persistence
/// </summary>
public class BM25IndexData
{
    public Dictionary<string, Dictionary<string, int>> InvertedIndex { get; set; } = new();
    public Dictionary<string, int> DocLengths { get; set; } = new();
    public Dictionary<string, string> DocTexts { get; set; } = new();
    public double AvgDocLength { get; set; }
    public int TotalDocs { get; set; }
}
