namespace Faktura.Api.Features.Billing;

public sealed record BillingDto(string Plan, string SubscriptionStatus, int SeatLimit);
public sealed record CheckoutRequest(string ReturnUrl);
public sealed record CheckoutResponse(string CheckoutUrl);
