using System.Globalization;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.Feishu.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// 飞书通讯录 Provider：子部门 + 部门直属成员 + 单用户详情 + 手机号/邮箱反查 user_id。
/// 根部门（"0"）查询要求应用拥有"全员通讯录"权限；权限不足时降级返回空集合而非抛出。
/// </summary>
public sealed class FeishuDirectoryProvider : IExternalDirectoryProvider
{
    private readonly FeishuApiClient _api;
    private readonly FeishuOptions _options;
    private readonly ILogger<FeishuDirectoryProvider> _logger;

    public FeishuDirectoryProvider(FeishuApiClient api, IOptions<FeishuOptions> options, ILogger<FeishuDirectoryProvider> logger)
    {
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderType => FeishuConnectorMarker.ProviderType;

    public async Task<IReadOnlyList<ExternalDepartment>> ListChildDepartmentsAsync(ConnectorContext context, string parentExternalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var collected = new List<ExternalDepartment>();
        string? pageToken = null;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = $"/open-apis/contact/v3/departments/{Uri.EscapeDataString(parentExternalDepartmentId)}/children?fetch_child={(recursive ? "true" : "false")}&page_size=50&department_id_type=open_department_id";
            if (!string.IsNullOrEmpty(pageToken))
            {
                path += $"&page_token={Uri.EscapeDataString(pageToken)}";
            }

            try
            {
                var resp = await _api.SendTenantGetAsync<FeishuChildDepartmentsData>(context, path, cancellationToken).ConfigureAwait(false);
                var items = resp.Data?.Items;
                if (items is null || items.Length == 0)
                {
                    break;
                }
                var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
                foreach (var d in items)
                {
                    collected.Add(MapDepartment(runtime, d));
                }
                pageToken = resp.Data!.HasMore ? resp.Data.PageToken : null;
            }
            catch (ConnectorException ex) when (IsScopeDenied(ex))
            {
                _logger.LogWarning("Feishu departments/children denied; returning collected so far ({Count}). code={Code}", collected.Count, ex.ProviderErrorCode);
                break;
            }
        }
        while (!string.IsNullOrEmpty(pageToken));

        return collected;
    }

    public async Task<ExternalDepartment?> GetDepartmentAsync(ConnectorContext context, string externalDepartmentId, CancellationToken cancellationToken)
    {
        var path = $"/open-apis/contact/v3/departments/{Uri.EscapeDataString(externalDepartmentId)}?department_id_type=open_department_id";
        try
        {
            var resp = await _api.SendTenantGetAsync<FeishuDepartmentSingle>(context, path, cancellationToken).ConfigureAwait(false);
            if (resp.Data?.Department is null)
            {
                return null;
            }
            var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
            return MapDepartment(runtime, resp.Data.Department);
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListDepartmentMemberIdsAsync(ConnectorContext context, string externalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var members = await ListDepartmentMembersAsync(context, externalDepartmentId, recursive, cancellationToken).ConfigureAwait(false);
        return members.Select(u => u.ExternalUserId).ToArray();
    }

    public async Task<IReadOnlyList<ExternalUserProfile>> ListDepartmentMembersAsync(ConnectorContext context, string externalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var collected = new List<ExternalUserProfile>();
        string? pageToken = null;
        var idType = _options.DefaultUserIdType;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = $"/open-apis/contact/v3/users/find_by_department?department_id={Uri.EscapeDataString(externalDepartmentId)}&user_id_type={idType}&department_id_type=open_department_id&page_size=50";
            if (!string.IsNullOrEmpty(pageToken))
            {
                path += $"&page_token={Uri.EscapeDataString(pageToken)}";
            }

            try
            {
                var resp = await _api.SendTenantGetAsync<FeishuDepartmentMembersData>(context, path, cancellationToken).ConfigureAwait(false);
                var items = resp.Data?.Items;
                if (items is null || items.Length == 0)
                {
                    break;
                }
                var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
                foreach (var u in items)
                {
                    collected.Add(MapUser(runtime, u));
                }
                pageToken = resp.Data!.HasMore ? resp.Data.PageToken : null;
            }
            catch (ConnectorException ex) when (IsScopeDenied(ex))
            {
                _logger.LogWarning("Feishu users/find_by_department denied; returning collected so far ({Count}). code={Code}", collected.Count, ex.ProviderErrorCode);
                break;
            }
        }
        while (!string.IsNullOrEmpty(pageToken));

        if (recursive)
        {
            var children = await ListChildDepartmentsAsync(context, externalDepartmentId, recursive: false, cancellationToken).ConfigureAwait(false);
            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var childMembers = await ListDepartmentMembersAsync(context, child.ExternalDepartmentId, recursive: true, cancellationToken).ConfigureAwait(false);
                collected.AddRange(childMembers);
            }
        }

        return collected;
    }

    public async Task<ExternalUserProfile?> GetUserAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken)
    {
        var idType = _options.DefaultUserIdType;
        var path = $"/open-apis/contact/v3/users/{Uri.EscapeDataString(externalUserId)}?user_id_type={idType}";
        try
        {
            var resp = await _api.SendTenantGetAsync<FeishuContactUserData>(context, path, cancellationToken).ConfigureAwait(false);
            var user = resp.Data?.User;
            if (user is null)
            {
                return null;
            }
            var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
            return MapUser(runtime, user);
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveExternalUserIdsAsync(ConnectorContext context, ExternalDirectoryLookupKind kind, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        if (values.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var mobiles = kind == ExternalDirectoryLookupKind.Mobile ? values : null;
        var emails = kind == ExternalDirectoryLookupKind.Email ? values : null;
        try
        {
            return await _api.BatchGetUserIdsAsync(context, mobiles, emails, _options.DefaultUserIdType, cancellationToken).ConfigureAwait(false);
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            _logger.LogWarning("Feishu batch_get_id denied due to visibility scope (code={Code}); returning empty.", ex.ProviderErrorCode);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static ExternalDepartment MapDepartment(FeishuRuntimeOptions runtime, FeishuDepartment d)
        => new()
        {
            ProviderType = FeishuConnectorMarker.ProviderType,
            ProviderTenantId = runtime.TenantKey ?? runtime.AppId,
            ExternalDepartmentId = d.OpenDepartmentId ?? d.DepartmentId ?? string.Empty,
            ParentExternalDepartmentId = d.ParentDepartmentId,
            Name = d.Name ?? string.Empty,
            Order = d.Order,
            LeaderExternalUserIds = string.IsNullOrEmpty(d.LeaderUserId) ? null : new[] { d.LeaderUserId! },
            RawJson = JsonSerializer.Serialize(d),
        };

    private static ExternalUserProfile MapUser(FeishuRuntimeOptions runtime, FeishuContactUser u)
        => new()
        {
            ProviderType = FeishuConnectorMarker.ProviderType,
            ProviderTenantId = runtime.TenantKey ?? runtime.AppId,
            ExternalUserId = u.UserId ?? u.OpenId ?? string.Empty,
            OpenId = u.OpenId,
            UnionId = u.UnionId,
            Name = u.Name ?? u.Nickname,
            EnglishName = u.EnName,
            Email = u.EnterpriseEmail ?? u.Email,
            Mobile = u.Mobile,
            Avatar = u.Avatar?.Avatar240 ?? u.Avatar?.Avatar72,
            Position = u.JobTitle,
            DepartmentIds = u.DepartmentIds,
            PrimaryDepartmentId = u.DepartmentIds is { Length: > 0 } ? u.DepartmentIds[0] : null,
            Status = u.Status is null ? null : string.Create(CultureInfo.InvariantCulture, $"activated={u.Status.IsActivated},resigned={u.Status.IsResigned},frozen={u.Status.IsFrozen}"),
            RawJson = JsonSerializer.Serialize(u),
        };

    private static bool IsScopeDenied(ConnectorException ex)
        => string.Equals(ex.Code, ConnectorErrorCodes.VisibilityScopeDenied, StringComparison.Ordinal);
}

internal sealed class FeishuDepartmentSingle
{
    [System.Text.Json.Serialization.JsonPropertyName("department")]
    public FeishuDepartment? Department { get; set; }
}
