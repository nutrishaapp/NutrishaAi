#!/bin/bash

# Test script to verify vector message saving is working
API_URL="http://localhost:5134"

echo "🧪 Testing Vector Message Storage Integration"
echo "============================================="

# Test JWT token (replace with a valid one for your system)
JWT_TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiJjOWUyZjQ1ZC1kNjBiLTRiNDEtYmNkYS0yOWNjYTk4Zjk0OTEiLCJlbWFpbCI6Im5hc2h3YUBudXRyaXNoYS1hcHAuY29tIiwicm9sZSI6ImFkbWluIiwic3ViIjoiYzllMmY0NWQtZDYwYi00YjQxLWJjZGEtMjljY2E5OGY5NDkxIiwianRpIjoiMmYyMzA0YWEtYTBmNi00YjE3LTljOWMtYTUxZTIyY2MxNDEyIiwiaWF0IjoxNzU3MzQ5MjQzLCJuYmYiOjE3NTczNDkyNDMsImV4cCI6MTc1NzM1Mjg0MywiaXNzIjoiTnV0cmlzaGFBSSJ9.2xPkWZIwBvq_ucCJ27RhJqP-o6fW9bt65OJZ-jOG8RA"

# Test conversation ID (use an existing one or create new)
CONVERSATION_ID="123e4567-e89b-12d3-a456-426614174000"

echo "📤 Sending test message with health information..."

# Send a message with important health information that should be saved
RESPONSE=$(curl -s -X POST "$API_URL/api/chat/send" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -d '{
    "conversationId": "'$CONVERSATION_ID'",
    "content": "Hi! I have type 2 diabetes and I am allergic to shellfish and nuts. I am trying to lose 20 pounds and I exercise 3 times a week. My goal is to improve my blood sugar control and reduce my weight.",
    "messageType": "text",
    "attachments": []
  }')

echo "📨 Response:"
echo "$RESPONSE" | jq '.' 2>/dev/null || echo "$RESPONSE"

echo ""
echo "🔍 Check the server logs for memory extraction and storage messages:"
echo "   - Look for: 'Saving memory for user'"
echo "   - Look for: 'Memory stored successfully in Qdrant'"
echo ""
echo "🌐 You can also check your Qdrant Cloud dashboard to see if the memory was stored:"
echo "   https://cloud.qdrant.io/dashboard"