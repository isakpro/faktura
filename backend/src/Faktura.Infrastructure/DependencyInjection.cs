using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Infrastructure.Common;
using Faktura.Infrastructure.Configuration;
using Faktura.Infrastructure.Persistence;
using Faktura.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Faktura.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers persistence, security and domain services for the SaaS skeleton.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<MongoOptions>(config.GetSection(MongoOptions.SectionName));
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.Configure<PlanOptions>(config.GetSection(PlanOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, ObjectIdGenerator>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPlanCatalog, PlanCatalog>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddSingleton<MongoContext>();
        services.AddScoped<IOrganizationRepository, MongoOrganizationRepository>();
        services.AddScoped<IUserRepository, MongoUserRepository>();
        services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();

        // Pure domain service composed of the abstractions above.
        services.AddScoped<AccountRegistration>();

        return services;
    }
}
