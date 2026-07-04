namespace Faktura.Domain.Invoicing;

/// <summary>
/// Per-organisationsinställning för automatiska påminnelser. Opt-in: standard av, 7 dagar
/// efter förfall (FR-007).
/// </summary>
public sealed record ReminderSettings(string TenantId, bool AutoEnabled, int DaysAfterDue)
{
    public const int DefaultDaysAfterDue = 7;

    public static ReminderSettings Default(string tenantId) =>
        new(tenantId, AutoEnabled: false, DaysAfterDue: DefaultDaysAfterDue);
}
