-- Create a test user directly in the database
-- Note: This creates a user in public.users but NOT in auth.users
-- For a complete user, you need to create via Supabase Auth

-- Check if test user exists in auth.users
SELECT id, email, created_at, confirmed_at 
FROM auth.users 
WHERE email = 'demo@test.com';

-- If you want to manually confirm the user:
UPDATE auth.users 
SET email_confirmed_at = NOW(), 
    confirmed_at = NOW() 
WHERE email = 'demo@test.com';

-- Check the user in public.users table too
SELECT * FROM public.users WHERE email = 'demo@test.com';