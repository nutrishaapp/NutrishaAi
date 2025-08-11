#!/bin/bash

# Supabase credentials
SUPABASE_URL="https://tktwsanbheqvbiubmbqe.supabase.co"
SERVICE_KEY="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InRrdHdzYW5iaGVxdmJpdWJtYnFlIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc1NDg5NDQ3MiwiZXhwIjoyMDcwNDcwNDcyfQ.oC6pVKT_GsHwERlO_L4kfpZ7iqmDd_tyXOLbO1wN-YI"

echo "🚀 Setting up NutrishaAI database tables in Supabase..."
echo ""

# Function to execute SQL via Supabase REST API
execute_sql() {
    local sql="$1"
    local description="$2"
    
    echo "Executing: $description"
    
    # URL encode the SQL
    encoded_sql=$(echo -n "$sql" | jq -Rs .)
    
    response=$(curl -s -X POST \
        "${SUPABASE_URL}/rest/v1/rpc/exec_sql" \
        -H "apikey: ${SERVICE_KEY}" \
        -H "Authorization: Bearer ${SERVICE_KEY}" \
        -H "Content-Type: application/json" \
        -d "{\"sql_query\": $encoded_sql}")
    
    if [[ "$response" == *"error"* ]]; then
        echo "  ❌ Failed"
        echo "  Response: $response"
    else
        echo "  ✅ Success"
    fi
    echo ""
}

# Read the SQL file and execute it
SQL_FILE="NutrishaAI.API/Database/supabase_schema.sql"

if [ ! -f "$SQL_FILE" ]; then
    echo "❌ SQL file not found: $SQL_FILE"
    exit 1
fi

# Read entire SQL file
SQL_CONTENT=$(<"$SQL_FILE")

echo "📄 Executing SQL schema from: $SQL_FILE"
echo "================================================"
echo ""

# Execute the entire schema as one command
execute_sql "$SQL_CONTENT" "Complete database schema"

echo "✨ Database setup attempt completed!"
echo ""
echo "Please check your Supabase dashboard to verify tables were created:"
echo "  ${SUPABASE_URL}"
echo ""
echo "Tables that should be created:"
echo "  - users"
echo "  - conversations"
echo "  - messages"
echo "  - media_attachments"
echo "  - patient_health_data"
echo "  - health_metrics"
echo "  - meal_logs"
echo "  - diet_plans"
echo "  - pinned_plans"
echo "  - api_keys"