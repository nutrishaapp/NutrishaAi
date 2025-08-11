-- Verify user exists in both auth.users and public.users tables

-- Check auth.users
SELECT 
    id,
    email,
    email_confirmed_at,
    user_metadata->>'full_name' as full_name_metadata,
    user_metadata->>'role' as role_metadata,
    user_metadata->>'phone_number' as phone_metadata,
    user_metadata->>'gender' as gender_metadata,
    created_at
FROM auth.users 
WHERE email = 'dualtest@nutrisha.com';

-- Check public.users  
SELECT 
    id,
    email,
    full_name,
    role,
    phone_number,
    gender,
    created_at
FROM public.users 
WHERE email = 'dualtest@nutrisha.com';

-- Verify IDs match between tables
SELECT 
    'auth.users' as table_name,
    id,
    email
FROM auth.users 
WHERE email = 'dualtest@nutrisha.com'
UNION ALL
SELECT 
    'public.users' as table_name,
    id,
    email  
FROM public.users 
WHERE email = 'dualtest@nutrisha.com'
ORDER BY table_name;