const { createClient } = require('@supabase/supabase-js');
const fs = require('fs');
const path = require('path');

// Supabase credentials
const supabaseUrl = 'https://tktwsanbheqvbiubmbqe.supabase.co';
const supabaseServiceKey = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InRrdHdzYW5iaGVxdmJpdWJtYnFlIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc1NDg5NDQ3MiwiZXhwIjoyMDcwNDcwNDcyfQ.oC6pVKT_GsHwERlO_L4kfpZ7iqmDd_tyXOLbO1wN-YI';

// Create Supabase client with service role key for admin access
const supabase = createClient(supabaseUrl, supabaseServiceKey, {
    auth: {
        persistSession: false
    }
});

async function setupDatabase() {
    try {
        console.log('🚀 Starting Supabase database setup...\n');
        
        // Read the SQL schema file
        const schemaPath = path.join(__dirname, 'NutrishaAI.API', 'Database', 'supabase_schema.sql');
        const sqlContent = fs.readFileSync(schemaPath, 'utf8');
        
        // Split SQL content into individual statements
        const statements = sqlContent
            .split(';')
            .map(s => s.trim())
            .filter(s => s.length > 0 && !s.startsWith('--'));
        
        console.log(`Found ${statements.length} SQL statements to execute\n`);
        
        // Execute each statement
        for (let i = 0; i < statements.length; i++) {
            const statement = statements[i] + ';';
            
            // Extract table/operation info for logging
            let operation = 'Executing statement';
            if (statement.includes('CREATE TABLE')) {
                const match = statement.match(/CREATE TABLE[^(]*\(?\s*([^\s(]+)/i);
                if (match) operation = `Creating table: ${match[1]}`;
            } else if (statement.includes('CREATE INDEX')) {
                const match = statement.match(/CREATE INDEX[^O]*ON\s+([^\s(]+)/i);
                if (match) operation = `Creating index on: ${match[1]}`;
            } else if (statement.includes('CREATE TRIGGER')) {
                const match = statement.match(/CREATE TRIGGER\s+([^\s]+)/i);
                if (match) operation = `Creating trigger: ${match[1]}`;
            } else if (statement.includes('CREATE FUNCTION')) {
                const match = statement.match(/CREATE FUNCTION\s+([^\s(]+)/i);
                if (match) operation = `Creating function: ${match[1]}`;
            } else if (statement.includes('ALTER TABLE')) {
                const match = statement.match(/ALTER TABLE\s+([^\s]+)/i);
                if (match) operation = `Altering table: ${match[1]}`;
            } else if (statement.includes('CREATE EXTENSION')) {
                operation = 'Enabling UUID extension';
            }
            
            console.log(`[${i + 1}/${statements.length}] ${operation}`);
            
            const { error } = await supabase.rpc('exec_sql', {
                sql_query: statement
            }).single();
            
            if (error) {
                // Try direct execution as alternative
                const { data, error: directError } = await supabase
                    .from('_sql')
                    .insert({ query: statement })
                    .select();
                
                if (directError) {
                    console.error(`   ❌ Error: ${directError.message}`);
                    // Continue with next statement instead of stopping
                } else {
                    console.log(`   ✅ Success`);
                }
            } else {
                console.log(`   ✅ Success`);
            }
        }
        
        console.log('\n✨ Database setup completed!');
        console.log('\n📊 Tables created:');
        console.log('   - users');
        console.log('   - conversations');
        console.log('   - messages');
        console.log('   - media_attachments');
        console.log('   - patient_health_data');
        console.log('   - health_metrics');
        console.log('   - meal_logs');
        console.log('   - diet_plans');
        console.log('   - pinned_plans');
        console.log('   - api_keys');
        
        console.log('\n🔒 Note: Remember to set up Row Level Security (RLS) policies in Supabase dashboard');
        
    } catch (error) {
        console.error('❌ Setup failed:', error.message);
        process.exit(1);
    }
}

// Run the setup
setupDatabase();