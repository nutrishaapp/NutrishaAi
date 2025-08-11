using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NutrishaAI.API.Middleware;
using NutrishaAI.API.Services;
using Serilog;
using Supabase;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/nutrisha-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with authentication
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "NutrishaAI API", 
        Version = "v1",
        Description = "AI-powered nutritionist platform API"
    });

    // Add JWT Bearer authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Add API Key authentication
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key authentication. Add X-API-Key and X-User-Id headers",
        Name = "X-API-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure Supabase
var supabaseUrl = builder.Configuration.GetConnectionString("SupabaseUrl") ?? "";
var supabaseKey = builder.Configuration.GetConnectionString("SupabaseKey") ?? "";
var supabaseServiceKey = builder.Configuration.GetConnectionString("SupabaseServiceKey") ?? "";

builder.Services.AddSingleton(provider => new Supabase.Client(
    supabaseUrl,
    supabaseKey,
    new SupabaseOptions
    {
        AutoConnectRealtime = true,
        AutoRefreshToken = true
    }));

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "MultiAuth";
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddPolicyScheme("MultiAuth", "Multiple Authentication", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // Check which authentication to use
        if (context.Request.Headers.ContainsKey(
            builder.Configuration["ApiKeySettings:HeaderName"] ?? "X-API-Key"))
        {
            return "ApiKey";
        }
        
        return JwtBearerDefaults.AuthenticationScheme;
    };
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? ""))
    };
})
.AddScheme<ApiKeyAuthOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

// Register services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "http://localhost:3000" };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NutrishaAI API V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the root
    });
}

// Global error handling
app.UseExceptionHandler("/error");

// Use Serilog request logging
app.UseSerilogRequestLogging();

// Use CORS
app.UseCors("AllowSpecificOrigins");

// Use authentication
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

// Error endpoint
app.Map("/error", () => Results.Problem());

Log.Information("NutrishaAI API starting up...");

app.Run();