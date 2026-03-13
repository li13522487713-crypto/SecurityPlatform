using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiShortcutCommandService : IAiShortcutCommandService
{
    private const string DefaultPopupCode = "ai-onboarding";
    private readonly AiShortcutCommandRepository _shortcutRepository;
    private readonly AiBotPopupInfoRepository _popupRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AiShortcutCommandService(
        AiShortcutCommandRepository shortcutRepository,
        AiBotPopupInfoRepository popupRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _shortcutRepository = shortcutRepository;
        _popupRepository = popupRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<IReadOnlyList<AiShortcutCommandItem>> GetCommandsAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var commands = await _shortcutRepository.GetEnabledAsync(tenantId, cancellationToken);
        return commands
            .Select(x => new AiShortcutCommandItem(
                x.Id,
                x.CommandKey,
                x.DisplayName,
                x.TargetPath,
                x.Description,
                x.SortOrder,
                x.IsEnabled))
            .ToArray();
    }

    public async Task<long> CreateCommandAsync(
        TenantId tenantId,
        AiShortcutCommandCreateRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedKey = request.CommandKey.Trim();
        if (await _shortcutRepository.ExistsByCommandKeyAsync(tenantId, normalizedKey, null, cancellationToken))
        {
            throw new BusinessException("快捷命令编码已存在。", ErrorCodes.ValidationError);
        }

        var entity = new AiShortcutCommand(
            tenantId,
            normalizedKey,
            request.DisplayName.Trim(),
            request.TargetPath.Trim(),
            request.Description?.Trim(),
            request.SortOrder,
            _idGeneratorAccessor.NextId());
        await _shortcutRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateCommandAsync(
        TenantId tenantId,
        long commandId,
        AiShortcutCommandUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var command = await _shortcutRepository.FindByIdAsync(tenantId, commandId, cancellationToken)
            ?? throw new BusinessException("快捷命令不存在。", ErrorCodes.NotFound);
        command.Update(
            request.DisplayName.Trim(),
            request.TargetPath.Trim(),
            request.Description?.Trim(),
            request.SortOrder,
            request.IsEnabled);
        await _shortcutRepository.UpdateAsync(command, cancellationToken);
    }

    public async Task DeleteCommandAsync(TenantId tenantId, long commandId, CancellationToken cancellationToken)
    {
        await _shortcutRepository.DeleteAsync(tenantId, commandId, cancellationToken);
    }

    public async Task<AiBotPopupInfoDto> GetPopupInfoAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var popup = await _popupRepository.FindByUserAndCodeAsync(
            tenantId,
            userId,
            DefaultPopupCode,
            cancellationToken);
        if (popup is null)
        {
            popup = new AiBotPopupInfo(
                tenantId,
                userId,
                DefaultPopupCode,
                "欢迎使用 AI 平台",
                "可通过快捷命令快速进入 Agent、工作流、知识库与开放平台。",
                dismissed: false,
                _idGeneratorAccessor.NextId());
            await _popupRepository.AddAsync(popup, cancellationToken);
        }

        return MapPopup(popup);
    }

    public async Task<AiBotPopupInfoDto> DismissPopupAsync(
        TenantId tenantId,
        long userId,
        AiBotPopupDismissRequest request,
        CancellationToken cancellationToken)
    {
        var popup = await _popupRepository.FindByUserAndCodeAsync(
            tenantId,
            userId,
            request.PopupCode.Trim(),
            cancellationToken);
        if (popup is null)
        {
            popup = new AiBotPopupInfo(
                tenantId,
                userId,
                request.PopupCode.Trim(),
                "欢迎使用 AI 平台",
                "可通过快捷命令快速进入 Agent、工作流、知识库与开放平台。",
                request.Dismissed,
                _idGeneratorAccessor.NextId());
            await _popupRepository.AddAsync(popup, cancellationToken);
            return MapPopup(popup);
        }

        popup.SetDismissed(request.Dismissed);
        await _popupRepository.UpdateAsync(popup, cancellationToken);
        return MapPopup(popup);
    }

    private static AiBotPopupInfoDto MapPopup(AiBotPopupInfo popup)
        => new(
            popup.Id,
            popup.PopupCode,
            popup.Title,
            popup.Content,
            popup.Dismissed,
            popup.CreatedAt,
            popup.UpdatedAt);
}
