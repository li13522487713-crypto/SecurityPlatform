using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Channels;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.LowCode;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels.Wechat;

public sealed class WechatMpChannelCredentialService : IWechatMpChannelCredentialService
{
    private readonly WechatMpChannelCredentialRepository _credentialRepository;
    private readonly WorkspacePublishChannelRepository _channelRepository;
    private readonly LowCodeCredentialProtector _protector;
    private readonly IIdGeneratorAccessor _idGenerator;

    public WechatMpChannelCredentialService(
        WechatMpChannelCredentialRepository credentialRepository,
        WorkspacePublishChannelRepository channelRepository,
        LowCodeCredentialProtector protector,
        IIdGeneratorAccessor idGenerator)
    {
        _credentialRepository = credentialRepository;
        _channelRepository = channelRepository;
        _protector = protector;
        _idGenerator = idGenerator;
    }

    public async Task<WechatMpChannelCredentialDto?> GetAsync(TenantId tenantId, string workspaceId, string channelId, CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var entity = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<WechatMpChannelCredentialDto> UpsertAsync(TenantId tenantId, string workspaceId, string channelId, WechatMpChannelCredentialUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AppId)) throw new BusinessException(ErrorCodes.ValidationError, "WechatMpAppIdRequired");
        if (string.IsNullOrWhiteSpace(request.AppSecret)) throw new BusinessException(ErrorCodes.ValidationError, "WechatMpAppSecretRequired");
        if (string.IsNullOrWhiteSpace(request.Token)) throw new BusinessException(ErrorCodes.ValidationError, "WechatMpTokenRequired");

        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var encAppSecret = _protector.Encrypt(request.AppSecret);
        var encAesKey = string.IsNullOrEmpty(request.EncodingAesKey) ? string.Empty : _protector.Encrypt(request.EncodingAesKey!);

        var existing = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        if (existing is null)
        {
            var entity = new WechatMpChannelCredential(
                tenantId,
                channel.Id,
                channel.WorkspaceId,
                request.AppId.Trim(),
                encAppSecret,
                request.Token.Trim(),
                encAesKey,
                "[]",
                _idGenerator.NextId());
            await _credentialRepository.AddAsync(entity, cancellationToken);
            return ToDto(entity);
        }

        existing.Update(
            appId: request.AppId.Trim(),
            appSecretEnc: encAppSecret,
            token: request.Token.Trim(),
            encodingAesKeyEnc: encAesKey,
            agentBindingsJson: existing.AgentBindingsJson);
        await _credentialRepository.UpdateAsync(existing, cancellationToken);
        return ToDto(existing);
    }

    public async Task DeleteAsync(TenantId tenantId, string workspaceId, string channelId, CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        var existing = await _credentialRepository.FindByChannelAsync(tenantId, channel.Id, cancellationToken);
        if (existing is null) return;
        await _credentialRepository.DeleteAsync(existing, cancellationToken);
    }

    private async Task<Domain.AiPlatform.Entities.WorkspacePublishChannel> LoadChannelOrThrowAsync(TenantId tenantId, string workspaceId, string channelId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(channelId, out var id))
        {
            throw new BusinessException(ErrorCodes.NotFound, "ChannelNotFound");
        }
        var entity = await _channelRepository.FindAsync(tenantId, workspaceId, id, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "ChannelNotFound");
        }
        return entity;
    }

    internal static WechatMpChannelCredentialDto ToDto(WechatMpChannelCredential entity)
    {
        return new WechatMpChannelCredentialDto(
            Id: entity.Id.ToString(),
            ChannelId: entity.ChannelId.ToString(),
            WorkspaceId: entity.WorkspaceId,
            AppId: entity.AppId,
            AppIdMasked: LowCodeCredentialProtector.Mask(entity.AppId),
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
