namespace Faktura.Infrastructure.Billing;

/// <summary>Stripe configuration (test mode), bound from the "Stripe" section.</summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string ProPriceId { get; set; } = "";
}
