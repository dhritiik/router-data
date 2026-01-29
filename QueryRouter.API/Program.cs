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

// Register query analyzer
builder.Services.AddScoped<IQueryAnalyzer, QueryAnalyzer>();

// Register database contexts and services
builder.Services.AddDbContext<PosDbContext>(options =>
    options.UseSqlite("Data Source=pos_requirements.db"));

builder.Services.AddSingleton<AzureOpenAIEmbeddings>();
builder.Services.AddSingleton<QdrantVectorStore>();
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

