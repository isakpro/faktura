using System.Text.Json;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Billing;
using Faktura.Domain.Common;
using Faktura.Domain.Organizations;

namespace Faktura.Api.Tests.Fakes;

/// <summary>Fake gateway: deterministic checkout URL and customer id (cus_{tenantId}).</summary>
public sealed class FakeBillingGateway : IBillingGateway
{
    public Task<CheckoutResult> CreateProCheckoutAsync(Organization organization, string returnUrl, CancellationToken ct = default)
        => Task.FromResult(new CheckoutResult("https://checkout.test/session", "cus_" + organization.Id));
}

/// <summary>
/// Fake parser: signature "valid" parses the JSON test payload; anything else is treated as
/// an invalid signature. Keeps webhook tests independent of Stripe's real HMAC signing.
/// </summary>
public sealed class FakeWebhookEventParser : IWebhookEventParser
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public Result<BillingEvent> Parse(string payload, string? signature)
    {
        if (signature != "valid")
            return Result.Failure<BillingEvent>(Error.InvalidSignature());

        var e = JsonSerializer.Deserialize<TestEvent>(payload, Web)!;
        var type = Enum.Parse<BillingEventType>(e.Type);
        return Result.Success(new BillingEvent(e.Id, type, e.CustomerId, e.SubscriptionId));
    }

    private sealed record TestEvent(string Id, string Type, string? CustomerId, string? SubscriptionId);
}

/// <summary>In-memory idempotency store.</summary>
public sealed class InMemoryProcessedEventStore : IProcessedEventStore
{
    private readonly HashSet<string> _seen = new();

    public Task<bool> TryMarkProcessedAsync(string eventId, string eventType, CancellationToken ct = default)
    {
        lock (_seen)
            return Task.FromResult(_seen.Add(eventId));
    }
}
