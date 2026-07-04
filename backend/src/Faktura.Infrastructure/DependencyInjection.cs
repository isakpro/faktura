using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Infrastructure.Billing;
using Faktura.Infrastructure.Common;
using Faktura.Infrastructure.Configuration;
using Faktura.Infrastructure.Pdf;
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
        services.Configure<ThrottleOptions>(config.GetSection(ThrottleOptions.SectionName));
        services.Configure<StripeOptions>(config.GetSection(StripeOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, ObjectIdGenerator>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPlanCatalog, PlanCatalog>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ILoginThrottle, InMemoryLoginThrottle>();

        services.AddSingleton<MongoContext>();
        services.AddScoped<IOrganizationRepository, MongoOrganizationRepository>();
        services.AddScoped<IUserRepository, MongoUserRepository>();
        services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();
        services.AddScoped<IInvitationRepository, MongoInvitationRepository>();
        services.AddScoped<IProcessedEventStore, MongoProcessedEventStore>();
        services.AddScoped<ICustomerRepository, MongoCustomerRepository>();
        services.AddScoped<IInvoiceRepository, MongoInvoiceRepository>();
        services.AddScoped<IInvoiceNumberSequence, MongoInvoiceNumberSequence>();
        services.AddSingleton<IInvoicePdfGenerator, QuestPdfInvoiceGenerator>();

        services.AddSingleton<IBillingGateway, StripeBillingGateway>();
        services.AddSingleton<IWebhookEventParser, StripeWebhookEventParser>();

        // Pure domain service composed of the abstractions above.
        services.AddScoped<AccountRegistration>();

        return services;
    }
}
