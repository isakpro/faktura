namespace Faktura.Domain.Organizations;

/// <summary>State of an organization's paid subscription (driven by Stripe webhooks).</summary>
public enum SubscriptionStatus
{
    None = 0,
    Active = 1,
    PastDue = 2,
    Canceled = 3
}
