// Test file to see correct login method
// Based on Supabase C# documentation, the correct method should be:

// For Supabase.Client v0.8+
var session = await supabase.Auth.SignInWithPassword(email, password);

// For older versions
var session = await supabase.Auth.SignIn(email, password);

// Try checking what we have
// The issue might be that login is failing at Supabase level