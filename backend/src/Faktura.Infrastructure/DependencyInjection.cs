using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Infrastructure.Billing;
using Faktura.Infrastructure.Common;
using Faktura.Infrastructure.Email;
using Faktura.Infrastructure.Configuration;
using Faktura.Infrastructure.Pdf;
using Faktura.Infrastructure.Persistence;
using Faktura.Infrastructure.Security;
using Faktura.Infrastructure.Webhooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

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
        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
        services.Configure<AppOptions>(config.GetSection(AppOptions.SectionName));
        services.Configure<RedisOptions>(config.GetSection(RedisOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, ObjectIdGenerator>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPlanCatalog, PlanCatalog>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Distribuerad broms/rate limiting (spec 018) — delad mellan instanser via Redis.
        // Lazy anslutning: kraschar inte vid start om Redis inte är uppe än (t.ex. compose-startordning).
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisOptions>>().Value.ConnectionString));
        services.AddSingleton<IRateLimitCounter, RedisRateLimitCounter>();
        services.AddSingleton<ILoginThrottle, RedisLoginThrottle>();

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
        services.AddScoped<IInvoiceEmailRepository, MongoInvoiceEmailRepository>();
        services.AddScoped<IInvoicePaymentRepository, MongoInvoicePaymentRepository>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IInvoiceReminderRepository, MongoInvoiceReminderRepository>();
        services.AddScoped<IReminderSettingsRepository, MongoReminderSettingsRepository>();
        services.AddScoped<IArticleRepository, MongoArticleRepository>();
        services.AddScoped<IRecurringInvoiceRepository, MongoRecurringInvoiceRepository>();
        services.AddScoped<IAuditLogRepository, MongoAuditLogRepository>();
        services.AddScoped<IPasswordResetRepository, MongoPasswordResetRepository>();
        services.AddScoped<IApiKeyRepository, MongoApiKeyRepository>();
        services.AddScoped<IWebhookEndpointRepository, MongoWebhookEndpointRepository>();
        services.AddScoped<IWebhookDeliveryRepository, MongoWebhookDeliveryRepository>();
        services.AddHttpClient("webhooks", c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<IWebhookDispatcher, HttpWebhookDispatcher>();

        services.AddSingleton<IBillingGateway, StripeBillingGateway>();
        services.AddSingleton<IWebhookEventParser, StripeWebhookEventParser>();

        // Pure domain service composed of the abstractions above.
        services.AddScoped<AccountRegistration>();

        return services;
    }
}
