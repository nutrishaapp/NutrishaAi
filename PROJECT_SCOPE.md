# NutrishaAI - Project Scope

## Overview
AI-powered nutritionist platform providing real-time chat, health tracking, and personalized dietary guidance.

## Core Architecture

### Technology Stack
- **Backend Framework**: .NET 8 Web API
- **Database**: Supabase (PostgreSQL)
- **Authentication**: Supabase Auth
- **File Storage**: Azure Blob Storage
- **AI Engine**: Google Gemini 1.5 Pro (Multimodal)
- **Vector Database**: Qdrant (Patient History & Semantic Search)
- **Real-time Communication**: Supabase Realtime
- **Hosting**: Microsoft Azure

## Authentication System

### Dual Authentication Support
1. **JWT Bearer Tokens** (Primary)
   - For web and mobile applications
   - Supabase Auth integration
   - Token refresh mechanism

2. **API Key Authentication** (Secondary)
   - API Key + User ID combination
   - For third-party integrations and clinics
   - Rate limiting and permissions management

## Core Features

### 1. Chat System
- Real-time messaging using Supabase Realtime
- Multimedia messaging support:
  - Images (food photos, meal tracking)
  - Voice notes (direct Gemini processing)
  - Documents (lab reports, prescriptions)
- Azure Blob Storage for all media files
- Automatic transcription and analysis

### 2. Conversation Management
- Secure storage of complete conversation history
- Automatic extraction of health information from messages:
  - Weight, blood pressure, glucose levels
  - Symptoms and health complaints
  - Meal descriptions and nutritional intake
  - Medications mentioned
- Vector embeddings stored in Qdrant for intelligent retrieval
- Patient timeline generation

### 3. AI Integration (Google Gemini)
- Native multimodal processing (no separate transcription needed)
- Voice note analysis and transcription
- Food image recognition and nutritional analysis
- Document understanding (lab reports, prescriptions)
- Context-aware responses based on patient history
- AI Response Toggle - ability to enable/disable AI responses

### 4. Prompt Management
- Flexible prompt configuration system
- Update and refine AI behavior without code changes
- Category-based prompt organization
- Version control for prompts

### 5. Knowledge Base Integration
- Structured nutritional data repository
- FAQs for quick responses
- Vector search capabilities via Qdrant
- Evidence-based nutritional information

### 6. Manual Diet Plans
- Created directly by nutritionists (NOT AI-generated)
- Personalized meal planning
- Structured storage with metadata
- PDF export capability

### 7. Notification System
- Real-time notifications via Supabase Realtime
- New message alerts
- Appointment reminders
- Health goal updates

### 8. Pinned Plans
- Users can pin their current diet/workout plans
- Maintains contextual awareness for AI responses
- AI references pinned plans when providing feedback
- Improves personalization and relevance of suggestions

## Database Schema

### Primary Tables

```sql
-- Core Tables
users (id, email, full_name, role, created_at, updated_at)
conversations (id, user_id, nutritionist_id, status, created_at, updated_at)
messages (id, conversation_id, sender_id, content, message_type, is_ai_generated, created_at)

-- Media & Attachments
media_attachments (id, message_id, file_url, file_type, file_size, file_name, thumbnail_url, transcription, metadata, created_at)

-- Health Data
patient_health_data (id, patient_id, conversation_id, message_id, data_type, value, extracted_at, confidence_score, verified_by, qdrant_point_id, created_at)
health_metrics (id, patient_id, metric_type, value, unit, recorded_date, source, source_message_id)
meal_logs (id, patient_id, message_id, meal_type, food_items, nutritional_info, image_url, confidence_score, created_at)

-- Features
diet_plans (id, patient_id, nutritionist_id, title, description, plan_data, status, created_at, updated_at)
pinned_plans (id, user_id, plan_type, plan_id, plan_content, goals, start_date, end_date, is_active, reminder_settings, created_at, updated_at)
knowledge_base (id, category, title, content, metadata, embedding_id, created_at)
prompts (id, name, content, category, is_active, created_at, updated_at)
notifications (id, user_id, type, title, message, is_read, created_at)

-- API Management
api_keys (id, key_hash, key_prefix, name, user_id, organization_id, permissions, rate_limit, is_active, last_used_at, expires_at, created_at)
api_key_usage (id, api_key_id, endpoint, method, status_code, ip_address, user_agent, response_time_ms, created_at)
```

## API Endpoints

### Authentication
- `POST /api/auth/register` - User registration
- `POST /api/auth/login` - User login
- `POST /api/auth/refresh` - Refresh token
- `POST /api/auth/logout` - Logout

### Chat & Messaging
- `POST /api/chat/send` - Send text message
- `POST /api/chat/send-multimedia` - Send multimedia message
- `GET /api/chat/conversations` - Get user conversations
- `GET /api/chat/messages/{conversationId}` - Get conversation messages
- Supabase Realtime subscriptions for live updates

### Media Management
- `POST /api/media/get-upload-url` - Get presigned URL for upload
- `POST /api/chat/process-multimedia` - Process uploaded media
- `GET /api/media/{messageId}/attachments` - Get message attachments

### Health Data
- `GET /api/patients/{id}/health-data` - Get patient health data
- `GET /api/patients/{id}/timeline` - Get patient timeline
- `GET /api/patients/{id}/metrics` - Get specific metrics
- `POST /api/health-data/verify` - Nutritionist verification

### AI Configuration
- `GET /api/prompts` - Get all prompts
- `POST /api/prompts` - Create prompt
- `PUT /api/prompts/{id}` - Update prompt
- `POST /api/ai/toggle` - Enable/disable AI responses

### Knowledge Base
- `GET /api/knowledge` - Get knowledge articles
- `POST /api/knowledge` - Add knowledge article
- `POST /api/knowledge/search` - Semantic search

### Diet Plans
- `GET /api/dietplans` - Get diet plans
- `POST /api/dietplans` - Create manual diet plan
- `PUT /api/dietplans/{id}` - Update diet plan
- `GET /api/dietplans/{id}` - Get specific diet plan

### Pinned Plans
- `POST /api/plans/pin` - Pin a diet/workout plan
- `GET /api/users/{userId}/pinned-plans` - Get user's pinned plans
- `DELETE /api/plans/{pinnedPlanId}/unpin` - Unpin a plan

### Notifications
- `GET /api/notifications` - Get user notifications
- `PUT /api/notifications/{id}/read` - Mark as read
- Supabase Realtime channel for notifications

### API Key Management
- `POST /api/apikeys/generate` - Generate new API key
- `GET /api/apikeys` - List user's API keys
- `DELETE /api/apikeys/{keyId}` - Revoke API key

## Processing Pipeline

1. **Message Reception** → User sends text/voice/image/document
2. **Media Upload** → Store in Azure Blob Storage
3. **AI Processing** → Send to Gemini for analysis
4. **Data Extraction** → Extract health metrics, symptoms, meals
5. **Vector Storage** → Index in Qdrant with metadata
6. **Response Generation** → Context-aware AI response
7. **Real-time Delivery** → Supabase Realtime broadcast to clients

## Security & Compliance

- JWT authentication with refresh tokens
- API key authentication for integrations
- Rate limiting per API key
- Input validation and sanitization
- CORS configuration
- Azure Key Vault for secrets
- Audit logging for all health data access
- File validation (type, size limits)
- Encryption at rest and in transit

## Media Processing Specifications

### Supported File Types
- **Images**: JPEG, PNG, WebP (Max: 10MB)
- **Voice Notes**: WebM, MP3, WAV, M4A (Max: 5MB)
- **Documents**: PDF, DOCX, TXT (Max: 20MB)

### Processing Capabilities
- Direct Gemini processing (no separate transcription)
- Automatic nutritional analysis from food photos
- Health data extraction from documents
- Confidence scoring for extracted data

## Required NuGet Packages

```xml
<!-- Core -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />

<!-- Database & Storage -->
<PackageReference Include="Supabase" />
<PackageReference Include="Azure.Storage.Blobs" />
<PackageReference Include="Qdrant.Client" />

<!-- AI -->
<PackageReference Include="Google.Cloud.AIPlatform.V1" />

<!-- Utilities -->
<PackageReference Include="AutoMapper" />
<PackageReference Include="FluentValidation" />
<PackageReference Include="BCrypt.Net-Next" />
<PackageReference Include="Serilog" />
<PackageReference Include="Swashbuckle.AspNetCore" />
<PackageReference Include="Polly" />
```

## Implementation Timeline

### Week 1: Foundation
- Project setup and architecture
- Supabase database schema
- Authentication system (JWT + API Keys)
- Basic CRUD operations

### Week 2: Core Features
- Chat system with Supabase Realtime
- Azure Blob Storage integration
- Gemini AI integration
- Prompt management system

### Week 3: Advanced Features
- Qdrant vector database setup
- Health data extraction pipeline
- Knowledge base implementation
- Manual diet plans

### Week 4: Polish & Deployment
- Notification system
- Testing and optimization
- Azure deployment configuration
- API documentation

## Project Details

- **Client**: Dr. Ahmed Nowair
- **API Handover**: Nashwa
- **Duration**: 1 Month
- **Budget**: 350,000 EGP
- **Confidentiality**: Strictly confidential project

## Success Criteria

1. Fully functional REST API with all specified endpoints
2. Real-time chat with multimedia support
3. Accurate health data extraction from all message types
4. Seamless Gemini AI integration with toggle capability
5. Robust authentication supporting both JWT and API keys
6. Comprehensive API documentation
7. Deployed and tested on Microsoft Azure

---

*Document Version: 1.2*
*Last Updated: 2025-08-10*
*Changes: 
  - Added pinned_plans table for contextual AI feedback
  - Updated to use Supabase Realtime instead of SignalR*