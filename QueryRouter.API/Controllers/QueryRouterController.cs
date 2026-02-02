using Microsoft.AspNetCore.Mvc;
using QueryRouter.Core.Analyzers;
using QueryRouter.Core.Models;
using QueryRouter.Data.Executors;

namespace QueryRouter.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryRouterController : ControllerBase
{
    private readonly IQueryAnalyzer _queryAnalyzer;
    private readonly ILogger<QueryRouterController> _logger;
    private readonly SqlQueryExecutor? _sqlExecutor;
    private readonly VectorQueryExecutor? _vectorExecutor;
    private readonly GraphQueryExecutor? _graphExecutor;

    public QueryRouterController(
        IQueryAnalyzer queryAnalyzer,
        ILogger<QueryRouterController> logger,
        SqlQueryExecutor? sqlExecutor = null,
        VectorQueryExecutor? vectorExecutor = null,
        GraphQueryExecutor? graphExecutor = null)
    {
        _queryAnalyzer = queryAnalyzer;
        _logger = logger;
        _sqlExecutor = sqlExecutor;
        _vectorExecutor = vectorExecutor;
        _graphExecutor = graphExecutor;
    }

    /// <summary>
    /// Analyzes a query and returns routing information
    /// </summary>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(QueryRoutingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QueryRoutingResult>> AnalyzeQuery([FromBody] QueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query cannot be empty" });
        }

        try
        {
            var result = await _queryAnalyzer.AnalyzeAsync(request.Query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query: {Query}", request.Query);
            return StatusCode(500, new { error = "Internal server error analyzing query" });
        }
    }

    /// <summary>
    /// Executes a query and returns actual results from the databases
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(QueryExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QueryExecutionResult>> ExecuteQuery([FromBody] QueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query cannot be empty" });
        }

        try
        {
            // First, analyze the query
            var routing = await _queryAnalyzer.AnalyzeAsync(request.Query);
            
            var results = new List<RequirementResult>();

            // Execute based on route type
            switch (routing.Route)
            {
                case RouteType.SQL:
                    if (_sqlExecutor != null && routing.SqlIntent != null)
                    {
                        results = await _sqlExecutor.ExecuteAsync(routing, request.Query);
                    }
                    break;

                case RouteType.VECTOR:
                    if (_vectorExecutor != null && routing.VectorIntent != null)
                    {
                        results = await _vectorExecutor.ExecuteAsync(routing.VectorIntent);
                    }
                    break;

                case RouteType.GRAPH:
                    if (_graphExecutor != null && routing.GraphIntent != null)
                    {
                        results = await _graphExecutor.ExecuteAsync(routing.GraphIntent);
                    }
                    break;

                case RouteType.HYBRID:
                    // Execute all applicable queries and merge results
                    if (_sqlExecutor != null && routing.SqlIntent != null)
                    {
                        var sqlResults = await _sqlExecutor.ExecuteAsync(routing, request.Query);
                        results.AddRange(sqlResults);
                    }
                    if (_vectorExecutor != null && routing.VectorIntent != null)
                    {
                        var vectorResults = await _vectorExecutor.ExecuteAsync(routing.VectorIntent);
                        results.AddRange(vectorResults);
                    }
                    if (_graphExecutor != null && routing.GraphIntent != null)
                    {
                        var graphResults = await _graphExecutor.ExecuteAsync(routing.GraphIntent);
                        results.AddRange(graphResults);
                    }
                    
                    // Deduplicate by client reference ID
                    results = results
                        .GroupBy(r => r.ClientReferenceId)
                        .Select(g => g.OrderByDescending(r => r.Score).First())
                        .OrderByDescending(r => r.Score)
                        .ToList();
                    break;
            }

            return Ok(new QueryExecutionResult
            {
                Query = request.Query,
                Routing = routing,
                Results = results,
                TotalResults = results.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {Query}", request.Query);
            return StatusCode(500, new { error = "Internal server error executing query" });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}

public class QueryRequest
{
    public string Query { get; set; } = string.Empty;
}

public class QueryExecutionResult
{
    public string Query { get; set; } = string.Empty;
    public QueryRoutingResult Routing { get; set; } = new();
    public List<RequirementResult> Results { get; set; } = new();
    public int TotalResults { get; set; }
}

