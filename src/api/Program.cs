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

// --- Service Registration ---

// Supabase Client (Singleton recommended)
builder.Services.AddSingleton<Supabase.Client>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url not configured.");
    var key = config["Supabase:ApiKey"] ?? throw new InvalidOperationException("Supabase:ApiKey not configured.");
    // Consider using Service Role Key for backend operations if needed, loaded securely.
    var options = new SupabaseOptions
    {
        AutoRefreshToken = true,
        AutoConnectRealtime = false, // Enable if using Realtime features
        // Add other options as needed
    };
    return new Supabase.Client(url, key, options);
});

// Register Repositories (Scoped recommended)
builder.Services.AddScoped<IPlatformConnectionRepository, SupabasePlatformConnectionRepository>();
builder.Services.AddScoped<IMappingRepository, SupabaseMappingRepository>();
builder.Services.AddScoped<ICoreRepository, SupabaseCoreRepository>();
builder.Services.AddScoped<IUserRepository, SupabaseUserRepository>();

// Register Core Services
builder.Services.AddScoped<ISyncService, SyncService>();

// Register Infrastructure Services
builder.Services.AddHttpClient(); // Used by ShopifySharp etc.

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


// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer(); // Use this instead of AddOpenApi for minimal APIs
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "sssync API", Version = "v1" });
    // Add JWT Authentication to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "sssync API v1"));
    app.UseDeveloperExceptionPage(); // More detailed errors in dev
}
else
{
    // Add production error handling (e.g., app.UseExceptionHandler("/Error"))
    app.UseHsts(); // Enforce HTTPS in production
}

app.UseHttpsRedirection();

// --- Authentication/Authorization Middleware ---
app.UseAuthentication(); // IMPORTANT: Must come before UseAuthorization
app.UseAuthorization();


// --- Minimal API Endpoints ---

var authGroup = app.MapGroup("/api/auth").WithTags("Authentication");
var syncGroup = app.MapGroup("/api/sync").WithTags("Sync").RequireAuthorization(); // Secure sync endpoints
var connectionGroup = app.MapGroup("/api/connections").WithTags("Connections").RequireAuthorization(); // Secure connection endpoints

// --- Shopify OAuth Endpoints ---
authGroup.MapGet("/shopify/initiate", (HttpContext context, IConfiguration config) =>
{
    // TODO: Get UserId from authenticated context (e.g., context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
    string? userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var shopifyApiKey = config["ShopifyApp:ApiKey"] ?? throw new InvalidOperationException("ShopifyApp:ApiKey not configured.");
    var redirectUrl = config["ShopifyApp:DefaultRedirectUrl"] ?? throw new InvalidOperationException("ShopifyApp:DefaultRedirectUrl not configured.");
    // Define required scopes
    var scopes = new List<AuthorizationScope>() {
        AuthorizationScope.ReadProducts,
        AuthorizationScope.WriteProducts,
        AuthorizationScope.ReadInventory,
        AuthorizationScope.WriteInventory,
        AuthorizationScope.ReadLocations
        // Add other scopes as needed (Orders, Customers, etc.)
    };

    // Get shop domain from query parameter (user enters this in frontend)
    if (!context.Request.Query.TryGetValue("shop", out var shopDomain) || string.IsNullOrEmpty(shopDomain))
    {
        return Results.BadRequest("Missing 'shop' query parameter (e.g., my-store.myshopify.com).");
    }

    // Construct the authorization URL
    // NOTE: ShopifySharp's AuthorizationService might require credentials upfront.
    // We construct it manually here for simplicity in this endpoint.
    var authUrl = ShopifySharp.AuthorizationService.BuildAuthorizationUrl(scopes, shopDomain.ToString(), shopifyApiKey, redirectUrl, state: userId); // Use userId as state for verification

    _logger.LogInformation("Redirecting user {UserId} to Shopify auth for shop {ShopDomain}", userId, shopDomain);
    // Redirect the user's browser
    return Results.Redirect(authUrl.ToString());

}).WithName("InitiateShopifyAuth");


authGroup.MapGet("/shopify/callback", async (
    HttpContext context,
    IConfiguration config,
    IPlatformConnectionRepository connectionRepo,
    ILogger<Program> logger) =>
{
    // Extract parameters Shopify sends back
    string? code = context.Request.Query["code"];
    string? shop = context.Request.Query["shop"];
    string? state = context.Request.Query["state"]; // This should be the userId we sent

    // TODO: Get expected UserId from authenticated context OR handle state validation carefully
    // For now, we trust the state parameter matches the user initiating
    string? expectedUserId = state; // Simplification - requires robust validation in production

    logger.LogInformation("Received Shopify callback for shop {Shop} with code present: {HasCode}", shop, !string.IsNullOrEmpty(code));

    // Validate parameters
    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(shop) || string.IsNullOrEmpty(expectedUserId))
    {
        logger.LogError("Invalid Shopify callback parameters received. Code: {Code}, Shop: {Shop}, State: {State}", code, shop, state);
        return Results.BadRequest("Invalid callback parameters.");
    }

    var shopifyApiKey = config["ShopifyApp:ApiKey"] ?? throw new InvalidOperationException("ShopifyApp:ApiKey not configured.");
    var shopifySecret = config["ShopifyApp:ApiSecret"] ?? throw new InvalidOperationException("ShopifyApp:ApiSecret not configured.");

    try
    {
        // Exchange the authorization code for an access token
        string accessToken = await ShopifySharp.AuthorizationService.Authorize(code, shop, shopifyApiKey, shopifySecret);

        logger.LogInformation("Successfully obtained Shopify access token for user {UserId}, shop {Shop}", expectedUserId, shop);

        // Save the connection details
        var connection = new PlatformConnection
        {
            UserId = expectedUserId,
            Platform = "Shopify",
            ShopDomain = shop,
            AccessToken = accessToken, // TODO: Encrypt this token before saving!
            RefreshToken = null, // Shopify REST Admin API tokens don't typically expire/refresh this way
            ExpiresAt = null
        };
        await connectionRepo.SaveConnectionAsync(connection);

        // TODO: Redirect user back to a success page in your frontend application
        return Results.Ok($"Successfully connected Shopify store: {shop}. You can close this window.");
        // Example Redirect: return Results.Redirect("https://YOUR_FRONTEND_URL/connections?success=shopify");
    }
    catch (ShopifyException ex)
    {
        logger.LogError(ex, "Shopify API error during token exchange for shop {Shop}: {StatusCode} - {Error}", shop, ex.HttpStatusCode, ex.Message);
        return Results.Problem($"Shopify authorization failed: {ex.Message}");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Generic error during Shopify callback processing for shop {Shop}", shop);
        return Results.Problem("An unexpected error occurred during Shopify authorization.");
    }
}).WithName("HandleShopifyCallback");


// --- Connection Management Endpoints ---
connectionGroup.MapGet("/", async (HttpContext context, IPlatformConnectionRepository connectionRepo) =>
{
    string? userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    var connections = await connectionRepo.GetActiveConnectionsAsync(userId);
    // Avoid sending back sensitive tokens to the frontend
    var safeConnections = connections.Select(c => new { c.UserId, c.Platform, c.ShopDomain /* Add other safe fields */ });
    return Results.Ok(safeConnections);
}).WithName("GetUserConnections");

connectionGroup.MapDelete("/{platformName}", async (string platformName, HttpContext context, IPlatformConnectionRepository connectionRepo) =>
{
     string? userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    await connectionRepo.DeleteConnectionAsync(userId, platformName);
    return Results.NoContent(); // Success
}).WithName("DeleteUserConnection");


// --- Sync Endpoints ---
syncGroup.MapPost("/trigger", async (HttpContext context, ILogger<Program> logger, ISyncService syncService) =>
{
    string? userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

    logger.LogInformation("Manual sync trigger requested for authenticated user {UserId}", userId);
    try
    {
        // Fire-and-forget is simple but not robust. Consider Hangfire or similar for background jobs.
        _ = Task.Run(() => syncService.SynchronizeUserAsync(userId));
        // TODO: Implement status checking endpoint and return its URL
        return Results.Accepted(value: $"Sync triggered for user {userId}.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error triggering sync for user {UserId}", userId);
        return Results.Problem($"An error occurred while triggering sync for user {userId}");
    }
})
.WithName("TriggerUserSync")
.Produces(StatusCodes.Status202Accepted)
.ProducesProblem(StatusCodes.Status500InternalServerError);

// TODO: Add endpoint to check sync status (requires SyncJobs table/repo)
// syncGroup.MapGet("/status", ...)


app.Run();
