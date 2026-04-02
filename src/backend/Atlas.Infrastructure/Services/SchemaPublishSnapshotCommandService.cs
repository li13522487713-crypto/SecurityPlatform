using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class SchemaPublishSnapshotCommandService : ISchemaPublishSnapshotCommandService
{
    private readonly ISchemaPublishSnapshotRepository _snapshotRepository;
    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicIndexRepository _indexRepository;
    private readonly IIdGenerator _idGenerator;

    public SchemaPublishSnapshotCommandService(
        ISchemaPublishSnapshotRepository snapshotRepository,
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicIndexRepository indexRepository,
        IIdGenerator idGenerator)
    {
        _snapshotRepository = snapshotRepository;
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _indexRepository = indexRepository;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateSnapshotAsync(
        TenantId tenantId,
        long userId,
        SchemaPublishSnapshotCreateRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, request.TableKey, null, cancellationToken)
            ?? throw new BusinessException("NOT_FOUND", $"Table '{request.TableKey}' not found.");

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var indexes = await _indexRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);

        var snapshotPayload = new
        {
            tableKey = table.TableKey,
            displayName = table.DisplayName,
            description = table.Description,
            dbType = table.DbType.ToString(),
            fields = fields.Select(f => new
            {
                name = f.Name,
                displayName = f.DisplayName,
                fieldType = f.FieldType.ToString(),
                length = f.Length,
                precision = f.Precision,
                scale = f.Scale,
                allowNull = f.AllowNull,
                isPrimaryKey = f.IsPrimaryKey,
                isAutoIncrement = f.IsAutoIncrement,
                isUnique = f.IsUnique,
                defaultValue = f.DefaultValue,
                sortOrder = f.SortOrder,
                isComputed = f.IsComputed,
                computedExprId = f.ComputedExprId,
                isStatusField = f.IsStatusField,
                isRowVersionField = f.IsRowVersionField
            }),
            indexes = indexes.Select(i => new
            {
                name = i.Name,
                isUnique = i.IsUnique,
                fieldsJson = i.FieldsJson
            })
        };

        var snapshotJson = JsonSerializer.Serialize(snapshotPayload, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var now = DateTimeOffset.UtcNow;
        table.IncrementSchemaVersion(userId, now);
        await _tableRepository.UpdateAsync(table, cancellationToken);

        var snapshot = new SchemaPublishSnapshot(
            tenantId,
            table.Id,
            table.TableKey,
            table.SchemaVersion,
            snapshotJson,
            request.PublishNote,
            userId,
            _idGenerator.NextId(),
            now);

        await _snapshotRepository.AddAsync(snapshot, cancellationToken);
        return snapshot.Id;
    }
}
