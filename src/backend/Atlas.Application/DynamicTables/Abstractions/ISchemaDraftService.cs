using Atlas.Core.Tenancy;
using Atlas.Application.DynamicTables.Models;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface ISchemaDraftService
{
    Task<IReadOnlyList<SchemaDraftListItem>> ListDraftsAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken);

    Task<SchemaDraftListItem?> GetDraftAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken);

    Task<long> CreateDraftAsync(
        TenantId tenantId,
        long userId,
        DynamicSchemaDraftCreateRequest request,
        CancellationToken cancellationToken);

    Task ValidateDraftAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken);

    Task<SchemaDraftPublishResult> PublishDraftsAsync(
        TenantId tenantId,
        long userId,
        long appInstanceId,
        CancellationToken cancellationToken);

    Task AbandonDraftAsync(
        TenantId tenantId,
        long draftId,
        CancellationToken cancellationToken);
}
