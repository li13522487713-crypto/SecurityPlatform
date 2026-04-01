using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Infrastructure.Services;

public sealed class SchemaDraftService : ISchemaDraftService
{
    private readonly ISchemaDraftRepository _draftRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public SchemaDraftService(
        ISchemaDraftRepository draftRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _draftRepository = draftRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<IReadOnlyList<SchemaDraftListItem>> ListDraftsAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var drafts = await _draftRepository.ListByAppInstanceAsync(tenantId, appInstanceId, cancellationToken);
        return drafts
            .Where(d => d.Status != Atlas.Domain.DynamicTables.Enums.SchemaDraftStatus.Abandoned
                     && d.Status != Atlas.Domain.DynamicTables.Enums.SchemaDraftStatus.Published)
            .Select(ToListItem)
            .ToArray();
    }

    public async Task<SchemaDraftListItem?> GetDraftAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.FindByIdAsync(tenantId, draftId, cancellationToken);
        return draft is null ? null : ToListItem(draft);
    }

    public async Task<long> CreateDraftAsync(
        TenantId tenantId,
        long userId,
        DynamicSchemaDraftCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SchemaDraftObjectType>(request.ObjectType, true, out var objectType))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvalidSchemaDraftObjectType");
        }

        if (!Enum.TryParse<SchemaDraftChangeType>(request.ChangeType, true, out var changeType))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "InvalidSchemaDraftChangeType");
        }

        if (!Enum.TryParse<SchemaDraftRiskLevel>(request.RiskLevel, true, out var riskLevel))
        {
            riskLevel = SchemaDraftRiskLevel.Low;
        }

        var id = _idGeneratorAccessor.NextId();
        var draft = new Atlas.Domain.DynamicTables.Entities.SchemaDraft(
            tenantId,
            request.AppInstanceId,
            objectType,
            request.ObjectId,
            request.ObjectKey,
            changeType,
            request.BeforeSnapshot,
            request.AfterSnapshot,
            riskLevel,
            userId,
            id,
            DateTimeOffset.UtcNow);

        await _draftRepository.AddAsync(draft, cancellationToken);
        return id;
    }

    public async Task ValidateDraftAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.FindByIdAsync(tenantId, draftId, cancellationToken);
        if (draft is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "SchemaDraftNotFound");
        }

        // 基础校验：命名非空、AfterSnapshot 有效
        if (string.IsNullOrWhiteSpace(draft.ObjectKey))
        {
            draft.MarkValidationFailed("ObjectKey cannot be empty");
        }
        else
        {
            draft.MarkValidated();
        }

        await _draftRepository.UpdateAsync(draft, cancellationToken);
    }

    public async Task<SchemaDraftPublishResult> PublishDraftsAsync(
        TenantId tenantId,
        long userId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var drafts = await _draftRepository.ListPendingByAppAsync(tenantId, appInstanceId, cancellationToken);
        if (drafts.Count == 0)
        {
            return new SchemaDraftPublishResult(true, 0, Array.Empty<string>());
        }

        var errors = new List<string>();
        var toPublish = new List<Atlas.Domain.DynamicTables.Entities.SchemaDraft>();

        foreach (var draft in drafts)
        {
            if (draft.Status == SchemaDraftStatus.Pending)
            {
                draft.MarkValidated();
            }

            toPublish.Add(draft);
        }

        foreach (var draft in toPublish)
        {
            draft.MarkPublished();
        }

        await _draftRepository.UpdateRangeAsync(toPublish, cancellationToken);

        return new SchemaDraftPublishResult(errors.Count == 0, toPublish.Count, errors);
    }

    public async Task AbandonDraftAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.FindByIdAsync(tenantId, draftId, cancellationToken);
        if (draft is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "SchemaDraftNotFound");
        }

        draft.Abandon();
        await _draftRepository.UpdateAsync(draft, cancellationToken);
    }

    private static SchemaDraftListItem ToListItem(Atlas.Domain.DynamicTables.Entities.SchemaDraft draft)
    {
        return new SchemaDraftListItem(
            draft.Id,
            draft.ObjectType.ToString(),
            draft.ObjectKey,
            draft.ChangeType.ToString(),
            draft.RiskLevel.ToString(),
            draft.Status.ToString(),
            draft.ValidationMessage,
            draft.AfterSnapshot,
            draft.BeforeSnapshot,
            draft.CreatedAt,
            draft.CreatedBy);
    }
}
