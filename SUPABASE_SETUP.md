# Supabase Database Setup Instructions

## Quick Setup Steps

1. **Open Supabase SQL Editor**
   - Go to: https://tktwsanbheqvbiubmbqe.supabase.co
   - Navigate to **SQL Editor** in the left sidebar

2. **Copy and Paste SQL**
   - Copy the entire contents of `NutrishaAI.API/Database/supabase_schema.sql`
   - Paste it into the SQL editor
   - Click **Run** button

3. **Verify Tables Created**
   - Go to **Table Editor** in the left sidebar
   - You should see all these tables:
     - users
     - conversations
     - messages
     - media_attachments
     - patient_health_data
     - health_metrics
     - meal_logs
     - diet_plans
     - pinned_plans
     - api_keys

## Alternative: Using Supabase CLI

If you have Supabase CLI installed:

```bash
# Install Supabase CLI (if not installed)
brew install supabase/tap/supabase

# Link to your project
supabase link --project-ref tktwsanbheqvbiubmbqe

# Run migrations
supabase db push NutrishaAI.API/Database/supabase_schema.sql
```

## Enable Row Level Security (RLS)

After creating tables, enable RLS for security:

1. Go to **Authentication** → **Policies**
2. For each table, click **Enable RLS**
3. Add appropriate policies for your use case

## Test the Connection

Once tables are created, restart your API:

```bash
cd NutrishaAI.API
dotnet run
```

Your signup/signin forms should now work with the real Supabase backend!

## Troubleshooting

If you encounter issues:

1. **Check Supabase Logs**
   - Go to **Logs** → **Database** in Supabase dashboard
   - Look for any error messages

2. **Verify Authentication is Enabled**
   - Go to **Authentication** → **Providers**
   - Make sure **Email** provider is enabled

3. **Check API Settings**
   - Ensure your API URL and keys are correct in `appsettings.json`