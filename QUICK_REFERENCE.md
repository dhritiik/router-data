# Quick Reference Card - SQLite Only

## 🚀 One-Command Setup
```bash
./setup-sqlite-only.sh
```

## 📝 Manual Setup (3 Steps)
```bash
# 1. Ingest data
cd DataIngestion.CLI && dotnet run -- ingest-sql

# 2. Start API
cd ../QueryRouter.API && dotnet run

# 3. Open browser
# http://localhost:5000/swagger
```

## 🧪 Test Queries

### Count Queries
```bash
# Functional (42)
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "count functional requirements"}'

# Security (19)
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "count security requirements"}'
```

### List Queries
```bash
# List functional
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "list functional requirements"}'

# POS requirements
curl -X POST http://localhost:5000/api/QueryRouter/execute \
  -H "Content-Type: application/json" \
  -d '{"query": "requirements with functional constraint in POS"}'
```

## 🔧 Troubleshooting

### Port in use
```bash
lsof -ti:5000 | xargs kill -9
```

### Re-ingest data
```bash
cd DataIngestion.CLI
dotnet run -- ingest-sql
```

### Check database
```bash
sqlite3 QueryRouter.API/pos_requirements.db "SELECT COUNT(*) FROM Requirements;"
```

## ✅ What Works
- ✅ SQL queries
- ✅ COUNT aggregations
- ✅ Filtering by requirement_type
- ✅ System-based queries
- ✅ Routing logic testing

## ❌ What Doesn't Work (Needs Docker)
- ❌ Vector similarity search
- ❌ Graph relationship queries

## 📊 Expected Counts
- Total: 97
- Functional: 42
- Security: 19
- Non-functional: 17
- Compliance: 9
- Integration: 10

---
**Full docs**: See [SQLITE_ONLY_SETUP.md](SQLITE_ONLY_SETUP.md)
