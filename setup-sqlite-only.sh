#!/bin/bash

# SQLite-Only Quick Start Script
# Run this to set up and start the query router with SQLite only (no Docker)

set -e  # Exit on error

echo "🚀 SQLite-Only Query Router Setup"
echo "=================================="
echo ""

# Step 1: Check if we're in the right directory
if [ ! -f "QueryRouter.sln" ]; then
    echo "❌ Error: QueryRouter.sln not found"
    echo "Please run this script from the router-data directory"
    exit 1
fi

# Step 2: Restore dependencies
echo "📦 Restoring .NET dependencies..."
dotnet restore
echo "✅ Dependencies restored"
echo ""

# Step 3: Ingest data into SQLite
echo "💾 Ingesting data into SQLite..."
cd DataIngestion.CLI
dotnet run -- ingest-sql

if [ $? -ne 0 ]; then
    echo "❌ Data ingestion failed"
    exit 1
fi

echo "✅ Data ingestion complete"
echo ""

# Step 4: Check if database was created
if [ ! -f "../QueryRouter.API/pos_requirements.db" ]; then
    echo "❌ Error: Database file not created"
    exit 1
fi

DB_SIZE=$(ls -lh ../QueryRouter.API/pos_requirements.db | awk '{print $5}')
echo "✅ Database created: pos_requirements.db ($DB_SIZE)"
echo ""

# Step 5: Verify data
echo "🔍 Verifying database..."
REQUIREMENT_COUNT=$(sqlite3 ../QueryRouter.API/pos_requirements.db "SELECT COUNT(*) FROM Requirements;")
echo "✅ Total requirements in database: $REQUIREMENT_COUNT"
echo ""

# Step 6: Kill any existing process on port 5000
echo "🔧 Checking for existing processes on port 5000..."
if lsof -ti:5000 > /dev/null 2>&1; then
    echo "⚠️  Port 5000 is in use. Killing existing process..."
    lsof -ti:5000 | xargs kill -9
    sleep 2
    echo "✅ Port cleared"
fi
echo ""

# Step 7: Start the API
echo "🚀 Starting API server..."
echo "=================================="
echo ""
echo "API will start at: http://localhost:5000"
echo "Swagger UI: http://localhost:5000/swagger"
echo ""
echo "⚠️  You may see warnings about Qdrant/Neo4j - this is normal!"
echo "   The API will work fine with SQLite only."
echo ""
echo "Press Ctrl+C to stop the server"
echo ""

cd ../QueryRouter.API
dotnet run
