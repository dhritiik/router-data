# SQLite-Only Setup (No Docker Required)

This guide is for running the query router with **SQLite only** when Docker is not available. Perfect for testing routing logic and SQL queries on laptops without Docker Desktop.

## 🎯 What Works Without Docker

✅ **SQL Database Queries** - Full support  
✅ **Query Routing Logic** - Routes to SQL correctly  
✅ **COUNT Aggregations** - Works perfectly  
✅ **Filtering & Search** - All SQL-based queries  
✅ **API & Swagger** - Full functionality  

⚠️ **Limited Functionality** (requires Docker):  
❌ Vector similarity search (needs Qdrant)  
❌ Graph relationship traversal (needs Neo4j)  

## 🚀 Quick Setup (3 Steps)

### Step 1: Clone and Navigate
```bash
git clone <your-repo-url>
cd router-data
```

### Step 2: Ingest Data into SQLite
```bash
cd DataIngestion.CLI
dotnet run -- ingest-sql
```

**Expected Output**:
```
✅ SQL: 97 requirements ingested
✅ Ingestion completed successfully!
```

This creates `QueryRouter.API/pos_requirements.db` with all 97 requirements.

### Step 3: Start the API
```bash
cd ../QueryRouter.API
dotnet run
```

**API will start at**: http://localhost:5000  
**Swagger UI**: http://localhost:5000/swagger

## ⚠️ Handling Vector/Graph Initialization Errors

The API will show warnings about Qdrant and Neo4j not being available. **This is normal and safe to ignore**:

```
warn: Failed to connect to Qdrant at localhost:6334
warn: Failed to connect to Neo4j at bolt://localhost:7687
```

The API will still start successfully and handle SQL queries perfectly.

## 🧪 Test Queries (SQLite Only)

### ✅ Count Queries
```bash
# Count functional requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "count functional requirements"}'
# Expected: 42

# Count security requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "count security requirements"}'
# Expected: 19

# Count all requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "count all requirements"}'
# Expected: 97
```

### ✅ List Queries
```bash
# List functional requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "list functional requirements"}'

# List security requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "show all security requirements"}'
```

### ✅ System-Based Queries
```bash
# Requirements with functional constraint in POS
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "requirements with functional constraint in POS"}'
# Expected: 3 results

# POS backend requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "show requirements for POS backend"}'
```

### ✅ Subcategory Queries
```bash
# Finance-related requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "requirements related to finance"}'

# UI requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "show UI requirements"}'
```

## ❌ Queries That Won't Work (Need Docker)

These queries will route to Vector or Graph but fail gracefully:

```bash
# Vector queries (need Qdrant)
"Find similar requirements to offline transactions"
"Requirements semantically similar to payment processing"

# Graph queries (need Neo4j)
"Which requirements are related to PCI-DSS?"
"Show requirements connected to payment systems"
```

**What happens**: The API will return an error message indicating the database is unavailable, but won't crash.

## 📊 Database Verification

Check your SQLite database directly:

```bash
# Count all requirements
sqlite3 QueryRouter.API/pos_requirements.db \
  "SELECT COUNT(*) FROM Requirements;"
# Output: 97

# Count by requirement type
sqlite3 QueryRouter.API/pos_requirements.db \
  "SELECT RequirementType, COUNT(*) FROM Requirements GROUP BY RequirementType;"

# View sample requirements
sqlite3 QueryRouter.API/pos_requirements.db \
  "SELECT ClientReferenceId, NormalizedText FROM Requirements LIMIT 5;"
```

## 🔧 Troubleshooting

### Port 5000 Already in Use
```bash
# Kill existing process
lsof -ti:5000 | xargs kill -9

# Then restart
cd QueryRouter.API
dotnet run
```

### Database Not Found
If you see "no such table: Requirements":

```bash
# Re-run SQL ingestion
cd DataIngestion.CLI
dotnet run -- ingest-sql

# Verify database exists
ls -lh ../QueryRouter.API/pos_requirements.db
```

### API Crashes on Startup
If the API crashes trying to connect to Qdrant/Neo4j, you may need to modify the code. See "Optional: Disable Vector/Graph" below.

## 🛠️ Optional: Disable Vector/Graph Initialization

If the API won't start due to Vector/Graph connection errors, you can temporarily disable them:

**Edit**: `QueryRouter.API/Program.cs`

**Find** (around line 30-40):
```csharp
builder.Services.AddSingleton<AzureOpenAIEmbeddings>();
builder.Services.AddSingleton<QdrantVectorStore>();
builder.Services.AddSingleton<Neo4jGraphStore>();
```

**Comment out**:
```csharp
// builder.Services.AddSingleton<AzureOpenAIEmbeddings>();
// builder.Services.AddSingleton<QdrantVectorStore>();
// builder.Services.AddSingleton<Neo4jGraphStore>();
```

Then rebuild and run:
```bash
cd QueryRouter.API
dotnet build
dotnet run
```

## 📈 What You Can Test

With SQLite only, you can fully test and improve:

1. **SQL Routing Logic** - How queries are classified as SQL
2. **Filter Generation** - `requirement_type`, `constraint.type`, `subcategories`, `systems`
3. **Aggregation Logic** - COUNT, SUM operations
4. **Query Parsing** - Word boundary matching, keyword detection
5. **System Detection** - When to use `constraint.type` vs `requirement_type`
6. **API Responses** - JSON structure, error handling

## 🎓 Learning & Development

Perfect for:
- Testing new routing rules
- Adding new SQL filters
- Debugging query classification
- Understanding the routing logic
- Developing without cloud dependencies

## 📝 Data Modification

To modify requirements data:

1. Edit `requirements.json`
2. Re-run ingestion:
   ```bash
   cd DataIngestion.CLI
   dotnet run -- ingest-sql
   ```
3. Restart API

## 🚀 Upgrade Path

When Docker becomes available:

1. Install Docker Desktop
2. Start services:
   ```bash
   docker run -d --name qdrant -p 6334:6334 qdrant/qdrant:latest
   docker run -d --name neo4j -p 7474:7474 -p 7687:7687 \
     -e NEO4J_AUTH=neo4j/password neo4j:latest
   ```
3. Create `.env` with Azure OpenAI credentials
4. Run full ingestion:
   ```bash
   cd DataIngestion.CLI
   dotnet run -- ingest-all
   ```
5. Restart API - now all features work!

## 📊 Expected Counts

| Query | Count |
|-------|-------|
| Total requirements | 97 |
| Functional requirements | 42 |
| Security requirements | 19 |
| Non-functional requirements | 17 |
| Compliance requirements | 9 |
| Integration requirements | 10 |

## 💡 Tips

- Use Swagger UI (http://localhost:5000/swagger) for easy testing
- Check logs to see how queries are routed
- SQLite database is portable - copy `pos_requirements.db` anywhere
- No internet required once data is ingested

---

**Need Help?** Check the main [README.md](README.md) for full documentation.
