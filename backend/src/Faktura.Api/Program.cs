using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Faktura.Api.Auth;
using Faktura.Api.Features.Articles;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Billing;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Faktura.Api.Features.Members;
using Faktura.Api.Health;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Organizations;
using Faktura.Infrastructure;
using Faktura.Infrastructure.Persistence;
using Faktura.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Strukturerad loggning (Serilog): konsol + berikad kontext; overridebart via "Serilog"-sektionen.
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- Services ---
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<TokenIssuer>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MemberService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ReminderMailer>();
builder.Services.AddScoped<ReminderService>();
builder.Services.AddScoped<ReminderJob>();
builder.Services.AddScoped<ArticleService>();
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<ReminderBackgroundService>();
builder.Services.AddProblemDetails();

// OpenAPI-dokument (Swashbuckle) + hälsokontroller. Liveness (/health) är beroendefri;
// readiness (/health/ready) pingar Mongo och registreras inte i Testing (in-memory-repos).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var health = builder.Services.AddHealthChecks();
if (!builder.Environment.IsEnvironment("Testing"))
    health.AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configure JwtBearer from JwtOptions via DI so the signing key is read from the fully
// merged configuration at runtime (inline reads would miss test/host config added at Build).
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtAccessor) =>
    {
        var jwt = jwtAccessor.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            NameClaimType = "sub",
            RoleClaimType = FakturaClaims.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// Rate limiting per tenant: partition by tenantId, quota from the tenant's plan config.
// Anonymous requests are not tenant-limited here (login abuse is handled by the login throttle).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var tenantId = user.FindFirstValue(FakturaClaims.TenantId) ?? "unknown";
            var catalog = context.RequestServices.GetRequiredService<IPlanCatalog>();
            var plan = Enum.TryParse<PlanTier>(user.FindFirstValue(FakturaClaims.Plan), out var p) ? p : PlanTier.Free;
            var def = catalog.Get(plan);
            return RateLimitPartition.GetFixedWindowLimiter($"tenant:{tenantId}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = def.RateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(def.RateLimitWindowSeconds),
                QueueLimit = 0
            });
        }
        return RateLimitPartition.GetNoLimiter("anon");
    });
    options.OnRejected = async (context, ct) =>
    {
        var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra) ? (int)ra.TotalSeconds : 60;
        context.HttpContext.Response.Headers.RetryAfter = retry.ToString();
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { title = "För många anrop", status = 429, retryAfterSeconds = retry }, ct);
    };
});

var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("spa", policy =>
{
    if (corsOrigins.Length > 0)
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

// --- Pipeline ---
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// API-dokumentation: OpenAPI-json via Swashbuckle, interaktiv referens via Scalar (/scalar).
app.UseSwagger();
app.MapScalarApiReference(options =>
{
    options.Title = "Faktura API";
    options.OpenApiRoutePattern = "/swagger/{documentName}/swagger.json";
});

// Hälsokontroller: /health = liveness (beroendefri), /health/ready = readiness (Mongo-ping).
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => !r.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready");

app.MapAuthEndpoints();
app.MapMembersEndpoints();
app.MapBillingEndpoints();
app.MapCustomerEndpoints();
app.MapInvoiceEndpoints();
app.MapArticleEndpoints();

// Create indexes at startup (skipped under Testing / when SkipDbInit is set — tests use in-memory fakes).
if (!app.Environment.IsEnvironment("Testing") && !app.Configuration.GetValue<bool>("SkipDbInit"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<MongoContext>().EnsureIndexesAsync();
}

app.Run();

/// <summary>Exposed so the integration test host (WebApplicationFactory) can reference it.</summary>
public partial class Program { }
