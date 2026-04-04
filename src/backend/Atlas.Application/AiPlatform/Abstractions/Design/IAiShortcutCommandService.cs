using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiShortcutCommandService
{
    Task<IReadOnlyList<AiShortcutCommandItem>> GetCommandsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<long> CreateCommandAsync(
        TenantId tenantId,
        AiShortcutCommandCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateCommandAsync(
        TenantId tenantId,
        long commandId,
        AiShortcutCommandUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteCommandAsync(
        TenantId tenantId,
        long commandId,
        CancellationToken cancellationToken);

    Task<AiBotPopupInfoDto> GetPopupInfoAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    Task<AiBotPopupInfoDto> DismissPopupAsync(
        TenantId tenantId,
        long userId,
        AiBotPopupDismissRequest request,
        CancellationToken cancellationToken);
}
