using System.Text.Json;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

/// <summary>
/// 4 档绑定策略实现：
/// - Direct：使用 ExternalUserId 与 LocalUserId 一一对应（用于历史导入 / 管理员手动绑定）；
/// - Mobile：通过手机号匹配本地用户；
/// - Email：通过邮箱匹配本地用户；
/// - NameDept：保留接口不自动绑定，进入 PendingConfirm；
/// - Manual：纯人工，由 CreateManualAsync 触发。
/// </summary>
public sealed class ExternalIdentityBindingService : IExternalIdentityBindingService
{
    private readonly IExternalIdentityBindingRepository _bindingRepository;
    private readonly IExternalIdentityBindingAuditRepository _auditRepository;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly ILocalUserDirectory _localUserDirectory;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly IMapper _mapper;
    private readonly ILogger<ExternalIdentityBindingService> _logger;

    public ExternalIdentityBindingService(
        IExternalIdentityBindingRepository bindingRepository,
        IExternalIdentityBindingAuditRepository auditRepository,
        IExternalIdentityProviderRepository providerRepository,
        ILocalUserDirectory localUserDirectory,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGenerator,
        TimeProvider timeProvider,
        IMapper mapper,
        ILogger<ExternalIdentityBindingService> logger)
    {
        _bindingRepository = bindingRepository;
        _auditRepository = auditRepository;
        _providerRepository = providerRepository;
        _localUserDirectory = localUserDirectory;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BindingResolutionResult> ResolveOrAttemptBindAsync(long providerId, ExternalUserProfile profile, IdentityBindingMatchStrategy strategy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var tenantId = _tenantProvider.GetTenantId();

        var existing = await _bindingRepository.GetByExternalUserIdAsync(tenantId, providerId, profile.ExternalUserId, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.Status == IdentityBindingStatus.Active)
        {
            existing.UpdateProfileSnapshot(profile.OpenId, profile.UnionId, profile.Mobile, profile.Email, _timeProvider.GetUtcNow());
            await _bindingRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            return new BindingResolutionResult { Kind = BindingResolutionKind.Existing, Binding = _mapper.Map<ExternalIdentityBindingResponse>(existing) };
        }

        // 自动匹配本地用户
        LocalUserSnapshot? local = null;
        IdentityBindingMatchStrategy? matchedStrategy = null;
        switch (strategy)
        {
            case IdentityBindingMatchStrategy.Direct:
                // Direct 策略要求外部 user id 已经直接对应本地 user id；不走任何模糊匹配。
                if (long.TryParse(profile.ExternalUserId, out var directLocalId))
                {
                    local = await _localUserDirectory.FindByIdAsync(directLocalId, cancellationToken).ConfigureAwait(false);
                    matchedStrategy = local is null ? null : IdentityBindingMatchStrategy.Direct;
                }
                break;
            case IdentityBindingMatchStrategy.Mobile:
                if (!string.IsNullOrWhiteSpace(profile.Mobile))
                {
                    local = await _localUserDirectory.FindByMobileAsync(profile.Mobile!, cancellationToken).ConfigureAwait(false);
                    matchedStrategy = local is null ? null : IdentityBindingMatchStrategy.Mobile;
                }
                break;
            case IdentityBindingMatchStrategy.Email:
                if (!string.IsNullOrWhiteSpace(profile.Email))
                {
                    local = await _localUserDirectory.FindByEmailAsync(profile.Email!, cancellationToken).ConfigureAwait(false);
                    matchedStrategy = local is null ? null : IdentityBindingMatchStrategy.Email;
                }
                break;
            case IdentityBindingMatchStrategy.NameDept:
            case IdentityBindingMatchStrategy.Manual:
                // 不做自动匹配
                break;
        }

        if (local is null)
        {
            return new BindingResolutionResult
            {
                Kind = strategy == IdentityBindingMatchStrategy.NameDept ? BindingResolutionKind.PendingConfirm : BindingResolutionKind.PendingManual,
                PendingTicket = BuildPendingTicket(providerId, profile),
            };
        }

        // 检查本地用户是否已被同 provider 的其他外部账号绑定
        var existingForLocalUser = (await _bindingRepository.GetByLocalUserIdAsync(tenantId, local.Id, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(b => b.ProviderId == providerId && b.Status == IdentityBindingStatus.Active);
        if (existingForLocalUser is not null && existingForLocalUser.ExternalUserId != profile.ExternalUserId)
        {
            // 冲突：同一本地用户在该 provider 已绑定到不同 ExternalUserId（典型场景：换号 / 重名）。
            existingForLocalUser.MarkConflict(_timeProvider.GetUtcNow());
            await _bindingRepository.UpdateAsync(existingForLocalUser, cancellationToken).ConfigureAwait(false);
            await WriteAuditAsync(providerId, existingForLocalUser.Id, local.Id, profile.ExternalUserId, IdentityBindingAuditAction.ConflictDetected,
                $"Local user {local.Id} already bound to {existingForLocalUser.ExternalUserId}, new external {profile.ExternalUserId} marked as conflict.",
                "system", cancellationToken).ConfigureAwait(false);
            return new BindingResolutionResult
            {
                Kind = BindingResolutionKind.Conflict,
                ConflictWith = _mapper.Map<ExternalIdentityBindingListItem>(existingForLocalUser),
            };
        }

        // 自动新建绑定
        var now = _timeProvider.GetUtcNow();
        var binding = new ExternalIdentityBinding(
            tenantId,
            _idGenerator.NextId(),
            providerId,
            local.Id,
            profile.ExternalUserId,
            profile.OpenId,
            profile.UnionId,
            profile.Mobile,
            profile.Email,
            matchedStrategy ?? strategy,
            "oauth_callback",
            now,
            IdentityBindingStatus.Active);
        await _bindingRepository.AddAsync(binding, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(providerId, binding.Id, local.Id, profile.ExternalUserId, IdentityBindingAuditAction.Created,
            $"Auto bind by strategy {matchedStrategy}.", "system", cancellationToken).ConfigureAwait(false);

        return new BindingResolutionResult
        {
            Kind = BindingResolutionKind.AutoCreated,
            Binding = _mapper.Map<ExternalIdentityBindingResponse>(binding),
        };
    }

    public async Task<ExternalIdentityBindingResponse> CreateManualAsync(ManualBindingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = _tenantProvider.GetTenantId();

        var provider = await _providerRepository.GetByIdAsync(tenantId, request.ProviderId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"Provider {request.ProviderId} not found.");

        var existing = await _bindingRepository.GetByExternalUserIdAsync(tenantId, provider.Id, request.ExternalUserId, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.Status == IdentityBindingStatus.Active)
        {
            throw new BusinessException("CONNECTOR_BINDING_EXISTS", $"External user {request.ExternalUserId} already bound to local user {existing.LocalUserId}.");
        }

        var local = await _localUserDirectory.FindByIdAsync(request.LocalUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_LOCAL_USER_NOT_FOUND", $"Local user {request.LocalUserId} not found.");

        var now = _timeProvider.GetUtcNow();
        var binding = new ExternalIdentityBinding(
            tenantId,
            _idGenerator.NextId(),
            provider.Id,
            local.Id,
            request.ExternalUserId,
            request.OpenId,
            request.UnionId,
            request.Mobile,
            request.Email,
            IdentityBindingMatchStrategy.Manual,
            "manual_admin",
            now,
            IdentityBindingStatus.Active);

        await _bindingRepository.AddAsync(binding, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(provider.Id, binding.Id, local.Id, request.ExternalUserId, IdentityBindingAuditAction.Created,
            "Manual create by admin.", "admin", cancellationToken).ConfigureAwait(false);

        return _mapper.Map<ExternalIdentityBindingResponse>(binding);
    }

    public async Task<ExternalIdentityBindingResponse> ResolveConflictAsync(BindingConflictResolutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = _tenantProvider.GetTenantId();
        var binding = await _bindingRepository.GetByIdAsync(tenantId, request.BindingId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_BINDING_NOT_FOUND", $"Binding {request.BindingId} not found.");

        var now = _timeProvider.GetUtcNow();
        switch (request.Resolution)
        {
            case BindingConflictResolution.KeepCurrent:
                binding.Confirm(now);
                break;
            case BindingConflictResolution.SwitchToLocalUser:
                if (request.NewLocalUserId is not { } newLocalId)
                {
                    throw new BusinessException("CONNECTOR_BINDING_NEW_LOCAL_REQUIRED", "NewLocalUserId is required for SwitchToLocalUser.");
                }
                var newLocal = await _localUserDirectory.FindByIdAsync(newLocalId, cancellationToken).ConfigureAwait(false)
                    ?? throw new BusinessException("CONNECTOR_LOCAL_USER_NOT_FOUND", $"Local user {newLocalId} not found.");
                // 直接 revoke 旧 binding，再创建新 binding，便于审计 trail 清晰。
                binding.Revoke(now);
                await _bindingRepository.UpdateAsync(binding, cancellationToken).ConfigureAwait(false);
                var replacement = new ExternalIdentityBinding(
                    tenantId,
                    _idGenerator.NextId(),
                    binding.ProviderId,
                    newLocal.Id,
                    binding.ExternalUserId,
                    binding.OpenId,
                    binding.UnionId,
                    binding.Mobile,
                    binding.Email,
                    binding.MatchStrategy,
                    "manual_admin_conflict_resolve",
                    now,
                    IdentityBindingStatus.Active);
                await _bindingRepository.AddAsync(replacement, cancellationToken).ConfigureAwait(false);
                await WriteAuditAsync(binding.ProviderId, replacement.Id, newLocal.Id, binding.ExternalUserId,
                    IdentityBindingAuditAction.ConflictResolved, $"Switched from local user {binding.LocalUserId} to {newLocal.Id}.", "admin", cancellationToken).ConfigureAwait(false);
                return _mapper.Map<ExternalIdentityBindingResponse>(replacement);
            case BindingConflictResolution.Revoke:
                binding.Revoke(now);
                break;
            default:
                throw new BusinessException("CONNECTOR_BINDING_RESOLUTION_INVALID", $"Unknown resolution {request.Resolution}.");
        }

        await _bindingRepository.UpdateAsync(binding, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(binding.ProviderId, binding.Id, binding.LocalUserId, binding.ExternalUserId,
            request.Resolution == BindingConflictResolution.Revoke ? IdentityBindingAuditAction.Revoked : IdentityBindingAuditAction.ConflictResolved,
            request.Resolution.ToString(), "admin", cancellationToken).ConfigureAwait(false);

        return _mapper.Map<ExternalIdentityBindingResponse>(binding);
    }

    public async Task RevokeAsync(long bindingId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var binding = await _bindingRepository.GetByIdAsync(tenantId, bindingId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_BINDING_NOT_FOUND", $"Binding {bindingId} not found.");

        var now = _timeProvider.GetUtcNow();
        binding.Revoke(now);
        await _bindingRepository.UpdateAsync(binding, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(binding.ProviderId, binding.Id, binding.LocalUserId, binding.ExternalUserId,
            IdentityBindingAuditAction.Revoked, "Revoked by admin.", "admin", cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ExternalIdentityBindingListItem>> ListByProviderAsync(long providerId, IdentityBindingStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var entities = await _bindingRepository.ListByProviderAsync(tenantId, providerId, status, skip, take, cancellationToken).ConfigureAwait(false);
        return _mapper.Map<IReadOnlyList<ExternalIdentityBindingListItem>>(entities);
    }

    public Task<int> CountByProviderAsync(long providerId, IdentityBindingStatus? status, CancellationToken cancellationToken)
        => _bindingRepository.CountByProviderAsync(_tenantProvider.GetTenantId(), providerId, status, cancellationToken);

    public async Task TouchLoginAsync(long bindingId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var binding = await _bindingRepository.GetByIdAsync(tenantId, bindingId, cancellationToken).ConfigureAwait(false);
        if (binding is null)
        {
            return;
        }
        binding.TouchLogin(_timeProvider.GetUtcNow());
        await _bindingRepository.UpdateAsync(binding, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAuditAsync(long providerId, long? bindingId, long? localUserId, string externalUserId,
        IdentityBindingAuditAction action, string detail, string actor, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var log = new ExternalIdentityBindingAuditLog(
            tenantId,
            _idGenerator.NextId(),
            providerId,
            bindingId,
            localUserId,
            externalUserId,
            action,
            detail,
            actor,
            _timeProvider.GetUtcNow());
        await _auditRepository.AddAsync(log, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildPendingTicket(long providerId, ExternalUserProfile profile)
    {
        // 临时 ticket 仅承载 provider + external_user_id + 简要资料，调用方应在前端待绑定页传回。
        var payload = new
        {
            providerId,
            providerType = profile.ProviderType,
            externalUserId = profile.ExternalUserId,
            mobile = profile.Mobile,
            email = profile.Email,
        };
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload));
    }
}
