// ─────────────────────────────────────────────────────────────────────────────
// Program.cs — Application entry point and DI/middleware configuration
//
// Uses the ASP.NET Core Minimal Hosting model. Order is critical:
//   1. Static settings (SqlMapper) BEFORE var builder = ...
//   2. DI registrations BEFORE var app = builder.Build()
//   3. Middleware pipeline AFTER var app = builder.Build()
//   4. Schema validation BEFORE endpoint registration
//   5. Endpoint registration BEFORE await app.RunAsync()
// ─────────────────────────────────────────────────────────────────────────────

using System.Text;
using System.Text.Json.Serialization;
using Dapper;
using ControlIT.Api.Application;
using ControlIT.Api.Domain.Models;
using ControlIT.Api.Common;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Endpoints;
using ControlIT.Api.Infrastructure.Auth;
using ControlIT.Api.Infrastructure.NetLock;
using ControlIT.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

// ── P0 FIX — must be the absolute first statement in Program.cs ──────────────
// Without this, every underscore-named column (device_name, last_access,
// tenant_id, etc.) is mapped to null/default silently with NO exception thrown.
// Dapper's default behaviour is to match property names EXACTLY — "DeviceName"
// would not match "device_name". This setting enables automatic name conversion.
// NOTE: In Dapper 2.x the setting is on DefaultTypeMap, not SqlMapper.Settings.
DefaultTypeMap.MatchNamesWithUnderscores = true;
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── Environment variable overrides ───────────────────────────────────────────
// Three explicit env vars that take precedence over appsettings values.
// Useful for CI, Docker runs, and after a NetLock container restart that
// regenerates the remote_session_token.
//
//   CONTROLIT_DB_CONNECTION     → ConnectionStrings:ControlIt
//   CONTROLIT_NETLOCK_TOKEN      → NetLock:AdminSessionToken  (remote_session_token for SignalR)
//   CONTROLIT_NETLOCK_HUB_URL    → NetLock:HubUrl
//   CONTROLIT_NETLOCK_FILES_KEY  → NetLock:FilesApiKey  (files_api_key for admin REST endpoints)
//
// Set any of these in shell before running `dotnet run` and they will win.
// e.g.  export CONTROLIT_NETLOCK_TOKEN="<new token from refresh-token.sh>"
var envOverrides = new Dictionary<string, string?>();

var dbConn = Environment.GetEnvironmentVariable("CONTROLIT_DB_CONNECTION");
if (!string.IsNullOrWhiteSpace(dbConn))
    envOverrides["ConnectionStrings:ControlIt"] = dbConn;

var netLockToken = Environment.GetEnvironmentVariable("CONTROLIT_NETLOCK_TOKEN");
if (!string.IsNullOrWhiteSpace(netLockToken))
    envOverrides["NetLock:AdminSessionToken"] = netLockToken;

var netLockHub = Environment.GetEnvironmentVariable("CONTROLIT_NETLOCK_HUB_URL");
if (!string.IsNullOrWhiteSpace(netLockHub))
    envOverrides["NetLock:HubUrl"] = netLockHub;

var netLockFilesKey = Environment.GetEnvironmentVariable("CONTROLIT_NETLOCK_FILES_KEY");
if (!string.IsNullOrWhiteSpace(netLockFilesKey))
    envOverrides["NetLock:FilesApiKey"] = netLockFilesKey;

if (envOverrides.Count > 0)
    builder.Configuration.AddInMemoryCollection(envOverrides);
// ─────────────────────────────────────────────────────────────────────────────

// ── CORS ────────────────────────────────────────────────────────────────────
// AllowedOrigins is read from config so it can differ per environment.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
                ?? ["http://localhost:3000"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Rate limiting ─────────────────────────────────────────────────────────
// Two policies:
//   "api"      — 120 requests/min — general API usage
//   "commands" — 20 requests/min  — command dispatch (stricter to prevent flooding)
// Must be registered BEFORE var app = builder.Build().
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 120;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;  // No queuing — reject immediately when over limit
    });

    options.AddFixedWindowLimiter("commands", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 20;
        limiterOptions.QueueLimit = 0;
    });

    // Return 429 Too Many Requests when rate limit is exceeded.
    options.RejectionStatusCode = 429;
});

// ── Options binding ──────────────────────────────────────────────────────
// Configure<T>() binds a configuration section to a strongly-typed options class.
// IOptions<T> is then available for injection into any service that needs these values.
builder.Services.Configure<ControlIT.Api.Common.Configuration.NetLockOptions>(
    builder.Configuration.GetSection("NetLock"));
builder.Services.Configure<ControlIT.Api.Common.Configuration.NetbirdOptions>(
    builder.Configuration.GetSection("Netbird"));
builder.Services.Configure<ControlIT.Api.Common.Configuration.WazuhOptions>(
    builder.Configuration.GetSection("Wazuh"));
builder.Services.Configure<ControlIT.Api.Common.Configuration.DatabaseOptions>(
    builder.Configuration.GetSection("Database"));

// ── HttpClient for NetLock admin API ─────────────────────────────────────
// Named client "netlockadmin" — used by NetLockAdminClient to call
// GET /admin/devices/connected. Singleton lifetime via IHttpClientFactory.
builder.Services.AddHttpClient("netlockadmin");

// WHY Singleton: NetLockAdminClient is stateless except for the base URL
// and API key, which are read once from config and never change.
builder.Services.AddSingleton<INetLockAdminClient, NetLockAdminClient>();

// ── Infrastructure — Singleton ───────────────────────────────────────────
// WHY Singleton: IDbConnectionFactory only holds a connection string (thread-safe).
// It creates short-lived connections on demand; the factory itself stays alive.
builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();

// ── Infrastructure — Scoped ──────────────────────────────────────────────
// Scoped: one instance per HTTP request. Each repository creates its own DB
// connection, which is disposed when the request ends.
builder.Services.AddScoped<IDeviceRepository, MySqlDeviceRepository>();
builder.Services.AddScoped<IEventRepository, MySqlEventRepository>();
builder.Services.AddScoped<ITenantRepository, MySqlTenantRepository>();

// ── Application — Scoped services ────────────────────────────────────────
builder.Services.AddScoped<IAuditService, AuditService>();

// AuditRepository is registered without an interface because it is an internal
// implementation detail used only by AuditService.
builder.Services.AddScoped<AuditRepository>();

builder.Services.AddScoped<IEndpointProvider, NetLockEndpointProvider>();
builder.Services.AddScoped<ICommandDispatcher, SignalRCommandDispatcher>();

// ── Application — Scoped: ControlItFacade MUST be Scoped, NOT Singleton ──
// Registering ControlItFacade as Singleton creates a "captive dependency" bug:
// it would capture the Scoped repositories from the FIRST request and reuse
// them for ALL subsequent requests — causing tenant data leakage.
builder.Services.AddScoped<ControlItFacade>();
builder.Services.AddScoped<TenantContext>();

// NotificationFactory is an instance class so it can be injected and replaced in tests.
builder.Services.AddScoped<NotificationFactory>();

// ── Infrastructure — Singleton hosted service for SignalR connection ──────
// WHY Singleton: The SignalR connection is process-wide. One connection handles
// all tenants' commands. If Scoped, each request would create a new connection
// and the response correlation would break (responses go to wrong connections).
builder.Services.AddSingleton<NetLockSignalRService>();

// Resolves the existing Singleton instance rather than creating a second one,
// which would produce two separate _pendingCommands dictionaries.
builder.Services.AddHostedService(sp => sp.GetRequiredService<NetLockSignalRService>());

// ── ISchemaValidator — Singleton ─────────────────────────────────────────
// Singleton because: IDbConnectionFactory (Singleton) and IConfiguration (Singleton)
// are its only dependencies — no captive dependency risk.
// Must be Singleton (not Scoped) because it's injected into NetLockSignalRService
// which is a Singleton. Injecting a Scoped service into a Singleton = captive dependency.
builder.Services.AddSingleton<ISchemaValidator, NetLockSchemaValidator>();

// ── EF Core — ControlIT's own tables only ────────────────────────────────
// NEVER add NetLock table models (Device, Tenant, etc.) to this context.
// EF migrations run ONLY for controlit_* tables.
builder.Services.AddDbContext<ControlItDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("ControlIt")
             ?? throw new InvalidOperationException("Connection string 'ControlIt' is required.");
    // ServerVersion.AutoDetect queries the server version on first connect,
    // ensuring the correct MySQL syntax variant is used (MySQL 8.x, 5.7, MariaDB, etc.).
    options.UseMySql(cs, ServerVersion.AutoDetect(cs));
});

// ── Auth — JWT bearer ────────────────────────────────────────────────────
// CONTROLIT_JWT_SIGNING_KEY is validated at JwtService construction time;
// we read it here too so AddJwtBearer can share the same key without a
// circular service dependency.
var jwtKey = Environment.GetEnvironmentVariable("CONTROLIT_JWT_SIGNING_KEY")
    ?? throw new InvalidOperationException(
        "CONTROLIT_JWT_SIGNING_KEY is required. Set it before starting the application.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException(
        "CONTROLIT_JWT_SIGNING_KEY must be at least 32 bytes (256 bits).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Prevents JwtSecurityTokenHandler from remapping claim names via InboundClaimTypeMap.
        // Without this, the "role" JWT claim becomes ClaimTypes.Role (long URI) in the principal,
        // making FindFirst("role") return null in HttpActorContext and policy assertions.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = JwtService.BuildValidationParameters(jwtKey);
        // Let the pipeline return 401 — endpoints call RequireAuthorization().
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync("{\"error\":\"Unauthorized\"}");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly",
        p => p.RequireRole(nameof(Role.SuperAdmin)));

    options.AddPolicy("CpAdminOrAbove",
        p => p.RequireRole(nameof(Role.SuperAdmin), nameof(Role.CpAdmin)));

    options.AddPolicy("TenantMember",
        p => p.RequireAssertion(ctx =>
        {
            // SuperAdmin and CpAdmin have cross-tenant access (no tenant_id claim required).
            // ClientAdmin and Technician must have a tenant_id claim.
            var role = ctx.User.FindFirst("role")?.Value;
            if (role is nameof(Role.SuperAdmin) or nameof(Role.CpAdmin)) return true;
            return ctx.User.HasClaim(c => c.Type == "tenant_id");
        }));

    options.AddPolicy("CanExecuteCommands",
        p => p.RequireRole(
            nameof(Role.SuperAdmin),
            nameof(Role.CpAdmin),
            nameof(Role.Technician)));
});

// ── Auth — DI registrations ───────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IActorContext, HttpActorContext>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
builder.Services.AddHostedService<BootstrapUserSeeder>();

// ── JSON — serialize enums as strings globally ────────────────────────────
// Without this, Role enum serializes as 0/1/2/3 in JSON responses.
// The frontend expects "SuperAdmin", "CpAdmin", etc. from /auth/login.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ── Health checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── HTTP clients ──────────────────────────────────────────────────────────
// IHttpClientFactory manages HttpClient lifetime and connection pooling,
// preventing socket exhaustion from per-request HttpClient instantiation.
builder.Services.AddHttpClient<INetbirdClient, ControlIT.Api.Infrastructure.Netbird.NetbirdApiClient>();

// Named clients for notification channels.
// NotificationFactory.Create() calls _httpFactory.CreateClient("teams") / ("webhook").
builder.Services.AddHttpClient("teams");
builder.Services.AddHttpClient("webhook");

// ── Build the application ────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline — order is critical ───────────────────────────────────

app.UseMiddleware<ErrorHandlingMiddleware>();     // 1. Catch all unhandled exceptions
// WHY first: wraps the entire pipeline so any downstream exception returns a clean JSON error.

app.UseCors();                                   // 2. CORS before auth
// WHY before auth: preflight OPTIONS requests must succeed without a token.

// ApiKeyMiddleware is intentionally NOT registered here (Contract 05B.3).
// The file is retained for one release as a rollback reference.

app.UseAuthentication();                         // 3. JWT bearer validation
app.UseAuthorization();                          // 4. Policy enforcement

app.UseRateLimiter();                            // 5. Rate limiting after auth
// WHY after auth: limits are applied per authenticated identity, not per IP.
// ─────────────────────────────────────────────────────────────────────────────

// ── Auto-apply EF migrations at startup (idempotent) ─────────────────────────
// MigrateAsync() applies any pending EF migrations. Already-applied migrations
// are skipped, so this is safe to run on every startup.
// Must run BEFORE schema validation so EF creates controlit_* tables first.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ControlItDbContext>();
    await db.Database.MigrateAsync();
}
// ─────────────────────────────────────────────────────────────────────────────

// ── Schema validation at startup ─────────────────────────────────────────────
// Runs NetLockSchemaValidator before the API accepts any traffic. If any required
// Dapper column is missing from NetLock's schema, this throws InvalidOperationException
// and prevents the host from reaching app.Run().
// WHY: fail-fast at startup rather than silently returning null/default data.
//
// A scope is used here as a safe pattern that works regardless of whether
// the validator is registered as Scoped or Singleton.
using (var scope = app.Services.CreateScope())
{
    var validator = scope.ServiceProvider.GetRequiredService<ISchemaValidator>();
    await validator.ValidateRequiredColumnsAsync();
}
// ─────────────────────────────────────────────────────────────────────────────

// ── Endpoint registration ─────────────────────────────────────────────────────
// Each group registers its routes via its static Map(app) method.
// This Minimal API pattern is lighter than MVC controllers and avoids
// reflection-based attribute routing.
AuthEndpoints.Map(app);
UserEndpoints.Map(app);
DeviceEndpoints.Map(app);
EventEndpoints.Map(app);
TenantEndpoints.Map(app);
CommandEndpoints.Map(app);
DashboardEndpoints.Map(app);
AuditEndpoints.Map(app);
HealthEndpoints.Map(app);
SystemHealthEndpoints.Map(app);
IntegrationEndpoints.Map(app);
// ─────────────────────────────────────────────────────────────────────────────

await app.RunAsync();
