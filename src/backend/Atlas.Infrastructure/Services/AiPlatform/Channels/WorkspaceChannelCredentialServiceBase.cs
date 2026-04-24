using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.LowCode;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels;

public abstract class WorkspaceChannelCredentialServiceBase
{
    private readonly WorkspacePublishChannelRepository _channelRepository;

    protected WorkspaceChannelCredentialServiceBase(
        WorkspacePublishChannelRepository channelRepository,
        LowCodeCredentialProtector protector,
        IIdGeneratorAccessor idGenerator)
    {
        _channelRepository = channelRepository;
        Protector = protector;
        IdGenerator = idGenerator;
    }

    protected LowCodeCredentialProtector Protector { get; }

    protected IIdGeneratorAccessor IdGenerator { get; }

    protected async Task<WorkspacePublishChannel> LoadChannelOrThrowAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
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

    protected static string EncryptOrEmpty(LowCodeCredentialProtector protector, string? raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? string.Empty : protector.Encrypt(raw);
    }
}
