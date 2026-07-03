namespace Faktura.Domain.Billing;

/// <summary>Normalized billing event derived from a verified provider webhook.</summary>
public enum BillingEventType
{
    Unknown = 0,
    SubscriptionActivated = 1,
    SubscriptionCanceled = 2
}

/// <summary>A verified, provider-agnostic billing event.</summary>
public sealed record BillingEvent(
    string Id,
    BillingEventType Type,
    string? CustomerId,
    string? SubscriptionId);
