# NutrishaAI Backend Development Action Plan

## Current Status
- ✅ .NET 8 Web API project structure created with Clean Architecture
- ✅ Supabase integration configured
- ✅ Dual authentication system (JWT + API Key) implemented
- ✅ Base controllers and entities created
- ✅ Build issues resolved (0 errors, 76 warnings)
- ✅ API running successfully on http://localhost:5133
- ✅ Swagger UI accessible at http://localhost:5133/

## Phase 1: Version Control Setup
1. **Initialize Git repository**
   - Set up local Git repository
   - Configure user settings

2. **Create .gitignore file for .NET projects**
   - Standard .NET gitignore patterns
   - Exclude bin/, obj/, .vs/, user secrets

3. **Stage and commit initial project files**
   - Add all project files to staging
   - Create initial commit with baseline code

4. **Create GitHub repository**
   - Create new repository on GitHub
   - Configure repository settings

5. **Add GitHub remote origin**
   - Link local repository to GitHub remote
   - Set up upstream tracking

6. **Push initial commit to GitHub**
   - Push main branch to GitHub
   - Verify repository sync

## Phase 2: Core Service Implementation

7. **Implement Azure Blob Storage service**
   - Create BlobStorageService in Infrastructure layer
   - Handle multimedia file uploads (images, voice notes, documents)
   - Configure connection strings and containers
   - Add file upload/download endpoints

8. **Implement Gemini AI service integration**
   - Create GeminiAIService for multimodal processing
   - Handle text, image, and voice note analysis
   - Implement nutritional analysis and recommendations
   - Add conversation context management

9. **Implement Qdrant vector database service**
   - Create QdrantService for vector operations
   - Store patient conversation embeddings
   - Implement semantic search functionality
   - Add conversation history retrieval

10. **Configure Supabase Realtime for chat functionality**
    - Set up real-time messaging channels
    - Implement chat message handling
    - Add presence tracking for online users
    - Configure message broadcasting

11. **Fix DietPlansController query filtering logic**
    - Resolve TODO: Fix query builder type issue
    - Implement proper filtering by status and user role
    - Add proper authorization checks
    - Test all CRUD operations

## Phase 3: Testing & Documentation

12. **Add comprehensive unit tests**
    - Set up test projects (xUnit)
    - Test all controllers with mock dependencies
    - Test services and business logic
    - Add integration tests for API endpoints

13. **Create API documentation**
    - Enhance Swagger documentation with examples
    - Add detailed endpoint descriptions
    - Document authentication flows
    - Create Postman collection

14. **Set up CI/CD pipeline**
    - Configure GitHub Actions workflows
    - Set up automated testing on PRs
    - Configure deployment to staging/production
    - Set up environment-specific configurations

## Technical Considerations

### Architecture
- Clean Architecture with API, Core, Infrastructure layers
- Dependency injection for all services
- Repository pattern for data access

### Security
- JWT authentication via Supabase Auth
- API Key authentication for third-party clients
- Row Level Security (RLS) policies in Supabase
- Secure handling of sensitive configuration

### Performance
- Async/await patterns throughout
- Efficient vector operations with Qdrant
- Optimized blob storage operations
- Connection pooling and caching strategies

### Monitoring
- Structured logging with Serilog
- Health checks for all external dependencies
- Performance metrics and monitoring
- Error tracking and alerting

## Dependencies to Add
- Azure.Storage.Blobs (Azure Blob Storage)
- Google.Cloud.AIPlatform.V1 (Gemini AI)
- Qdrant.Client (Vector database)
- xUnit, Moq (Testing)
- FluentAssertions (Testing)

## Configuration Required
- Azure Storage connection strings
- Google Cloud credentials for Gemini
- Qdrant cluster configuration
- Supabase Realtime settings
- Environment-specific appsettings

## Next Immediate Steps
1. Start with Git repository setup
2. Implement Azure Blob Storage service
3. Add Gemini AI integration
4. Set up Qdrant vector database
5. Test all integrations thoroughly