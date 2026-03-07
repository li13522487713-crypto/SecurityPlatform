using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Infrastructure.Services;

public sealed class FieldPermissionResolver : IFieldPermissionResolver
{
    private readonly IFieldPermissionRepository _fieldPermissionRepository;
    private readonly IRbacResolver _rbacResolver;

    public FieldPermissionResolver(
        IFieldPermissionRepository fieldPermissionRepository,
        IRbacResolver rbacResolver)
    {
        _fieldPermissionRepository = fieldPermissionRepository;
        _rbacResolver = rbacResolver;
    }

    public async Task<IReadOnlyList<DynamicField>> FilterViewableFieldsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long? appId,
        IReadOnlyList<DynamicField> fields,
        CancellationToken cancellationToken)
    {
        var (rules, hasConfiguredRules) = await ResolveRulesAsync(tenantId, userId, tableKey, appId, cancellationToken);
        if (!hasConfiguredRules)
        {
            return fields;
        }
        if (rules.Count == 0)
        {
            return Array.Empty<DynamicField>();
        }

        var allowed = rules
            .Where(x => x.CanView)
            .Select(x => x.FieldName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return fields.Where(x => allowed.Contains(x.Name)).ToArray();
    }

    public async Task EnsureEditableFieldsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long? appId,
        IReadOnlyList<string> fieldsToEdit,
        CancellationToken cancellationToken)
    {
        if (fieldsToEdit.Count == 0)
        {
            return;
        }

        var (rules, hasConfiguredRules) = await ResolveRulesAsync(tenantId, userId, tableKey, appId, cancellationToken);
        if (!hasConfiguredRules)
        {
            return;
        }
        if (rules.Count == 0)
        {
            throw new BusinessException(ErrorCodes.Forbidden, "当前角色无字段编辑权限。");
        }

        var editable = rules
            .Where(x => x.CanEdit)
            .Select(x => x.FieldName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blocked = fieldsToEdit
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => !editable.Contains(x))
            .ToArray();
        if (blocked.Length > 0)
        {
            throw new BusinessException(ErrorCodes.Forbidden, $"字段无编辑权限：{string.Join(", ", blocked)}");
        }
    }

    private async Task<(IReadOnlyList<FieldPermission> Rules, bool HasConfiguredRules)> ResolveRulesAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long? appId,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _rbacResolver.GetRoleCodesAsync(tenantId, userId, cancellationToken);
        if (roleCodes.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            || roleCodes.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase))
        {
            return (Array.Empty<FieldPermission>(), false);
        }

        var allRules = await _fieldPermissionRepository.ListByTableKeyAsync(tenantId, tableKey, appId, cancellationToken);
        if (allRules.Count == 0)
        {
            return (Array.Empty<FieldPermission>(), false);
        }

        var roleCodeSet = roleCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = allRules.Where(x => roleCodeSet.Contains(x.RoleCode)).ToArray();
        return (rules, true);
    }
}
