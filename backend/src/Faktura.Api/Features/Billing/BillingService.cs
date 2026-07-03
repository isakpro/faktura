using Faktura.Domain.Abstractions;
using Faktura.Domain.Billing;
using Faktura.Domain.Common;
using Faktura.Domain.Organizations;
using Faktura.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Faktura.Api.Features.Billing;

/// <summary>
/// Owner-only plan management and Stripe webhook handling. Plan status is driven solely by
/// verified, idempotent webhooks (FR-016–018); the client never sets plan state directly.
/// </summary>
public sealed class BillingService
{
    private readonly ITenantContext _tenant;
    private readonly IOrganizationRepository _organizations;
    private readonly IBillingGateway _gateway;
    private readonly IWebhookEventParser _parser;
    private readonly IProcessedEventStore _processed;
    private readonly IPlanCatalog _plans;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        ITenantContext tenant,
        IOrganizationRepository organizations,
        IBillingGateway gateway,
        IWebhookEventParser parser,
        IProcessedEventStore processed,
        IPlanCatalog plans,
        ILogger<BillingService> logger)
    {
        _tenant = tenant;
        _organizations = organizations;
        _gateway = gateway;
        _parser = parser;
        _processed = processed;
        _plans = plans;
        _logger = logger;
    }

    public async Task<Result<BillingDto>> GetBillingAsync(CancellationToken ct)
    {
        if (_tenant.Role != UserRole.Owner) return Result.Failure<BillingDto>(Error.Forbidden());

        var org = await _organizations.GetByIdAsync(_tenant.TenantId, ct);
        if (org is null) return Result.Failure<BillingDto>(Error.NotFound());

        return Result.Success(new BillingDto(org.Plan.ToString(), org.SubscriptionStatus.ToString(), org.SeatLimit));
    }

    public async Task<Result<CheckoutResponse>> CheckoutAsync(string returnUrl, CancellationToken ct)
    {
        if (_tenant.Role != UserRole.Owner) return Result.Failure<CheckoutResponse>(Error.Forbidden());

        var org = await _organizations.GetByIdAsync(_tenant.TenantId, ct);
        if (org is null) return Result.Failure<CheckoutResponse>(Error.NotFound());

        var checkout = await _gateway.CreateProCheckoutAsync(org, returnUrl, ct);

        if (org.StripeCustomerId != checkout.CustomerId)
        {
            org.AttachStripeCustomer(checkout.CustomerId);
            await _organizations.UpdateAsync(org, ct);
        }

        return Result.Success(new CheckoutResponse(checkout.CheckoutUrl));
    }

    /// <summary>Verifies and applies a webhook. Public (no auth) — trust comes from the signature.</summary>
    public async Task<Result> HandleWebhookAsync(string payload, string? signature, CancellationToken ct)
    {
        var parsed = _parser.Parse(payload, signature);
        if (parsed.IsFailure)
        {
            _logger.LogWarning("Rejected webhook with invalid signature");
            return Result.Failure(parsed.Error);
        }

        var evt = parsed.Value;
        if (evt.Type == BillingEventType.Unknown || string.IsNullOrEmpty(evt.CustomerId))
            return Result.Success(); // nothing to do — acknowledge

        // Idempotency: skip if we have already processed this event id.
        if (!await _processed.TryMarkProcessedAsync(evt.Id, evt.Type.ToString(), ct))
        {
            _logger.LogInformation("Skipping duplicate webhook {EventId}", evt.Id);
            return Result.Success();
        }

        var org = await _organizations.GetByStripeCustomerAsync(evt.CustomerId, ct);
        if (org is null) return Result.Success(); // unknown customer — acknowledge

        switch (evt.Type)
        {
            case BillingEventType.SubscriptionActivated:
                org.ActivatePro(evt.SubscriptionId ?? "", _plans.Get(PlanTier.Pro).SeatLimit);
                break;
            case BillingEventType.SubscriptionCanceled:
                org.CancelToFree(_plans.Get(PlanTier.Free).SeatLimit);
                break;
        }

        await _organizations.UpdateAsync(org, ct);
        _logger.LogInformation("Applied {EventType} for tenant {TenantId}", evt.Type, org.Id);
        return Result.Success();
    }
}
