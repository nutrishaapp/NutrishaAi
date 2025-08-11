-- Check auth.users table for user status
SELECT 
    id,
    email,
    email_confirmed_at,
    confirmed_at,
    last_sign_in_at,
    created_at
FROM auth.users
ORDER BY created_at DESC;

-- If users are unconfirmed, manually confirm them:
-- UPDATE auth.users 
-- SET email_confirmed_at = NOW(), 
--     confirmed_at = NOW() 
-- WHERE email IN ('test1@nutrisha.com', 'testing1@gmail.com');