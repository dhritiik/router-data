using QueryRouter.Core.Analyzers;
using QueryRouter.Data.SQL;
using QueryRouter.Data.Vector;
using QueryRouter.Data.Graph;
using QueryRouter.Data.Executors;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;

// Load .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "POS Requirements Query Router API", 
        Version = "v1",
        Description = "Intelligent query router for hybrid POS requirements intelligence system"
    });
});

// Register query analyzer and LLM services
builder.Services.AddSingleton<QueryRouter.Core.Services.AzureOpenAIService>();
builder.Services.AddSingleton<DatabaseSchemaProvider>();
builder.Services.AddScoped<IQueryAnalyzer, QueryAnalyzer>();

// Register database contexts and services
// Register database contexts and services
builder.Services.AddDbContext<PosDbContext>(options =>
    options.UseSqlite("Data Source=pos_requirements.db"));

// Register Langfuse
var langfusePublic = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY") ?? "";
var langfuseSecret = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY") ?? "";
var langfuseHost = Environment.GetEnvironmentVariable("LANGFUSE_HOST") ?? "https://cloud.langfuse.com";

if (!string.IsNullOrEmpty(langfusePublic))
{
    builder.Services.AddSingleton<LangfuseService>(sp => 
        new LangfuseService(langfusePublic, langfuseSecret, langfuseHost, sp.GetRequiredService<ILogger<LangfuseService>>()));
}
else
{
    // Register no-op or handle missing keys if critical, for now we effectively skip usage 
    // by not registering it, but we should register a dummy or handle null in consumers.
    // Better strategy: Register it but it will just fail to flush if keys are invalid.
    // Or cleaner: Don't register and make it optional in services.
    // For simplicity, let's register it if keys exist, otherwise consumers must check availability.
}

builder.Services.AddSingleton<AzureOpenAIEmbeddings>();
builder.Services.AddSingleton<FaissVectorStore>();
builder.Services.AddSingleton<BM25Scorer>();
builder.Services.AddSingleton<Neo4jGraphStore>();

// Register query executors
builder.Services.AddScoped<SqlQueryExecutor>();
builder.Services.AddScoped<VectorQueryExecutor>();
builder.Services.AddScoped<GraphQueryExecutor>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize vector stores
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Initializing vector store...");
        var vectorStore = services.GetRequiredService<FaissVectorStore>();
        await vectorStore.InitializeAsync();
        
        logger.LogInformation("Initializing BM25 scorer...");
        var bm25Scorer = services.GetRequiredService<BM25Scorer>();
        await bm25Scorer.InitializeAsync();
        
        logger.LogInformation("Vector stores initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing vector stores");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();

