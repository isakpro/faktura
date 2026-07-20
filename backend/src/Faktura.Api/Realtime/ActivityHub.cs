using Faktura.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Faktura.Api.Realtime;

/// <summary>
/// Realtidskanal för organisationens aktivitet (spec 017). Varje uppkoppling går in i en
/// grupp per tenant, härledd ur den autentiserade JWT:ns claims — aldrig ett klient-valt värde.
/// </summary>
[Authorize]
public sealed class ActivityHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst(FakturaClaims.TenantId)?.Value;
        if (!string.IsNullOrEmpty(tenantId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId));

        await base.OnConnectedAsync();
    }

    public static string GroupName(string tenantId) => $"tenant:{tenantId}";
}
