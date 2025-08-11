-- Fix RLS policies for users table to allow inserts during registration

-- Option 1: Add policy to allow users to insert their own record
CREATE POLICY "Users can insert their own profile on signup" ON public.users
    FOR INSERT WITH CHECK (auth.uid() = id);

-- Option 2: Allow service role to bypass RLS (for server-side operations)
CREATE POLICY "Service role can do anything" ON public.users
    USING (auth.jwt() ->> 'role' = 'service_role')
    WITH CHECK (auth.jwt() ->> 'role' = 'service_role');

-- Option 3: Temporarily disable RLS (for testing only)
-- ALTER TABLE public.users DISABLE ROW LEVEL SECURITY;

-- Check if policies exist
SELECT tablename, policyname, permissive, roles, cmd, qual, with_check 
FROM pg_policies 
WHERE tablename = 'users';