using Atlas.Application.DynamicTables;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// Validates dynamic record payloads against field metadata (type, nullability, length).
/// Phase 1 implementation: metadata-driven; Phase 2 will add expression-based rules.
/// </summary>
public sealed class DynamicFormValidationService : IDynamicFormValidationService
{
    private static readonly HashSet<string> SystemManagedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "created_at", "updated_at", "created_by", "updated_by", "is_deleted"
    };

    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAppContextAccessor _appContextAccessor;

    public DynamicFormValidationService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        ITenantProvider tenantProvider,
        IAppContextAccessor appContextAccessor)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _tenantProvider = tenantProvider;
        _appContextAccessor = appContextAccessor;
    }

    public async Task<bool> ValidateAsync(
        string tableKey,
        IDictionary<string, object> payload,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var table = await _tableRepository.FindByKeyAsync(
            tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);

        if (table is null)
        {
            return false;
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            return false;
        }

        var fieldLookup = fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            if (field.IsPrimaryKey || field.IsAutoIncrement)
            {
                continue;
            }

            if (SystemManagedFields.Contains(field.Name))
            {
                continue;
            }

            if (!field.AllowNull && !payload.ContainsKey(field.Name))
            {
                return false;
            }
        }

        foreach (var (key, value) in payload)
        {
            if (!fieldLookup.TryGetValue(key, out var fieldDef))
            {
                continue;
            }

            if (value is null)
            {
                if (!fieldDef.AllowNull)
                {
                    return false;
                }

                continue;
            }

            if (!IsTypeCompatible(fieldDef.FieldType, value))
            {
                return false;
            }

            if (fieldDef.Length.HasValue && value is string s && s.Length > fieldDef.Length.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTypeCompatible(DynamicFieldType fieldType, object value)
    {
        return fieldType switch
        {
            DynamicFieldType.Int => value is int or long or short or byte,
            DynamicFieldType.Long => value is long or int or short or byte,
            DynamicFieldType.Decimal => value is decimal or double or float or int or long,
            DynamicFieldType.String or DynamicFieldType.Text or DynamicFieldType.Json
                or DynamicFieldType.File or DynamicFieldType.Image or DynamicFieldType.Guid => value is string,
            DynamicFieldType.Bool => value is bool,
            DynamicFieldType.DateTime or DynamicFieldType.Date or DynamicFieldType.Time =>
                value is DateTimeOffset or DateTime or string,
            DynamicFieldType.Enum => value is string or int or long,
            _ => true
        };
    }
}
