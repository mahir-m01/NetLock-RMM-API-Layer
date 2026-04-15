// ─────────────────────────────────────────────────────────────────────────────
// Program.cs — Application entry point and DI/middleware configuration
//
// This is the ASP.NET Core "Minimal Hosting" model (introduced in .NET 6).
// Unlike older .NET apps, there's no Startup.cs or explicit Main() method.
// The top-level statements here ARE the Main() method.
//
// Order is critical:
//   1. Static settings (SqlMapper) BEFORE var builder = ...
//   2. DI registrations BEFORE var app = builder.Build()
//   3. Middleware pipeline AFTER var app = builder.Build()
//   4. Schema validation BEFORE endpoint registration
//   5. Endpoint registration BEFORE await app.RunAsync()
// ─────────────────────────────────────────────────────────────────────────────

using Dapper;
using ControlIT.Api.Application;
using ControlIT.Api.Common;
using ControlIT.Api.Domain.Interfaces;
using ControlIT.Api.Endpoints;
using ControlIT.Api.Infrastructure.NetLock;
using ControlIT.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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

// ── CORS ────────────────────────────────────────────────────────────────────
// Allow the dashboard (React/Next.js) to make requests to this API.
// AllowedOrigins is read from config so it can differ per environment.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
                ?? ["http://localhost:3000"])
              .AllowAnyHeader()
              .AllowAnyMethod();
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
// Equivalent to reading config["NetLock:HubUrl"] but with a typed class.
// IOptions<NetLockOptions> can then be injected into any service that needs these values.
builder.Services.Configure<ControlIT.Api.Common.Configuration.NetLockOptions>(
    builder.Configuration.GetSection("NetLock"));
builder.Services.Configure<ControlIT.Api.Common.Configuration.NetbirdOptions>(
    builder.Configuration.GetSection("Netbird"));
builder.Services.Configure<ControlIT.Api.Common.Configuration.WazuhOptions>(
    builder.Configuration.GetSection("Wazuh"));
builder.Services.Configure<ControlIT.Api.Common.Configuration.DatabaseOptions>(
    builder.Configuration.GetSection("Database"));

// ── Infrastructure — Singleton ───────────────────────────────────────────
// Singleton = one instance for the entire application lifetime.
// WHY: IDbConnectionFactory only holds a connection string (thread-safe).
// It creates short-lived connections on demand — the factory itself stays alive.
builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();

// ── Infrastructure — Scoped ──────────────────────────────────────────────
// Scoped = one instance per HTTP request. Each request gets its own repository,
// which in turn creates its own DB connection. Connections are disposed at end of request.
builder.Services.AddScoped<IDeviceRepository, MySqlDeviceRepository>();
builder.Services.AddScoped<IEventRepository, MySqlEventRepository>();
builder.Services.AddScoped<ITenantRepository, MySqlTenantRepository>();

// ── Application — Scoped services ────────────────────────────────────────
builder.Services.AddScoped<IAuditService, AuditService>();

// AuditRepository is registered directly (no interface) because only AuditService uses it.
builder.Services.AddScoped<AuditRepository>();

builder.Services.AddScoped<IEndpointProvider, NetLockEndpointProvider>();
builder.Services.AddScoped<ICommandDispatcher, SignalRCommandDispatcher>();

// ── Application — Scoped: ControlItFacade MUST be Scoped, NOT Singleton ──
// Registering ControlItFacade as Singleton creates a "captive dependency" bug:
// it would capture the Scoped repositories from the FIRST request and reuse
// them for ALL subsequent requests — causing tenant data leakage.
builder.Services.AddScoped<ControlItFacade>();
builder.Services.AddScoped<TenantContext>();

// NotificationFactory is an instance class (NOT static) — must be Scoped for DI injection.
builder.Services.AddScoped<NotificationFactory>();

// ── Infrastructure — Singleton hosted service for SignalR connection ──────
// WHY Singleton: The SignalR connection is process-wide. One connection handles
// all tenants' commands. If Scoped, each request would create a new connection
// and the response correlation would break (responses go to wrong connections).
builder.Services.AddSingleton<NetLockSignalRService>();

// AddHostedService registers NetLockSignalRService as an IHostedService.
// We use the existing Singleton instance (not create a second one) to avoid
// having two instances with two separate _pendingCommands dictionaries.
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
    // UseMySql with ServerVersion.AutoDetect: Pomelo queries the server version on first connect.
    // This ensures the correct MySQL syntax is used (MySQL 8.x vs 5.7, MariaDB, etc.)
    options.UseMySql(cs, ServerVersion.AutoDetect(cs));
});

// ── Health checks ─────────────────────────────────────────────────────────
// Built-in ASP.NET Core health check system. Results are reported at GET /healthz
// (separate from the custom /health endpoint which adds Netbird checks).
// Note: the custom /health endpoint in HealthEndpoints.cs is the primary health check
// endpoint — this provides the ASP.NET built-in system for framework integrations.
builder.Services.AddHealthChecks();

// ── HTTP clients ──────────────────────────────────────────────────────────
// IHttpClientFactory manages HttpClient instances — avoids socket exhaustion
// that occurs when creating new HttpClient() instances per request.
// AddHttpClient<INetbirdClient, NetbirdApiClient> registers a typed client.
builder.Services.AddHttpClient<INetbirdClient, ControlIT.Api.Infrastructure.Netbird.NetbirdApiClient>();

// Named clients for notification channels.
// NotificationFactory.Create() calls _httpFactory.CreateClient("teams") / ("webhook").
builder.Services.AddHttpClient("teams");
builder.Services.AddHttpClient("webhook");

// ── Build the application ────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware pipeline — order is critical ───────────────────────────────────
// Each middleware wraps the next one. The order here determines processing order.
// Think of it as nested functions: ErrorHandling(Cors(Auth(RateLimit(endpoint)))).

app.UseMiddleware<ErrorHandlingMiddleware>();     // 1. Catch all unhandled exceptions
// WHY first: if anything in the pipeline throws, this catches it and returns a clean JSON error.

app.UseCors();                                   // 2. CORS before auth
// WHY before auth: preflight OPTIONS requests must succeed without an API key.

app.UseMiddleware<ApiKeyMiddleware>();            // 3. Auth + tenant derivation
// Sets TenantContext.TenantId from DB lookup. /health is exempt.

app.UseRateLimiter();                            // 4. Rate limiting after auth
// WHY after auth: rate limit per authenticated client, not per IP.
// ─────────────────────────────────────────────────────────────────────────────

// ── Schema validation at startup ─────────────────────────────────────────────
// Run NetLockSchemaValidator before the API accepts any traffic.
// If any required Dapper column is missing from NetLock's schema, this throws
// InvalidOperationException and prevents the host from reaching app.Run().
// WHY: better to crash loudly at startup than to silently return null data.
//
// CreateScope() is needed because ISchemaValidator is Singleton — we can resolve
// it directly from the root service provider. But using a scope is the safe pattern
// and works regardless of whether the registration is Scoped or Singleton.
using (var scope = app.Services.CreateScope())
{
    var validator = scope.ServiceProvider.GetRequiredService<ISchemaValidator>();
    await validator.ValidateRequiredColumnsAsync();
}
// ─────────────────────────────────────────────────────────────────────────────

// ── Endpoint registration ─────────────────────────────────────────────────────
// Each group registers its routes via the static Map(app) method.
// This pattern (static Map method, not controller classes) is called "Minimal API".
// It's lighter than MVC controllers and avoids reflection-based attribute routing.
DeviceEndpoints.Map(app);
EventEndpoints.Map(app);
TenantEndpoints.Map(app);
CommandEndpoints.Map(app);
DashboardEndpoints.Map(app);
AuditEndpoints.Map(app);
HealthEndpoints.Map(app);
IntegrationEndpoints.Map(app);
// ─────────────────────────────────────────────────────────────────────────────

await app.RunAsync();
