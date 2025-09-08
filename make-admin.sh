#!/bin/bash

# Script to make a user an admin
# Usage: ./make-admin.sh <email>

if [ $# -eq 0 ]; then
    echo "Usage: ./make-admin.sh <email>"
    exit 1
fi

EMAIL=$1

# Read Supabase credentials from appsettings.json
SUPABASE_URL="https://tktwsanbheqvbiubmbqe.supabase.co"
SUPABASE_SERVICE_KEY="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InRrdHdzYW5iaGVxdmJpdWJtYnFlIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc1NDg5NDQ3MiwiZXhwIjoyMDcwNDcwNDcyfQ.oC6pVKT_GsHwERlO_L4kfpZ7iqmDd_tyXOLbO1wN-YI"

# Update user role to admin
echo "Updating user ${EMAIL} to admin role..."

curl -X PATCH \
  "${SUPABASE_URL}/rest/v1/users?email=eq.${EMAIL}" \
  -H "apikey: ${SUPABASE_SERVICE_KEY}" \
  -H "Authorization: Bearer ${SUPABASE_SERVICE_KEY}" \
  -H "Content-Type: application/json" \
  -H "Prefer: return=representation" \
  -d '{"role": "admin"}'

echo ""
echo "User ${EMAIL} has been updated to admin role (if they exist)"