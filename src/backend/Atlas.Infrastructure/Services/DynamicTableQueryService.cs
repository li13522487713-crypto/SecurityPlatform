using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicTableQueryService : IDynamicTableQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicIndexRepository _indexRepository;

    public DynamicTableQueryService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicIndexRepository indexRepository)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _indexRepository = indexRepository;
    }

    public async Task<PagedResult<DynamicTableListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;

        var (items, total) = await _tableRepository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);

        var list = items.Select(item => new DynamicTableListItem(
            item.Id.ToString(),
            item.TableKey,
            item.DisplayName,
            item.Description,
            item.DbType.ToString(),
            item.Status.ToString(),
            item.CreatedAt,
            item.CreatedBy,
            item.ApprovalFlowDefinitionId,
            item.ApprovalStatusField)).ToArray();

        return new PagedResult<DynamicTableListItem>(list, total, pageIndex, pageSize);
    }

    public async Task<DynamicTableDetail?> GetByKeyAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            return null;
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var indexes = await _indexRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);

        return new DynamicTableDetail(
            table.Id.ToString(),
            table.TableKey,
            table.DisplayName,
            table.Description,
            table.DbType.ToString(),
            table.Status.ToString(),
            table.CreatedAt,
            table.UpdatedAt,
            table.CreatedBy,
            table.UpdatedBy,
            fields.Select(ToFieldDefinition).ToArray(),
            indexes.Select(ToIndexDefinition).ToArray(),
            table.ApprovalFlowDefinitionId,
            table.ApprovalStatusField);
    }

    public async Task<IReadOnlyList<DynamicFieldDefinition>> GetFieldsAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            return Array.Empty<DynamicFieldDefinition>();
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        return fields.Select(ToFieldDefinition).ToArray();
    }

    public Task<IReadOnlyList<DynamicFieldTypeOption>> GetFieldTypesAsync(
        string dbType,
        CancellationToken cancellationToken)
    {
        var types = new[]
        {
            new DynamicFieldTypeOption("Int", "Int"),
            new DynamicFieldTypeOption("Long", "Long"),
            new DynamicFieldTypeOption("Decimal", "Decimal"),
            new DynamicFieldTypeOption("String", "String"),
            new DynamicFieldTypeOption("Text", "Text"),
            new DynamicFieldTypeOption("Bool", "Bool"),
            new DynamicFieldTypeOption("DateTime", "DateTime"),
            new DynamicFieldTypeOption("Date", "Date")
        };

        return Task.FromResult<IReadOnlyList<DynamicFieldTypeOption>>(types);
    }

    private static DynamicFieldDefinition ToFieldDefinition(DynamicField field)
    {
        return new DynamicFieldDefinition(
            field.Name,
            string.IsNullOrWhiteSpace(field.DisplayName) ? null : field.DisplayName,
            field.FieldType.ToString(),
            field.Length,
            field.Precision,
            field.Scale,
            field.AllowNull,
            field.IsPrimaryKey,
            field.IsAutoIncrement,
            field.IsUnique,
            field.DefaultValue,
            field.SortOrder);
    }

    private static DynamicIndexDefinition ToIndexDefinition(DynamicIndex index)
    {
        var fields = DeserializeFields(index.FieldsJson);
        return new DynamicIndexDefinition(index.Name, index.IsUnique, fields);
    }

    private static IReadOnlyList<string> DeserializeFields(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
