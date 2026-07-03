using System.Text;
using Faktura.Api.Auth;
using Faktura.Api.Features.Auth;
using Faktura.Domain.Abstractions;
using Faktura.Infrastructure;
using Faktura.Infrastructure.Persistence;
using Faktura.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddProblemDetails();

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
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

// Create indexes at startup (skipped under Testing / when SkipDbInit is set — tests use in-memory fakes).
if (!app.Environment.IsEnvironment("Testing") && !app.Configuration.GetValue<bool>("SkipDbInit"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<MongoContext>().EnsureIndexesAsync();
}

app.Run();

/// <summary>Exposed so the integration test host (WebApplicationFactory) can reference it.</summary>
public partial class Program { }
