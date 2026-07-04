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
        });
    }
}
