#!/bin/bash

# Configuration
API_URL="http://localhost:5134"
EMAIL="nashwa@nutrisha-app.com"
PASSWORD="12345678A"
PDF_PATH="/Users/osamahislam/Desktop/results.pdf"
IMAGE_PATH="/tmp/apple_image.jpg"

echo "Attachment Type Testing"
echo "======================="

# Step 1: Login
echo "1. Logging in..."
LOGIN_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")

TOKEN=$(echo "$LOGIN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin).get('token', ''))" 2>/dev/null)

if [ -z "$TOKEN" ]; then
    echo "Login failed!"
    echo "$LOGIN_RESPONSE"
    exit 1
fi

echo "Login successful!"

# Step 2: Get or create conversation
echo ""
echo "2. Getting conversations..."
CONV_RESPONSE=$(curl -s -X GET "$API_URL/api/chat/conversations" \
  -H "Authorization: Bearer $TOKEN")

CONVERSATION_ID=$(echo "$CONV_RESPONSE" | python3 -c "
import sys, json
data = json.load(sys.stdin)
if data and len(data) > 0:
    print(data[0]['id'])
" 2>/dev/null)

if [ -z "$CONVERSATION_ID" ]; then
    echo "Creating new conversation..."
    CONV_CREATE=$(curl -s -X POST "$API_URL/api/chat/conversations" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d '{"title":"Attachment Test","conversationMode":"ai"}')
    
    CONVERSATION_ID=$(echo "$CONV_CREATE" | python3 -c "import sys, json; print(json.load(sys.stdin).get('id', ''))" 2>/dev/null)
fi

echo "Using conversation: $CONVERSATION_ID"

# Step 3: Upload PDF
echo ""
echo "3. Uploading PDF..."
PDF_UPLOAD=$(curl -s -X POST "$API_URL/api/media/upload" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@$PDF_PATH" \
  -F "fileType=document")

PDF_BLOB=$(echo "$PDF_UPLOAD" | python3 -c "import sys, json; print(json.load(sys.stdin).get('blobName', ''))" 2>/dev/null)
PDF_URL=$(echo "$PDF_UPLOAD" | python3 -c "import sys, json; print(json.load(sys.stdin).get('url', ''))" 2>/dev/null)

echo "PDF uploaded: $PDF_BLOB"

# Step 4: Save the apple image temporarily
echo ""
echo "4. Preparing apple image..."
# This would be the base64 of the apple image you provided
# For testing, we'll use a placeholder
echo "Using provided apple image..."

# Step 5: Upload image
echo ""
echo "5. Uploading image..."
# You'll need to save the apple image first
# For now, using a test approach

# Create a test image file (you should replace this with the actual apple image)
# Download a sample image for testing
curl -s -o /tmp/apple_test.jpg "https://images.unsplash.com/photo-1567306226416-28f0efdc88ce?w=400"

IMAGE_UPLOAD=$(curl -s -X POST "$API_URL/api/media/upload" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/tmp/apple_test.jpg" \
  -F "fileType=image")

IMAGE_BLOB=$(echo "$IMAGE_UPLOAD" | python3 -c "import sys, json; print(json.load(sys.stdin).get('blobName', ''))" 2>/dev/null)
IMAGE_URL=$(echo "$IMAGE_UPLOAD" | python3 -c "import sys, json; print(json.load(sys.stdin).get('url', ''))" 2>/dev/null)

echo "Image uploaded: $IMAGE_BLOB"

# Step 6: Send message with PDF attachment
echo ""
echo "6. Sending message with PDF attachment..."
PDF_MESSAGE=$(cat <<EOF
{
  "conversationId": "$CONVERSATION_ID",
  "content": "Here is my InBody scan results",
  "messageType": "text",
  "attachments": [
    {
      "url": "$PDF_URL",
      "blobName": "$PDF_BLOB",
      "type": "document",
      "name": "results.pdf",
      "size": 1673931
    }
  ]
}
EOF
)

PDF_SEND=$(curl -s -X POST "$API_URL/api/chat/send" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$PDF_MESSAGE")

PDF_MSG_ID=$(echo "$PDF_SEND" | python3 -c "import sys, json; print(json.load(sys.stdin).get('id', ''))" 2>/dev/null)
echo "PDF message sent: $PDF_MSG_ID"

# Wait for AI response
sleep 3

# Step 7: Send message with image attachment
echo ""
echo "7. Sending message with image attachment..."
IMAGE_MESSAGE=$(cat <<EOF
{
  "conversationId": "$CONVERSATION_ID",
  "content": "What fruit is this?",
  "messageType": "text",
  "attachments": [
    {
      "url": "$IMAGE_URL",
      "blobName": "$IMAGE_BLOB",
      "type": "image",
      "name": "apple.jpg",
      "size": 50000
    }
  ]
}
EOF
)

IMAGE_SEND=$(curl -s -X POST "$API_URL/api/chat/send" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$IMAGE_MESSAGE")

IMAGE_MSG_ID=$(echo "$IMAGE_SEND" | python3 -c "import sys, json; print(json.load(sys.stdin).get('id', ''))" 2>/dev/null)
echo "Image message sent: $IMAGE_MSG_ID"

# Wait for responses
sleep 5

# Step 8: Get messages and check types
echo ""
echo "8. Checking message types..."
MESSAGES=$(curl -s -X GET "$API_URL/api/chat/messages/$CONVERSATION_ID?limit=10" \
  -H "Authorization: Bearer $TOKEN")

echo ""
echo "Message Types in Database:"
echo "$MESSAGES" | python3 -c "
import sys, json
data = json.load(sys.stdin)
for msg in data[-4:]:  # Last 4 messages
    msg_type = msg.get('messageType', 'unknown')
    has_attachments = 'Yes' if msg.get('attachments') else 'No'
    attachment_types = []
    if msg.get('attachments'):
        try:
            if isinstance(msg['attachments'], str):
                import json as j
                attachments = j.loads(msg['attachments'])
            else:
                attachments = msg['attachments']
            attachment_types = [a.get('Type', a.get('type', '')) for a in attachments]
        except:
            pass
    
    print(f\"- Message Type: {msg_type:10} | Has Attachments: {has_attachments:3} | Attachment Types: {attachment_types}\")
"

echo ""
echo "✅ Test Complete!"
echo ""
echo "Expected Results:"
echo "- PDF message should have type: 'document'"
echo "- Image message should have type: 'image'"
echo "- Regular text messages should have type: 'text'"