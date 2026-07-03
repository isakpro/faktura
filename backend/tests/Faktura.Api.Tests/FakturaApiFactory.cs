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
                ["Plans:Pro:SeatLimit"] = "25"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IOrganizationRepository>();
            services.RemoveAll<IRefreshTokenRepository>();

            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddSingleton<IOrganizationRepository, InMemoryOrganizationRepository>();
            services.AddSingleton<IRefreshTokenRepository, InMemoryRefreshTokenRepository>();
        });
    }
}
