using System.Globalization;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.DingTalk.Internal;
using Microsoft.Extensions.Logging;

namespace Atlas.Connectors.DingTalk;

/// <summary>
/// 钉钉通讯录 Provider：
/// - 子部门列表：v1 /topapi/v2/department/listsub；
/// - 部门成员 userid 列表：v1 /topapi/user/listid；
/// - 部门成员详情分页：v1 /topapi/v2/user/list；
/// - 成员详情：v1 /topapi/v2/user/get；
/// - 手机号反查 userid：v1 /topapi/v2/user/getbymobile。
/// 60011/60020/60121/60122 等权限错误自动降级为空集合。
/// </summary>
public sealed class DingTalkDirectoryProvider : IExternalDirectoryProvider
{
    private static readonly string[] EmptyArray = Array.Empty<string>();

    private readonly DingTalkApiClient _api;
    private readonly ILogger<DingTalkDirectoryProvider> _logger;

    public DingTalkDirectoryProvider(DingTalkApiClient api, ILogger<DingTalkDirectoryProvider> logger)
    {
        _api = api;
        _logger = logger;
    }

    public string ProviderType => DingTalkConnectorMarker.ProviderType;

    public async Task<IReadOnlyList<ExternalDepartment>> ListChildDepartmentsAsync(ConnectorContext context, string parentExternalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var parentId = ParseDeptId(parentExternalDepartmentId, defaultValue: 1);
        var body = new { dept_id = parentId };
        try
        {
            var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyDepartmentListResponse>(context, "/topapi/v2/department/listsub", body, cancellationToken).ConfigureAwait(false);
            var list = resp.Result?
                .Select(d => MapDepartment(context, d))
                .ToArray() ?? Array.Empty<ExternalDepartment>();
            return list;
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            _logger.LogWarning("DingTalk department/listsub denied due to visibility scope (errcode={Code}); returning empty.", ex.ProviderErrorCode);
            return Array.Empty<ExternalDepartment>();
        }
    }

    public async Task<ExternalDepartment?> GetDepartmentAsync(ConnectorContext context, string externalDepartmentId, CancellationToken cancellationToken)
    {
        var deptId = ParseDeptId(externalDepartmentId, defaultValue: 1);
        var body = new { dept_id = deptId, language = "zh_CN" };
        try
        {
            var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyGetDepartmentResponse>(context, "/topapi/v2/department/get", body, cancellationToken).ConfigureAwait(false);
            return resp.Result is null ? null : MapDepartment(context, resp.Result);
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListDepartmentMemberIdsAsync(ConnectorContext context, string externalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var deptId = ParseDeptId(externalDepartmentId, defaultValue: 1);
        var body = new { dept_id = deptId };
        try
        {
            var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyUserIdListResponse>(context, "/topapi/user/listid", body, cancellationToken).ConfigureAwait(false);
            return resp.Result?.UserIdList ?? EmptyArray;
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return EmptyArray;
        }
    }

    public async Task<IReadOnlyList<ExternalUserProfile>> ListDepartmentMembersAsync(ConnectorContext context, string externalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var runtime = DingTalkApiClient.ResolveRuntime(context);
        var deptId = ParseDeptId(externalDepartmentId, defaultValue: 1);
        var all = new List<ExternalUserProfile>();
        long cursor = 0;
        int pageSize = 100;
        try
        {
            while (true)
            {
                var body = new
                {
                    dept_id = deptId,
                    cursor,
                    size = pageSize,
                    order_field = "modify_desc",
                    contain_access_limit = false,
                    language = "zh_CN",
                };
                var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyUserListResponse>(context, "/topapi/v2/user/list", body, cancellationToken).ConfigureAwait(false);
                if (resp.Result?.List is { Length: > 0 } list)
                {
                    all.AddRange(list.Select(u => MapUser(runtime.CorpId ?? runtime.AppKey, u)));
                }
                if (resp.Result is null || !resp.Result.HasMore)
                {
                    break;
                }
                cursor = resp.Result.NextCursor;
            }
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return Array.Empty<ExternalUserProfile>();
        }
        return all;
    }

    public async Task<ExternalUserProfile?> GetUserAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken)
    {
        var runtime = DingTalkApiClient.ResolveRuntime(context);
        var body = new { userid = externalUserId, language = "zh_CN" };
        try
        {
            var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyUserDetailResponse>(context, "/topapi/v2/user/get", body, cancellationToken).ConfigureAwait(false);
            return resp.Result is null ? null : MapUser(runtime.CorpId ?? runtime.AppKey, resp.Result);
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex) || string.Equals(ex.Code, ConnectorErrorCodes.IdentityNotFound, StringComparison.Ordinal))
        {
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveExternalUserIdsAsync(ConnectorContext context, ExternalDirectoryLookupKind kind, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        if (values is null || values.Count == 0 || kind is not ExternalDirectoryLookupKind.Mobile)
        {
            // 钉钉仅 /topapi/v2/user/getbymobile 提供单个手机号 → userid，邮箱无对应接口；其他 kind 返回空。
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mobile in values.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            try
            {
                var body = new { mobile };
                var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyGetUserIdByMobileResponse>(context, "/topapi/v2/user/getbymobile", body, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(resp.Result?.UserId))
                {
                    dict[mobile] = resp.Result.UserId!;
                }
            }
            catch (ConnectorException ex) when (string.Equals(ex.Code, ConnectorErrorCodes.IdentityNotFound, StringComparison.Ordinal) || IsScopeDenied(ex))
            {
                // 不存在或不可见：跳过，下一个。
            }
        }
        return dict;
    }

    private static ExternalDepartment MapDepartment(ConnectorContext context, DingTalkDepartment d)
        => new()
        {
            ProviderType = DingTalkConnectorMarker.ProviderType,
            ProviderTenantId = context.TenantId.ToString("D"),
            ExternalDepartmentId = d.DeptId.ToString(CultureInfo.InvariantCulture),
            ParentExternalDepartmentId = d.ParentId.ToString(CultureInfo.InvariantCulture),
            Name = d.Name ?? string.Empty,
            FullPath = null,
            Order = d.Order,
            RawJson = JsonSerializer.Serialize(d),
        };

    private static ExternalUserProfile MapUser(string providerTenantId, DingTalkUserDetail u)
        => new()
        {
            ProviderType = DingTalkConnectorMarker.ProviderType,
            ProviderTenantId = providerTenantId,
            ExternalUserId = u.UserId ?? string.Empty,
            OpenId = null,
            UnionId = u.UnionId,
            Name = u.Name,
            Email = u.OrgEmail ?? u.Email,
            Mobile = u.Mobile,
            Avatar = u.Avatar,
            Position = u.Title,
            DepartmentIds = u.DeptIdList?.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray(),
            PrimaryDepartmentId = u.DeptIdList is { Length: > 0 } ? u.DeptIdList[0].ToString(CultureInfo.InvariantCulture) : null,
            Status = u.Active ? "active" : "inactive",
            RawJson = JsonSerializer.Serialize(u),
        };

    private static bool IsScopeDenied(ConnectorException ex)
        => string.Equals(ex.Code, ConnectorErrorCodes.VisibilityScopeDenied, StringComparison.Ordinal);

    private static long ParseDeptId(string? value, long defaultValue)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return id;
        }
        return defaultValue;
    }
}

internal sealed class DingTalkLegacyGetDepartmentResponse : DingTalkLegacyResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("result")]
    public DingTalkDepartment? Result { get; set; }
}
