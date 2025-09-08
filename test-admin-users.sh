#!/bin/bash

API_URL="http://localhost:5133"
EMAIL="nashwa@nutrisha-app.com"
PASSWORD="12345678A"

echo "================================"
echo "Testing User Management as Admin"
echo "================================"

# Step 1: Login as admin
echo -e "\n1. Logging in as admin..."
LOGIN_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")

TOKEN=$(echo $LOGIN_RESPONSE | jq -r '.token')

if [ "$TOKEN" == "null" ] || [ -z "$TOKEN" ]; then
    echo "Login failed. Response:"
    echo $LOGIN_RESPONSE | jq '.'
    exit 1
fi

echo "Successfully logged in as admin"
echo "Token obtained: ${TOKEN:0:20}..."

# Step 2: Get all users
echo -e "\n2. Getting all users..."
USERS_RESPONSE=$(curl -s -X GET "$API_URL/api/users" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json")

echo "Users list:"
echo $USERS_RESPONSE | jq '.'

# Step 3: Get user statistics
echo -e "\n3. Getting user statistics..."
STATS_RESPONSE=$(curl -s -X GET "$API_URL/api/users/stats" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json")

echo "User statistics:"
echo $STATS_RESPONSE | jq '.'

# Step 4: Search for users
echo -e "\n4. Searching for users with 'nutrisha' in email..."
SEARCH_RESPONSE=$(curl -s -X GET "$API_URL/api/users?search=nutrisha" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json")

echo "Search results:"
echo $SEARCH_RESPONSE | jq '.'

echo -e "\n================================"
echo "All tests completed!"
echo "================================"