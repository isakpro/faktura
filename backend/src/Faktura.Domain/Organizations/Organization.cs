namespace Faktura.Domain.Organizations;

/// <summary>
/// An organization (tenant). Its <see cref="Id"/> is the tenant key that every other
/// tenant-owned document references. New organizations start on <see cref="PlanTier.Free"/>.
/// </summary>
public sealed class Organization
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public PlanTier Plan { get; private set; }
    public SubscriptionStatus SubscriptionStatus { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public int SeatLimit { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Säljaruppgifter för fakturans PDF (spec 009). Null tills profilen fylls i.</summary>
    public InvoiceProfile? Profile { get; private set; }

    public void UpdateProfile(InvoiceProfile profile) => Profile = profile;

    // For persistence hydration.
    public Organization(
        string id,
        string name,
        PlanTier plan,
        SubscriptionStatus subscriptionStatus,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        int seatLimit,
        DateTime createdAt,
        InvoiceProfile? profile = null)
    {
        Profile = profile;
        Id = id;
        Name = name;
        Plan = plan;
        SubscriptionStatus = subscriptionStatus;
        StripeCustomerId = stripeCustomerId;
        StripeSubscriptionId = stripeSubscriptionId;
        SeatLimit = seatLimit;
        CreatedAt = createdAt;
    }

    /// <summary>Creates a brand-new organization on the Free plan.</summary>
    /// <param name="id">Pre-generated tenant id.</param>
    /// <param name="name">Organization display name.</param>
    /// <param name="freeSeatLimit">Seat limit for the Free plan (data-driven from plan config).</param>
    /// <param name="now">Current UTC time.</param>
    public static Organization CreateNew(string id, string name, int freeSeatLimit, DateTime now)
        => new(
            id: id,
            name: name.Trim(),
            plan: PlanTier.Free,
            subscriptionStatus: SubscriptionStatus.None,
            stripeCustomerId: null,
            stripeSubscriptionId: null,
            seatLimit: freeSeatLimit,
            createdAt: now);

    /// <summary>Links the organization to its Stripe customer (set at first checkout).</summary>
    public void AttachStripeCustomer(string customerId) => StripeCustomerId = customerId;

    /// <summary>Activates the Pro plan (from a verified checkout/subscription webhook).</summary>
    public void ActivatePro(string subscriptionId, int proSeatLimit)
    {
        Plan = PlanTier.Pro;
        SubscriptionStatus = SubscriptionStatus.Active;
        StripeSubscriptionId = subscriptionId;
        SeatLimit = proSeatLimit;
    }

    /// <summary>Downgrades to Free when the subscription is canceled/ended.</summary>
    public void CancelToFree(int freeSeatLimit)
    {
        Plan = PlanTier.Free;
        SubscriptionStatus = SubscriptionStatus.Canceled;
        StripeSubscriptionId = null;
        SeatLimit = freeSeatLimit;
    }
}
