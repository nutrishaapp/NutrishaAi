# Qdrant Vector Database Integration with Vertex AI Embeddings

## Overview
This implementation adds memory extraction and storage capabilities to the NutrishaAI chat system using:
- **Qdrant**: Vector database for storing user conversation memories
- **Vertex AI**: Google's embedding service for generating text embeddings
- **Gemini**: For extracting important information from messages

## Architecture

### Flow Diagram
```
User Message → ChatController
                ↓
         ChatOrchestrationService
                ↓
    ┌──────────────────────────────┐
    │     Parallel Processing       │
    ├──────────────────────────────┤
    │ 1. Save to Supabase DB        │
    │ 2. Extract Memory (Gemini)    │
    │ 3. Update Timestamp            │
    └──────────────────────────────┘
                ↓
    If shouldSave = true:
    ├─→ Generate Embedding (Vertex AI)
    └─→ Store in Qdrant (Fire & Forget)
```

## Components Added

### 1. Data Models
- **ExtractedMemory**: Contains summary, shouldSave flag, topics, and metadata
- **MemoryVector**: Stores embedding with user/conversation context
- **MemorySearchResult**: Search results with relevance scores

### 2. Services

#### VertexAIEmbeddingService
- Generates 768-dimensional embeddings using `text-embedding-004` model
- Supports batch embedding generation
- Location: `/Services/VertexAIEmbeddingService.cs`

#### QdrantVectorService
- Manages vector storage in Qdrant
- Creates and maintains `user_memories` collection
- Supports user-filtered semantic search
- Location: `/Services/QdrantVectorService.cs`

#### Enhanced SimpleGeminiService
- Added `ExtractMemoryAsync` method
- Extracts important information from messages
- Returns structured JSON with shouldSave flag
- Location: `/Services/SimpleGeminiService.cs`

### 3. Integration Points

#### ChatOrchestrationService
Modified `SendUserMessageAsync` to:
- Run memory extraction in parallel with message saving
- Fire-and-forget pattern for vector storage
- Non-blocking implementation maintains response speed

## Configuration

Add to `appsettings.json`:

```json
{
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334,
    "ApiKey": "",
    "CollectionName": "user_memories"
  },
  "VertexAI": {
    "ProjectId": "your-project-id",
    "Location": "us-central1",
    "EmbeddingModel": "text-embedding-004"
  }
}
```

## Setup Instructions

### 1. Start Qdrant
```bash
docker run -p 6334:6334 -v $(pwd)/qdrant_storage:/qdrant/storage qdrant/qdrant
```

### 2. Configure Google Cloud
Ensure Google Cloud credentials are configured for Vertex AI access.

### 3. Build and Run
```bash
cd NutrishaAI.API
dotnet build
dotnet run
```

## Memory Extraction Criteria

The system extracts and saves:
- Health conditions, symptoms, medical history
- Dietary preferences, restrictions, allergies
- Personal goals and objectives
- Important personal information
- Nutrition and health preferences

Does NOT save:
- Casual greetings or small talk
- Temporary questions without context
- Information already in conversation context

## Performance Considerations

- **Parallel Processing**: Memory extraction runs alongside message saving
- **Fire & Forget**: Vector storage doesn't block the response
- **Batch Support**: Vertex AI service supports batch embeddings
- **Indexing**: Qdrant indexes on user_id, conversation_id, and created_at

## Future Enhancements

- Memory retrieval for context enhancement (not implemented yet)
- User memory management endpoints
- Memory analytics and insights
- Cross-conversation memory aggregation

## Dependencies Added

- `Qdrant.Client` v1.15.1 - Official Qdrant C# SDK
- `Google.Cloud.AIPlatform.V1` v3.42.0 - Already present, used for Vertex AI

## Testing

To test the integration:
1. Send a message with personal information
2. Check Qdrant dashboard at http://localhost:6333/dashboard
3. Verify memory extraction in logs
4. Confirm vector storage in `user_memories` collection

## Monitoring

Key log messages:
- "Saving memory for user {UserId}: {Summary}"
- "Memory stored successfully in Qdrant for user {UserId}"
- "Failed to extract or store memory for conversation {ConversationId}"