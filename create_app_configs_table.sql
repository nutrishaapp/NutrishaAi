-- Create app_configs table for dynamic prompt management
CREATE TABLE app_configs (
    key VARCHAR(255) PRIMARY KEY,
    value TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_by UUID REFERENCES auth.users(id) ON DELETE SET NULL
);

-- Add indexes for better performance
CREATE INDEX idx_app_configs_updated_at ON app_configs(updated_at);
CREATE INDEX idx_app_configs_updated_by ON app_configs(updated_by);

-- Add comments for documentation
COMMENT ON TABLE app_configs IS 'Dynamic configuration key-value store for application settings like AI prompts';
COMMENT ON COLUMN app_configs.key IS 'Unique configuration key (e.g., nutritionist_system_prompt)';
COMMENT ON COLUMN app_configs.value IS 'Configuration value (can store large text like prompts)';
COMMENT ON COLUMN app_configs.created_at IS 'When the configuration was first created';
COMMENT ON COLUMN app_configs.updated_at IS 'When the configuration was last updated';
COMMENT ON COLUMN app_configs.updated_by IS 'UUID of the user who last updated this configuration';

-- Create a trigger to automatically update the updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_app_configs_updated_at 
    BEFORE UPDATE ON app_configs 
    FOR EACH ROW 
    EXECUTE FUNCTION update_updated_at_column();

-- Insert initial seed data with existing prompts
INSERT INTO app_configs (key, value, updated_by) VALUES 
(
    'nutritionist_system_prompt',
    'You are NutrishaAI, a professional AI nutritionist and health coach. You provide personalized nutrition advice, meal planning, and health guidance.

Your expertise includes:
- Nutritional analysis and meal planning
- Dietary restrictions and allergies management
- Weight management strategies
- Sports nutrition
- Health condition-specific diets (diabetes, heart disease, etc.)
- Food science and nutrient interactions
- Behavioral nutrition coaching

Guidelines:
- Always provide evidence-based advice
- Ask clarifying questions when needed
- Be encouraging and supportive
- Provide practical, actionable recommendations
- Consider individual needs, preferences, and constraints
- Mention when medical consultation is recommended
- Keep responses conversational but professional
- Include specific examples and portion sizes when relevant

Current conversation context: {conversationContext}',
    NULL
),
(
    'image_analysis_prompt',
    'You are a professional nutritionist analyzing food images.
                
Analyze this image and provide:
1. Food identification and portion estimation
2. Nutritional analysis (calories, macros, etc.)
3. Health recommendations
4. Any dietary concerns or benefits
                
{textPrompt}
                
[Image data provided as base64]',
    NULL
),
(
    'health_data_extraction_prompt',
    'Extract health and nutrition data from the following content.
                
Content Type: {contentType}
Content: {content}
                
Extract and return in JSON format:
- foods: array of {name, calories, servingSize}
- exercises: array of {activity, durationMinutes, intensity}
- symptoms: array of strings
- dietary_restrictions: array of strings
- health_goals: array of strings
- measurements: object with key-value pairs
- summary: brief text summary
                
If no relevant data is found, return empty arrays/objects.',
    NULL
),
(
    'error_response_message',
    'I apologize, but I''m experiencing technical difficulties. Please try again later.',
    NULL
),
(
    'voice_processing_prompt',
    'You are a professional nutritionist processing audio content.
                
{textPrompt}
                
Note: Audio transcription is not yet implemented. Please provide nutrition advice based on this audio.',
    NULL
);

-- Enable Row Level Security (RLS) if needed
ALTER TABLE app_configs ENABLE ROW LEVEL SECURITY;

-- Create policy to allow admins to manage configs
CREATE POLICY "Admin users can manage app configs" ON app_configs
    FOR ALL USING (
        EXISTS (
            SELECT 1 FROM users 
            WHERE users.id = auth.uid() 
            AND users.role = 'admin'
        )
    );

-- Create policy to allow service account access (for the API)
CREATE POLICY "Service account can read app configs" ON app_configs
    FOR SELECT USING (true);