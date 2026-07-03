using Faktura.Domain.Abstractions;
using Faktura.Domain.Billing;
using Faktura.Domain.Common;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Faktura.Infrastructure.Billing;

/// <summary>Verifies the Stripe webhook signature and normalizes events to <see cref="BillingEvent"/>.</summary>
internal sealed class StripeWebhookEventParser : IWebhookEventParser
{
    private readonly StripeOptions _options;

    public StripeWebhookEventParser(IOptions<StripeOptions> options) => _options = options.Value;

    public Result<BillingEvent> Parse(string payload, string? signature)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);
        }
        catch (StripeException)
        {
            return Result.Failure<BillingEvent>(Error.InvalidSignature());
        }

        return Result.Success(Normalize(stripeEvent));
    }

    private static BillingEvent Normalize(Event e) => e.Type switch
    {
        "checkout.session.completed" when e.Data.Object is Session s =>
            new BillingEvent(e.Id, BillingEventType.SubscriptionActivated, s.CustomerId, s.SubscriptionId),

        "customer.subscription.deleted" when e.Data.Object is Subscription sub =>
            new BillingEvent(e.Id, BillingEventType.SubscriptionCanceled, sub.CustomerId, sub.Id),

        "customer.subscription.updated" when e.Data.Object is Subscription sub =>
            new BillingEvent(e.Id,
                sub.Status == "active" ? BillingEventType.SubscriptionActivated : BillingEventType.SubscriptionCanceled,
                sub.CustomerId, sub.Id),

        _ => new BillingEvent(e.Id, BillingEventType.Unknown, null, null)
    };
}
