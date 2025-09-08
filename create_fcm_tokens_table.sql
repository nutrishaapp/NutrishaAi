-- Create FCM tokens table to store Firebase Cloud Messaging tokens for each user/device
CREATE TABLE IF NOT EXISTS public.fcm_tokens (
    id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    user_id UUID NOT NULL,
    token TEXT NOT NULL,
    device_id TEXT,
    platform TEXT, -- 'ios', 'android', 'web'
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT now()
);

-- Create index on user_id for faster lookups
CREATE INDEX IF NOT EXISTS idx_fcm_tokens_user_id ON public.fcm_tokens(user_id);

-- Create index on token for faster validation
CREATE INDEX IF NOT EXISTS idx_fcm_tokens_token ON public.fcm_tokens(token);

-- Create index on active tokens
CREATE INDEX IF NOT EXISTS idx_fcm_tokens_active ON public.fcm_tokens(user_id, is_active) WHERE is_active = true;

-- Create unique constraint on user_id + device_id to prevent duplicate tokens for same device
CREATE UNIQUE INDEX IF NOT EXISTS idx_fcm_tokens_user_device 
ON public.fcm_tokens(user_id, device_id) 
WHERE device_id IS NOT NULL AND is_active = true;

-- Add foreign key constraint to link with auth.users (Supabase auth table)
ALTER TABLE public.fcm_tokens 
ADD CONSTRAINT fk_fcm_tokens_user_id 
FOREIGN KEY (user_id) REFERENCES auth.users(id) ON DELETE CASCADE;

-- Enable Row Level Security (RLS)
ALTER TABLE public.fcm_tokens ENABLE ROW LEVEL SECURITY;

-- Create policies for RLS
-- Users can only see their own tokens
CREATE POLICY "Users can view own fcm tokens" ON public.fcm_tokens
    FOR SELECT USING (auth.uid() = user_id);

-- Users can insert their own tokens
CREATE POLICY "Users can insert own fcm tokens" ON public.fcm_tokens
    FOR INSERT WITH CHECK (auth.uid() = user_id);

-- Users can update their own tokens
CREATE POLICY "Users can update own fcm tokens" ON public.fcm_tokens
    FOR UPDATE USING (auth.uid() = user_id);

-- Users can delete their own tokens
CREATE POLICY "Users can delete own fcm tokens" ON public.fcm_tokens
    FOR DELETE USING (auth.uid() = user_id);

-- Service role can access all tokens (for admin operations)
CREATE POLICY "Service role can access all fcm tokens" ON public.fcm_tokens
    FOR ALL USING (auth.jwt() ->> 'role' = 'service_role');

-- Create a function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION public.handle_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at timestamp
DROP TRIGGER IF EXISTS trigger_fcm_tokens_updated_at ON public.fcm_tokens;
CREATE TRIGGER trigger_fcm_tokens_updated_at
    BEFORE UPDATE ON public.fcm_tokens
    FOR EACH ROW EXECUTE FUNCTION public.handle_updated_at();

-- Insert sample data (optional, for testing)
-- INSERT INTO public.fcm_tokens (user_id, token, device_id, platform) VALUES
-- ('sample-user-id', 'sample-fcm-token-123', 'device-123', 'android');

COMMENT ON TABLE public.fcm_tokens IS 'Stores Firebase Cloud Messaging tokens for push notifications';
COMMENT ON COLUMN public.fcm_tokens.user_id IS 'Reference to auth.users.id';
COMMENT ON COLUMN public.fcm_tokens.token IS 'FCM registration token from client device';
COMMENT ON COLUMN public.fcm_tokens.device_id IS 'Unique device identifier (optional)';
COMMENT ON COLUMN public.fcm_tokens.platform IS 'Device platform: ios, android, or web';
COMMENT ON COLUMN public.fcm_tokens.is_active IS 'Whether this token is still valid and active';