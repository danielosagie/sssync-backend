using Microsoft.AspNetCore.Authentication.JwtBearer; // For JWT Auth
using Microsoft.IdentityModel.Tokens; // For JWT Auth
using Microsoft.OpenApi.Models; // For Swagger Auth UI
using Supabase; // For Supabase client
using sssync_backend.core.Interfaces;
using sssync_backend.core.Services;
using sssync_backend.infrastructure.Shopify;
using sssync_backend.workers;
using sssync_backend.core.Interfaces.Repositories; // Add repository interfaces
using sssync_backend.infrastructure.Persistence; // Add repository implementations
using System.Text; // For JWT Key encoding
using ShopifySharp.Enums; // For Shopify Scopes
using ShopifySharp.Infrastructure; // For RequestEngine
using sssync_backend.core.Interfaces.Repositories;
using sssync_backend.infrastructure.Persistence.Repositories;
using sssync_backend.core.Interfaces.Services;
using sssync_backend.infrastructure.Services;
using sssync_backend.core.Interfaces.Platform;
using sssync_backend.infrastructure.Platform;
using sssync_backend.infrastructure.Platform.Shopify;
using MediatR;
using System.Reflection;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
// Ensure you have appsettings.json and User Secrets configured
// Example appsettings.json structure:
/*
{
  "Supabase": {
    "Url": "YOUR_SUPABASE_URL",
    "ApiKey": "YOUR_SUPABASE_ANON_KEY" // Use Anon key here, Service key if needed via User Secrets/Env Vars
  },
  "ShopifyApp": {
    "ApiKey": "YOUR_SHOPIFY_APP_API_KEY",
    "ApiSecret": "YOUR_SHOPIFY_APP_API_SECRET", // Keep this secret!
    "DefaultRedirectUrl": "https://YOUR_BACKEND_URL/api/auth/shopify/callback" // Your deployed backend callback URL
  },
  "Jwt": {
    "Issuer": "sssync.app",
    "Audience": "sssync.app.users",
    "Key": "YOUR_VERY_STRONG_AND_SECRET_JWT_SIGNING_KEY" // Keep this secret! Min 32 chars for HMACSHA256
  },
  "Logging": { ... }
}
*/
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(); // Store sensitive keys (Shopify Secret, JWT Key, Supabase Service Key) here during dev
}

// --- Configure Services ---

// Add Logging, Controllers, etc.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Authentication (Ensure this is set up correctly for Supabase JWTs)
// builder.Services.AddAuthentication(...) 
// builder.Services.AddAuthorization();

// Configure Options Pattern for Shopify Settings
builder.Services.Configure<ShopifyAppSettings>(
    builder.Configuration.GetSection("ShopifyApp"));

// Configure Supabase Client
// Make sure SUPABASE_URL and SUPABASE_KEY are in config (appsettings/env vars)
var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:ApiKey"];
if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
{
    builder.Services.AddSingleton(provider => 
        new Client(supabaseUrl, supabaseKey, new SupabaseOptions
        {
            AutoConnectRealtime = true // Optional: If using Realtime features
        }));
}
else
{
    // Log or handle missing Supabase configuration
    Console.WriteLine("Warning: Supabase URL or ApiKey not configured.");
}

// Configure Data Protection (Needed for EncryptionService)
// Keys might be stored ephemerally by default in development.
// For production, configure persistence (e.g., to Azure Blob Storage, Redis, DB)
// See: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview
builder.Services.AddDataProtection(); 

// Configure HttpClientFactory (for ShopifyApiClient)
builder.Services.AddHttpClient(); // Register default factory
builder.Services.AddHttpClient("ShopifyApiClient"); // Optionally configure named client here if needed

// Register Application Core Services (MediatR)
// This scans the assembly containing your handlers/commands/queries
builder.Services.AddMediatR(Assembly.GetAssembly(typeof(sssync_backend.core.Application.Handlers.InitiatePlatformConnectionHandler))); 

// Register Infrastructure Services & Repositories
builder.Services.AddScoped<IPlatformConnectionRepository, PlatformConnectionRepository>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();

// Register Platform API Clients
builder.Services.AddScoped<ShopifyApiClient>(); // Register concrete type
// Register CloverApiClient, etc. when implemented

// Register the Platform API Client Factory
builder.Services.AddScoped<IPlatformApiClientFactory, PlatformApiClientFactory>();

// Register Repositories (Scoped recommended)
builder.Services.AddScoped<IPlatformConnectionRepository, SupabasePlatformConnectionRepository>();
builder.Services.AddScoped<IMappingRepository, SupabaseMappingRepository>();
builder.Services.AddScoped<ICoreRepository, SupabaseCoreRepository>();
builder.Services.AddScoped<IUserRepository, SupabaseUserRepository>();

// Register Core Services
builder.Services.AddScoped<ISyncService, SyncService>();

// Register platform integration services
builder.Services.AddScoped<IPlatformIntegrationService, ShopifyService>();
// TODO: Add CloverService, SquareService when implemented

// Register Workers
builder.Services.AddHostedService<SyncBackgroundService>();

// --- Authentication (Supabase JWT) ---
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured.");
if (jwtKey.Length < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 characters long.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        // Supabase JWTs might have slightly different standard claims, adjust validation if needed
        // ClockSkew = TimeSpan.Zero // Consider setting for precise expiration checks
    };
    // Optional: Add event handlers for logging/debugging token validation
    // options.Events = new JwtBearerEvents { ... };
});

builder.Services.AddAuthorization(); // Add authorization services

// --- End Service Registration ---

// --- Configure Middleware Pipeline ---
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// IMPORTANT: Ensure Authentication and Authorization middleware are added correctly
// app.UseAuthentication(); 
// app.UseAuthorization();

app.MapControllers();

app.Run();
