#!/bin/bash

echo "=== NutrishaAI Prompt Extraction Tool ==="
echo "======================================="
echo

echo "1. NUTRITIONIST SYSTEM PROMPT (from SimpleGeminiService.cs:105-127):"
echo "-------------------------------------------------------------------"
grep -A 25 'var systemPrompt = @"' /Users/osamahislam/Documents/NutrishaAi/NutrishaAI.API/Services/SimpleGeminiService.cs | head -25
echo

echo "2. IMAGE ANALYSIS PROMPT (from SimpleGeminiService.cs:194-204):"
echo "----------------------------------------------------------------"
grep -A 15 'var prompt = @' /Users/osamahislam/Documents/NutrishaAi/NutrishaAI.API/Services/SimpleGeminiService.cs | grep -A 15 "professional nutritionist analyzing food images"
echo

echo "3. HEALTH DATA EXTRACTION PROMPT (from SimpleGeminiService.cs:266-280):"
echo "------------------------------------------------------------------------"
grep -A 20 'Extract health and nutrition data' /Users/osamahislam/Documents/NutrishaAi/NutrishaAI.API/Services/SimpleGeminiService.cs
echo

echo "4. API CONFIGURATION:"
echo "--------------------"
echo "- Model: gemini-1.5-flash (configurable via Gemini:Model)"
echo "- Base URL: https://generativelanguage.googleapis.com/v1beta"
echo "- Temperature: 0.7"
echo "- Max Output Tokens: 8192"
echo