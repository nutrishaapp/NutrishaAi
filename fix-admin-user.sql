-- First, check if the admin user exists in auth.users
SELECT id, email, created_at, confirmed_at 
FROM auth.users 
WHERE email = 'admin@nutrisha.com';

-- Copy the ID from above query and use it below
-- Replace 'YOUR-USER-ID-HERE' with the actual ID from the query above

-- Create the corresponding record in public.users
INSERT INTO public.users (id, email, full_name, role, created_at, updated_at)
SELECT 
    id,
    email,
    'Admin User',
    'admin',
    NOW(),
    NOW()
FROM auth.users 
WHERE email = 'admin@nutrisha.com'
ON CONFLICT (id) DO NOTHING;

-- Verify the user was created
SELECT * FROM public.users WHERE email = 'admin@nutrisha.com';