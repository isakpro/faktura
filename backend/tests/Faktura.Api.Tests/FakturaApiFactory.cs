using Faktura.Api.Tests.Fakes;
using Faktura.Domain.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Faktura.Api.Tests;

/// <summary>
/// Boots the real API in the "Testing" environment with in-memory repositories, so the
/// full HTTP + auth pipeline is exercised without MongoDB. Repositories are singletons so
/// state persists across requests within a test.
/// </summary>
public sealed class FakturaApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "faktura-integration-test-signing-key-0123456789",
                ["Jwt:Issuer"] = "faktura",
                ["Jwt:Audience"] = "faktura",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "30",
                ["Plans:Free:SeatLimit"] = "2",
                ["Plans:Pro:SeatLimit"] = "25",
                ["Throttle:MaxAttempts"] = "3",
                ["Throttle:WindowSeconds"] = "900",
                ["Throttle:LockoutSeconds"] = "900"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IOrganizationRepository>();
            services.RemoveAll<IRefreshTokenRepository>();
            services.RemoveAll<IInvitationRepository>();

            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddSingleton<IOrganizationRepository, InMemoryOrganizationRepository>();
            services.AddSingleton<IRefreshTokenRepository, InMemoryRefreshTokenRepository>();
            services.AddSingleton<IInvitationRepository, InMemoryInvitationRepository>();
            services.RemoveAll<IPasswordResetRepository>();
            services.AddSingleton<IPasswordResetRepository, InMemoryPasswordResetRepository>();

            // Billing: fake gateway/parser/idempotency store (no real Stripe in tests).
            services.RemoveAll<IBillingGateway>();
            services.RemoveAll<IWebhookEventParser>();
            services.RemoveAll<IProcessedEventStore>();
            services.AddSingleton<IBillingGateway, FakeBillingGateway>();
            services.AddSingleton<IWebhookEventParser, FakeWebhookEventParser>();
            services.AddSingleton<IProcessedEventStore, InMemoryProcessedEventStore>();

            // Fakturadomän (002): in-memory customer/invoice/nummerserie.
            services.RemoveAll<ICustomerRepository>();
            services.RemoveAll<IInvoiceRepository>();
            services.RemoveAll<IInvoiceNumberSequence>();
            services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
            services.AddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>();
            services.AddSingleton<IInvoiceNumberSequence, InMemoryInvoiceNumberSequence>();

            // Betalningsreskontra (012).
            services.RemoveAll<IInvoicePaymentRepository>();
            services.AddSingleton<IInvoicePaymentRepository, InMemoryInvoicePaymentRepository>();

            // E-post (003): fejkad sändare + in-memory utskicks-logg.
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IInvoiceEmailRepository>();
            services.AddSingleton<FakeEmailSender>();
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<FakeEmailSender>());
            services.AddSingleton<IInvoiceEmailRepository, InMemoryInvoiceEmailRepository>();

            // Påminnelser (004): styrbar klocka + in-memory repos.
            services.RemoveAll<IClock>();
            services.AddSingleton<MutableClock>();
            services.AddSingleton<IClock>(sp => sp.GetRequiredService<MutableClock>());
            services.RemoveAll<IInvoiceReminderRepository>();
            services.RemoveAll<IReminderSettingsRepository>();
            services.AddSingleton<IInvoiceReminderRepository, InMemoryInvoiceReminderRepository>();
            services.AddSingleton<IReminderSettingsRepository, InMemoryReminderSettingsRepository>();

            // Artiklar (005).
            services.RemoveAll<IArticleRepository>();
            services.AddSingleton<IArticleRepository, InMemoryArticleRepository>();

            services.RemoveAll<IAuditLogRepository>();
            services.AddSingleton<IAuditLogRepository, InMemoryAuditLogRepository>();

            // Återkommande fakturor (007).
            services.RemoveAll<IRecurringInvoiceRepository>();
            services.AddSingleton<IRecurringInvoiceRepository, InMemoryRecurringInvoiceRepository>();

            // Rate limiting/broms (018): Redis-implementationerna byts mot process-lokala så
            // testsviten inte kräver en Redis — IConnectionMultiplexer resolvas då aldrig.
            services.RemoveAll<IRateLimitCounter>();
            services.AddSingleton<IRateLimitCounter, InMemoryRateLimitCounter>();
            services.RemoveAll<ILoginThrottle>();
            services.AddSingleton<ILoginThrottle, Faktura.Infrastructure.Security.InMemoryLoginThrottle>();

            // Publikt API + webhooks (016): in-memory nycklar/mottagare + fångad dispatch (ingen riktig HTTP).
            services.RemoveAll<IApiKeyRepository>();
            services.AddSingleton<IApiKeyRepository, InMemoryApiKeyRepository>();
            services.RemoveAll<IWebhookEndpointRepository>();
            services.AddSingleton<IWebhookEndpointRepository, InMemoryWebhookEndpointRepository>();
            services.RemoveAll<IWebhookDeliveryRepository>();
            services.AddSingleton<IWebhookDeliveryRepository, InMemoryWebhookDeliveryRepository>();
            services.RemoveAll<IWebhookDispatcher>();
            services.AddSingleton<InMemoryWebhookDispatcher>();
            services.AddSingleton<IWebhookDispatcher>(sp => sp.GetRequiredService<InMemoryWebhookDispatcher>());
        });
    }
}
