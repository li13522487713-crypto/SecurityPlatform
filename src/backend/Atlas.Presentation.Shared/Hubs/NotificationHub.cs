using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Atlas.Presentation.Shared.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    public async Task SubscribeTenantAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new HubException("tenantId 不能为空。");
        }

        var claimTenantId = Context.User?.FindFirstValue("tenant_id");
        if (!string.IsNullOrWhiteSpace(claimTenantId)
            && !string.Equals(claimTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException("租户不匹配，禁止订阅。");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId.Trim()}", Context.ConnectionAborted);
    }

    public Task UnsubscribeTenantAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Task.CompletedTask;
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{tenantId.Trim()}", Context.ConnectionAborted);
    }
}
