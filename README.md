# Multi-Database POS Requirements Query Router

A sophisticated query routing system that intelligently directs natural language queries to the appropriate database (SQL, Vector, or Graph) based on query intent and semantic analysis.

## 🎯 Features

- **Intelligent Query Routing**: Automatically routes queries to SQL, Vector, or Graph databases based on intent
- **Multi-Database Support**: SQLite for structured data, Qdrant for semantic search, Neo4j for relationship traversal
- **Classification-Based Logic**: Uses `requirement_type` from classification as primary field
- **Semantic Search**: Azure OpenAI embeddings for similarity matching
- **Graph Traversal**: Neo4j for exploring requirement relationships
- **REST API**: Swagger-documented API for easy integration
- **Data Ingestion CLI**: Command-line tool for populating all databases

## 📋 Prerequisites

- **.NET 8.0 SDK** or later
- **Docker Desktop** (for Qdrant and Neo4j)
- **Azure OpenAI API Key** (for embeddings)
- **macOS, Linux, or Windows**

## 🚀 Quick Start

### 1. Clone the Repository

```bash
git clone <your-repo-url>
cd router-data
```

### 2. Set Up Environment Variables

Create a `.env` file in the root directory:

```bash
# Azure OpenAI Configuration
AZURE_OPENAI_ENDPOINT=https://your-endpoint.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_DEPLOYMENT_NAME=text-embedding-3-large
AZURE_OPENAI_API_VERSION=2024-02-15-preview
```

### 3. Start Docker Services

Start Qdrant (Vector DB) and Neo4j (Graph DB):

```bash
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant:latest

docker run -d --name neo4j \
  -p 7474:7474 -p 7687:7687 \
  -e NEO4J_AUTH=neo4j/password \
  neo4j:latest
```

Verify services are running:
```bash
docker ps
```

### 4. Restore Dependencies

```bash
dotnet restore
```

### 5. Ingest Data

Run the data ingestion CLI to populate all three databases:

```bash
cd DataIngestion.CLI
dotnet run -- ingest-all
```

This will:
- ✅ Load 97 requirements from `requirements.json`
- ✅ Ingest into SQLite database
- ✅ Generate embeddings and store in Qdrant
- ✅ Create graph nodes and relationships in Neo4j

### 6. Start the API

```bash
cd ../QueryRouter.API
dotnet run
```

The API will start at: **http://localhost:5000**

Swagger UI: **http://localhost:5000/swagger**

## 🧪 Test the System

### Test SQL Query (Structured Data)
```bash
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "count functional requirements"}'
```

**Expected**: Returns count of 42 functional requirements

### Test Vector Query (Semantic Similarity)
```bash
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "Find requirements similar to offline transaction handling"}'
```

**Expected**: Returns 10 semantically similar requirements

### Test Graph Query (Relationships)
```bash
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "Which requirements are related to PCI-DSS compliance?"}'
```

**Expected**: Returns requirements connected to PCI-DSS via graph relationships

### Test System-Based Query
```bash
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "requirements with functional constraint in POS"}'
```

**Expected**: Returns requirements with `constraint.type = functional` AND `systems CONTAINS POS`

## 📁 Project Structure

```
router-data/
├── QueryRouter.sln              # Solution file
├── requirements.json            # Source data (97 POS requirements)
├── .env                         # Environment variables (create this)
│
├── QueryRouter.Core/            # Core routing logic
│   ├── Models/                  # Data models
│   ├── Analyzers/               # Query analysis
│   └── Rules/                   # Routing rules (SQL, Vector, Graph, Hybrid)
│
├── QueryRouter.Data/            # Data access layer
│   ├── SQL/                     # SQLite DbContext
│   ├── Vector/                  # Qdrant integration
│   ├── Graph/                   # Neo4j integration
│   └── Executors/               # Query executors
│
├── QueryRouter.API/             # REST API
│   ├── Controllers/             # API endpoints
│   ├── Program.cs               # API configuration
│   └── pos_requirements.db      # SQLite database (created after ingestion)
│
├── DataIngestion.Core/          # Ingestion services
│   ├── Models/                  # JSON models
│   └── Services/                # SQL, Vector, Graph ingestion
│
└── DataIngestion.CLI/           # CLI tool
    └── Program.cs               # ingest-sql, ingest-vector, ingest-graph, ingest-all
```

## 🔧 Configuration

### Database Connections

The system uses these default configurations:

| Database | Connection | Credentials |
|----------|-----------|-------------|
| **SQLite** | `pos_requirements.db` | N/A |
| **Qdrant** | `localhost:6334` | N/A |
| **Neo4j** | `bolt://localhost:7687` | `neo4j/password` |

To change Neo4j password, update `QueryRouter.Data/Graph/Neo4jGraphStore.cs`:
```csharp
var password = configuration["NEO4J_PASSWORD"] ?? "your-password";
```

### Azure OpenAI

Update `.env` with your Azure OpenAI credentials:
- Endpoint URL
- API Key
- Deployment name (must support embeddings, e.g., `text-embedding-3-large`)

## 📊 Query Routing Logic

### Default Behavior (requirement_type)

Queries use **`classification.requirement_type`** by default:

```
"count functional requirements" → requirement_type = functional (42 results)
"count security requirements" → requirement_type = security (19 results)
```

### System-Based Queries (constraint.type)

When systems (POS, backend, frontend, etc.) are mentioned, uses **`constraint.type`**:

```
"requirements with functional constraint in POS"
→ constraint.type = functional AND systems CONTAINS POS (3 results)
```

### Subcategories

Subcategories are matched with word boundaries:

```
"functional requirements related to finance"
→ requirement_type = functional OR subcategories CONTAINS finance
```

## 🗄️ Database Schema

### SQLite (Requirements Table)

| Column | Type | Description |
|--------|------|-------------|
| `RequirementType` | TEXT | From `classification.requirement_type` (PRIMARY) |
| `ConstraintType` | TEXT | From `constraint.type` (used with systems) |
| `ConstraintSubcategories` | TEXT | JSON array from `constraint.subcategories` |
| `Systems` | TEXT | JSON array from `entities.systems` |
| `NormalizedText` | TEXT | Normalized requirement text |
| `Criticality` | TEXT | mandatory, high, medium, low |

### Qdrant (Vector Points)

Each requirement is embedded using Azure OpenAI and stored with metadata:
- `client_reference_id`
- `requirement_type`
- `constraint_type`
- `normalized_text`

### Neo4j (Graph Nodes)

- **Requirement nodes**: 97 nodes with `requirement_type` and `constraint_type`
- **System nodes**: 25 systems (POS, backend, etc.)
- **Regulation nodes**: 3 regulations (PCI-DSS, etc.)
- **Relationships**: `USES_SYSTEM`, `COMPLIES_WITH`

## 🛠️ CLI Commands

### Ingest All Databases
```bash
cd DataIngestion.CLI
dotnet run -- ingest-all
```

### Ingest Individual Databases
```bash
# SQL only
dotnet run -- ingest-sql

# Vector only
dotnet run -- ingest-vector

# Graph only
dotnet run -- ingest-graph
```

## 🌐 API Endpoints

### Execute Query
```
POST /api/QueryRouter/execute
Content-Type: application/json

{
  "query": "your natural language query here"
}
```

**Response**:
```json
{
  "query": "count functional requirements",
  "routing": {
    "route": 0,
    "confidence": 0.95,
    "reasoning": "Query involves requirement type classification...",
    "sqlIntent": {
      "filters": ["requirement_type = functional"],
      "aggregations": ["COUNT/SUM"]
    }
  },
  "results": [
    {
      "clientReferenceId": "AGGREGATION_RESULT",
      "normalizedText": "Total count: 42",
      "source": "SQL_AGGREGATION"
    }
  ]
}
```

## 🐛 Troubleshooting

### Port Already in Use

If port 5000 is in use:
```bash
lsof -ti:5000 | xargs kill -9
```

### Docker Services Not Running

Check Docker status:
```bash
docker ps
```

Restart services:
```bash
docker restart qdrant neo4j
```

### Neo4j Connection Issues

Verify Neo4j is accessible:
```bash
curl http://localhost:7474
```

Check credentials in `Neo4jGraphStore.cs` match Docker container (`neo4j/password`)

### Azure OpenAI Errors

Verify `.env` file exists and contains valid credentials:
```bash
cat .env
```

Test endpoint:
```bash
curl -H "api-key: YOUR_KEY" \
  https://your-endpoint.openai.azure.com/openai/deployments?api-version=2024-02-15-preview
```

## 📈 Performance

- **SQL Queries**: < 50ms for aggregations
- **Vector Search**: ~500ms for 10 similar results
- **Graph Traversal**: ~200ms for depth-2 relationships
- **Hybrid Queries**: ~800ms combining multiple databases

## 🔐 Security Notes

- **`.env` file**: Contains API keys - **DO NOT commit to Git**
- **Neo4j password**: Change default password in production
- **API**: No authentication by default - add auth middleware for production

## 📝 License

[Your License Here]

## 🤝 Contributing

[Your Contributing Guidelines Here]

## 📧 Support

[Your Support Information Here]
