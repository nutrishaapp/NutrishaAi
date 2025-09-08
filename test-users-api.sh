#!/bin/bash

# Test Users API endpoints

API_URL="http://localhost:5133"
TOKEN="YOUR_AUTH_TOKEN_HERE"

echo "Testing Users API..."
echo "==================="

# Test 1: Get all users
echo -e "\n1. Getting all users:"
curl -X GET "$API_URL/api/users" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" | jq '.'

# Test 2: Get user stats
echo -e "\n2. Getting user stats:"
curl -X GET "$API_URL/api/users/stats" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" | jq '.'

# Test 3: Search users
echo -e "\n3. Searching users (with search parameter):"
curl -X GET "$API_URL/api/users?search=admin" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" | jq '.'

echo -e "\nNote: To test role updates and deletions, you need a valid user ID."
echo "You can get user IDs from the responses above."