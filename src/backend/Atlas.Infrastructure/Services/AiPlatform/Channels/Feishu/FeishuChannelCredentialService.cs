using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Channels;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Channels;
using Atlas.Infrastructure.Services.LowCode;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels.Feishu;

public sealed class FeishuChannelCredentialService : WorkspaceChannelCredentialServiceBase, IFeishuChannelCredentialService
{
    private readonly FeishuChannelCredentialRepository _credentialRepository;

    public FeishuChannelCredentialService(
        FeishuChannelCredentialRepository credentialRepository,
        WorkspacePublishChannelRepository channelRepository,
        LowCodeCredentialProtector protector,
        IIdGeneratorAccessor idGenerator)
        : base(channelRepository, protector, idGenerator)
    {
        _credentialRepository = credentialRepository;
    }

    public async Task<FeishuChannelCredentialDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var entity = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<FeishuChannelCredentialDto> UpsertAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        FeishuChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AppId)) throw new BusinessException(ErrorCodes.ValidationError, "FeishuAppIdRequired");
        if (string.IsNullOrWhiteSpace(request.AppSecret)) throw new BusinessException(ErrorCodes.ValidationError, "FeishuAppSecretRequired");
        if (string.IsNullOrWhiteSpace(request.VerificationToken)) throw new BusinessException(ErrorCodes.ValidationError, "FeishuVerificationTokenRequired");

        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);

        var encAppSecret = Protector.Encrypt(request.AppSecret);
        var encEncryptKey = EncryptOrEmpty(Protector, request.EncryptKey);

        var existing = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        if (existing is null)
        {
            var entity = new FeishuChannelCredential(
                tenantId,
                channel.Id,
                channel.WorkspaceId,
                request.AppId.Trim(),
                encAppSecret,
                request.VerificationToken.Trim(),
                encEncryptKey,
                "[]",
                IdGenerator.NextId());
            await _credentialRepository.AddAsync(entity, cancellationToken);
            return ToDto(entity);
        }

        existing.Update(
            appId: request.AppId.Trim(),
            appSecretEnc: encAppSecret,
            verificationToken: request.VerificationToken.Trim(),
            encryptKeyEnc: encEncryptKey,
            agentBindingsJson: existing.AgentBindingsJson);
        await _credentialRepository.UpdateAsync(existing, cancellationToken);
        return ToDto(existing);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var existing = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        if (existing is null) return;
        await _credentialRepository.DeleteAsync(existing, cancellationToken);
    }

    internal static FeishuChannelCredentialDto ToDto(FeishuChannelCredential entity)
    {
        return new FeishuChannelCredentialDto(
            Id: entity.Id.ToString(),
            ChannelId: entity.ChannelId.ToString(),
            WorkspaceId: entity.WorkspaceId,
            AppId: entity.AppId,
            AppIdMasked: LowCodeCredentialProtector.Mask(entity.AppId),
            VerificationToken: entity.VerificationToken,
            HasEncryptKey: !string.IsNullOrEmpty(entity.EncryptKeyEnc),
            TenantAccessTokenExpiresAt: entity.TenantAccessTokenExpiresAt > DateTime.UnixEpoch
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.TenantAccessTokenExpiresAt, DateTimeKind.Utc))
                : null,
            RefreshCount: entity.RefreshCount,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }
}
