#!/bin/bash

# Configuration
API_URL="http://localhost:5134"
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiJjOWUyZjQ1ZC1kNjBiLTRiNDEtYmNkYS0yOWNjYTk4Zjk0OTEiLCJlbWFpbCI6Im5hc2h3YUBudXRyaXNoYS1hcHAuY29tIiwicm9sZSI6ImFkbWluIiwic3ViIjoiYzllMmY0NWQtZDYwYi00YjQxLWJjZGEtMjljY2E5OGY5NDkxIiwianRpIjoiMjA2MDlhYmUtMGIyYS00ZmJmLTkwMDktZmViM2JmN2FmNzNlIiwiaWF0IjoxNzU3MjU5NjMwLCJuYmYiOjE3NTcyNTk2MzAsImV4cCI6MTc1NzI2MzIzMCwiaXNzIjoiTnV0cmlzaGFBSSJ9.Jz0po-cN8eo4uVWCaeJxWkNigbCYx3S12JSDeS2A26U"
CONVERSATION_ID="d59a837d-5440-42f1-bb02-de6b5b1db458"
IMAGE_PATH="/Users/osamahislam/Library/CloudStorage/OneDrive-Sina/Sina/Marketing/Resources/Branding/app stores logos.jpg"

echo "Testing Image Upload Feature"
echo "============================"

# Step 1: Upload image
echo "1. Uploading image..."
UPLOAD_RESPONSE=$(curl -s -X POST "$API_URL/api/media/upload" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@$IMAGE_PATH" \
  -F "fileType=image")

echo "Upload Response:"
echo "$UPLOAD_RESPONSE" | python3 -m json.tool

# Extract blob name and URL from response
BLOB_NAME=$(echo "$UPLOAD_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin).get('blobName', ''))")
BLOB_URL=$(echo "$UPLOAD_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin).get('url', ''))")

if [ -z "$BLOB_NAME" ]; then
    echo "Error: Failed to upload image"
    exit 1
fi

echo ""
echo "Blob Name: $BLOB_NAME"
echo "Blob URL: $BLOB_URL"

# Step 2: Send message with attachment
echo ""
echo "2. Sending message with attachment..."

MESSAGE_BODY=$(cat <<EOF
{
  "conversationId": "$CONVERSATION_ID",
  "content": "What can you tell me about this logo image?",
  "messageType": "mixed",
  "attachments": [
    {
      "url": "$BLOB_URL",
      "blobName": "$BLOB_NAME",
      "type": "image",
      "name": "app stores logos.jpg",
      "size": 100000
    }
  ]
}
EOF
)

SEND_RESPONSE=$(curl -s -X POST "$API_URL/api/chat/send" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$MESSAGE_BODY")

echo "Send Message Response:"
echo "$SEND_RESPONSE" | python3 -m json.tool

# Step 3: Get messages to verify
echo ""
echo "3. Fetching conversation messages..."
sleep 2

MESSAGES_RESPONSE=$(curl -s -X GET "$API_URL/api/chat/messages/$CONVERSATION_ID" \
  -H "Authorization: Bearer $TOKEN")

echo "Messages in conversation:"
echo "$MESSAGES_RESPONSE" | python3 -m json.tool | head -50

echo ""
echo "Test completed!"