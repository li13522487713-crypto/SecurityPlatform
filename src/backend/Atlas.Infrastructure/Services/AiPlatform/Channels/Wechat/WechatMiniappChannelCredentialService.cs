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

public sealed class WechatMiniappChannelCredentialService : WorkspaceChannelCredentialServiceBase, IWechatMiniappChannelCredentialService
{
    private readonly WechatMiniappChannelCredentialRepository _credentialRepository;

    public WechatMiniappChannelCredentialService(
        WechatMiniappChannelCredentialRepository credentialRepository,
        WorkspacePublishChannelRepository channelRepository,
        LowCodeCredentialProtector protector,
        IIdGeneratorAccessor idGenerator)
        : base(channelRepository, protector, idGenerator)
    {
        _credentialRepository = credentialRepository;
    }

    public async Task<WechatMiniappChannelCredentialDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var entity = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<WechatMiniappChannelCredentialDto> UpsertAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        WechatMiniappChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AppId)) throw new BusinessException(ErrorCodes.ValidationError, "WechatMiniappAppIdRequired");
        if (string.IsNullOrWhiteSpace(request.AppSecret)) throw new BusinessException(ErrorCodes.ValidationError, "WechatMiniappAppSecretRequired");

        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var encAppSecret = Protector.Encrypt(request.AppSecret);
        var encAesKey = EncryptOrEmpty(Protector, request.EncodingAesKey);
        var originalId = request.OriginalId?.Trim() ?? string.Empty;
        var messageToken = request.MessageToken?.Trim() ?? string.Empty;

        var existing = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        if (existing is null)
        {
            var entity = new WechatMiniappChannelCredential(
                tenantId,
                channel.Id,
                channel.WorkspaceId,
                request.AppId.Trim(),
                encAppSecret,
                originalId,
                messageToken,
                encAesKey,
                "[]",
                IdGenerator.NextId());
            await _credentialRepository.AddAsync(entity, cancellationToken);
            return ToDto(entity);
        }

        existing.Update(
            appId: request.AppId.Trim(),
            appSecretEnc: encAppSecret,
            originalId: originalId,
            messageToken: messageToken,
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

    internal static WechatMiniappChannelCredentialDto ToDto(WechatMiniappChannelCredential entity)
    {
        return new WechatMiniappChannelCredentialDto(
            Id: entity.Id.ToString(),
            ChannelId: entity.ChannelId.ToString(),
            WorkspaceId: entity.WorkspaceId,
            AppId: entity.AppId,
            AppIdMasked: LowCodeCredentialProtector.Mask(entity.AppId),
            OriginalId: entity.OriginalId,
            MessageToken: entity.MessageToken,
            HasEncodingAesKey: !string.IsNullOrEmpty(entity.EncodingAesKeyEnc),
            AccessTokenExpiresAt: entity.AccessTokenExpiresAt > DateTime.UnixEpoch
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.AccessTokenExpiresAt, DateTimeKind.Utc))
                : null,
            RefreshCount: entity.RefreshCount,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }
}
