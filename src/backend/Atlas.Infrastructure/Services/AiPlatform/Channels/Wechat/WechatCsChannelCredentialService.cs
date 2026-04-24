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

namespace Atlas.Infrastructure.Services.AiPlatform.Channels.Wechat;

public sealed class WechatCsChannelCredentialService : WorkspaceChannelCredentialServiceBase, IWechatCsChannelCredentialService
{
    private readonly WechatCsChannelCredentialRepository _credentialRepository;

    public WechatCsChannelCredentialService(
        WechatCsChannelCredentialRepository credentialRepository,
        WorkspacePublishChannelRepository channelRepository,
        LowCodeCredentialProtector protector,
        IIdGeneratorAccessor idGenerator)
        : base(channelRepository, protector, idGenerator)
    {
        _credentialRepository = credentialRepository;
    }

    public async Task<WechatCsChannelCredentialDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var entity = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<WechatCsChannelCredentialDto> UpsertAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        WechatCsChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CorpId)) throw new BusinessException(ErrorCodes.ValidationError, "WechatCsCorpIdRequired");
        if (string.IsNullOrWhiteSpace(request.Secret)) throw new BusinessException(ErrorCodes.ValidationError, "WechatCsSecretRequired");
        if (string.IsNullOrWhiteSpace(request.OpenKfId)) throw new BusinessException(ErrorCodes.ValidationError, "WechatCsOpenKfIdRequired");

        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var encSecret = Protector.Encrypt(request.Secret);
        var encAesKey = EncryptOrEmpty(Protector, request.EncodingAesKey);
        var token = request.Token?.Trim() ?? string.Empty;

        var existing = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        if (existing is null)
        {
            var entity = new WechatCsChannelCredential(
                tenantId,
                channel.Id,
                channel.WorkspaceId,
                request.CorpId.Trim(),
                encSecret,
                request.OpenKfId.Trim(),
                token,
                encAesKey,
                "[]",
                IdGenerator.NextId());
            await _credentialRepository.AddAsync(entity, cancellationToken);
            return ToDto(entity);
        }

        existing.Update(
            corpId: request.CorpId.Trim(),
            corpSecretEnc: encSecret,
            openKfId: request.OpenKfId.Trim(),
            token: token,
            encodingAesKeyEnc: encAesKey,
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
        if (existing is null)
        {
            return;
        }

        await _credentialRepository.DeleteAsync(existing, cancellationToken);
    }

    internal static WechatCsChannelCredentialDto ToDto(WechatCsChannelCredential entity)
    {
        return new WechatCsChannelCredentialDto(
            Id: entity.Id.ToString(),
            ChannelId: entity.ChannelId.ToString(),
            WorkspaceId: entity.WorkspaceId,
            CorpId: entity.CorpId,
            CorpIdMasked: LowCodeCredentialProtector.Mask(entity.CorpId),
            OpenKfId: entity.OpenKfId,
            Token: entity.Token,
            HasEncodingAesKey: !string.IsNullOrEmpty(entity.EncodingAesKeyEnc),
            AccessTokenExpiresAt: entity.AccessTokenExpiresAt > DateTime.UnixEpoch
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.AccessTokenExpiresAt, DateTimeKind.Utc))
                : null,
            RefreshCount: entity.RefreshCount,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }
}
