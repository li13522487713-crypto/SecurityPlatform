using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class MigrationService : IMigrationService
{
    private readonly IMigrationRecordRepository _migrationRecordRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public MigrationService(
        IMigrationRecordRepository migrationRecordRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _migrationRecordRepository = migrationRecordRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<PagedResult<MigrationRecordListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        string? tableKey,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _migrationRecordRepository.QueryPageAsync(
            tenantId,
            request.PageIndex,
            request.PageSize,
            request.Keyword,
            tableKey,
            cancellationToken);

        var listItems = items
            .Select(x => new MigrationRecordListItem(
                x.Id.ToString(),
                x.TableKey,
                x.Version,
                x.Status,
                x.IsDestructive,
                x.CreatedAt,
                x.UpdatedAt,
                x.ExecutedAt,
                x.CreatedBy,
                x.ErrorMessage))
            .ToArray();

        return new PagedResult<MigrationRecordListItem>(listItems, totalCount, request.PageIndex, request.PageSize);
    }

    public async Task<MigrationRecordDetail?> GetByIdAsync(
        TenantId tenantId,
        long migrationId,
        CancellationToken cancellationToken)
    {
        var entity = await _migrationRecordRepository.FindByIdAsync(tenantId, migrationId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new MigrationRecordDetail(
            entity.Id.ToString(),
            entity.TableKey,
            entity.Version,
            entity.Status,
            entity.UpScript,
            entity.DownScript,
            entity.IsDestructive,
            entity.ErrorMessage,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ExecutedAt,
            entity.CreatedBy,
            entity.UpdatedBy);
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        MigrationRecordCreateRequest request,
        CancellationToken cancellationToken)
    {
        var existed = await _migrationRecordRepository.FindByVersionAsync(
            tenantId,
            request.TableKey,
            request.Version,
            cancellationToken);
        if (existed is not null)
        {
            throw new BusinessException("同一表的迁移版本已存在。", ErrorCodes.ValidationError);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new MigrationRecord(
            tenantId,
            request.TableKey,
            request.Version,
            request.UpScript,
            request.DownScript,
            request.IsDestructive,
            userId,
            _idGeneratorAccessor.NextId(),
            now);

        await _migrationRecordRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }
}
