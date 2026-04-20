using System.Globalization;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.WeCom.Internal;
using Microsoft.Extensions.Logging;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 企业微信通讯录 Provider：部门列表/子部门 ID/部门成员/成员详情/成员 ID 列表 + 手机号/邮箱反查（兜底通过详情匹配）。
/// 60011 / 60020 等"应用可见范围不足"错误自动降级为空集合，避免阻塞同步流程。
/// </summary>
public sealed class WeComDirectoryProvider : IExternalDirectoryProvider
{
    private static readonly string[] EmptyArray = Array.Empty<string>();

    private readonly WeComApiClient _api;
    private readonly ILogger<WeComDirectoryProvider> _logger;

    public WeComDirectoryProvider(WeComApiClient api, ILogger<WeComDirectoryProvider> logger)
    {
        _api = api;
        _logger = logger;
    }

    public string ProviderType => WeComConnectorMarker.ProviderType;

    public async Task<IReadOnlyList<ExternalDepartment>> ListChildDepartmentsAsync(ConnectorContext context, string parentExternalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(parentExternalDepartmentId))
        {
            query["id"] = parentExternalDepartmentId;
        }

        try
        {
            // 优先走 department/list（返回结构更全），不支持时再降级 simplelist
            var resp = await _api.SendAuthorizedGetAsync<WeComDepartmentListResponse>(context, "/cgi-bin/department/list", query, cancellationToken).ConfigureAwait(false);
            return MapDepartments(context, resp.Department);
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            _logger.LogWarning("WeCom department/list denied due to visibility scope (errcode={Code}); returning empty.", ex.ProviderErrorCode);
            return Array.Empty<ExternalDepartment>();
        }
    }

    public async Task<ExternalDepartment?> GetDepartmentAsync(ConnectorContext context, string externalDepartmentId, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = externalDepartmentId };
        try
        {
            var resp = await _api.SendAuthorizedGetAsync<WeComDepartmentListResponse>(context, "/cgi-bin/department/list", query, cancellationToken).ConfigureAwait(false);
            var first = resp.Department?.FirstOrDefault(d => d.Id.ToString(CultureInfo.InvariantCulture) == externalDepartmentId);
            return first is null ? null : MapDepartment(context, first);
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListDepartmentMemberIdsAsync(ConnectorContext context, string externalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["department_id"] = externalDepartmentId,
            ["fetch_child"] = recursive ? "1" : "0",
        };
        try
        {
            var resp = await _api.SendAuthorizedGetAsync<WeComDepartmentMemberSimpleResponse>(context, "/cgi-bin/user/simplelist", query, cancellationToken).ConfigureAwait(false);
            if (resp.UserList is null || resp.UserList.Length == 0)
            {
                return EmptyArray;
            }
            return resp.UserList.Where(u => !string.IsNullOrEmpty(u.UserId)).Select(u => u.UserId!).ToArray();
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return EmptyArray;
        }
    }

    public async Task<IReadOnlyList<ExternalUserProfile>> ListDepartmentMembersAsync(ConnectorContext context, string externalDepartmentId, bool recursive, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["department_id"] = externalDepartmentId,
            ["fetch_child"] = recursive ? "1" : "0",
        };
        try
        {
            var runtime = WeComApiClient.ResolveRuntime(context);
            var resp = await _api.SendAuthorizedGetAsync<WeComDepartmentMemberDetailResponse>(context, "/cgi-bin/user/list", query, cancellationToken).ConfigureAwait(false);
            if (resp.UserList is null || resp.UserList.Length == 0)
            {
                return Array.Empty<ExternalUserProfile>();
            }
            return resp.UserList.Where(u => !string.IsNullOrEmpty(u.UserId))
                .Select(u => MapUser(runtime.CorpId, u))
                .ToArray();
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return Array.Empty<ExternalUserProfile>();
        }
    }

    public async Task<ExternalUserProfile?> GetUserAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["userid"] = externalUserId };
        try
        {
            var runtime = WeComApiClient.ResolveRuntime(context);
            var detail = await _api.SendAuthorizedGetAsync<WeComUserDetailResponse>(context, "/cgi-bin/user/get", query, cancellationToken).ConfigureAwait(false);
            return MapUser(runtime.CorpId, detail);
        }
        catch (ConnectorException ex) when (IsScopeDenied(ex))
        {
            return null;
        }
    }

    public Task<IReadOnlyDictionary<string, string>> ResolveExternalUserIdsAsync(ConnectorContext context, ExternalDirectoryLookupKind kind, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        // 企微未公开"按手机号 / 邮箱批量取 userid"接口；常用做法是走 user/getuserid（仅手机号），且需要单独申请权限。
        // 这里返回空字典，提示业务层走"目录全量同步 + 本地反查 ExternalUserMirror"路径。
        _logger.LogDebug("WeCom does not provide a generic batch_get_id by mobile/email; caller should query ExternalUserMirror instead.");
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<ExternalDepartment> MapDepartments(ConnectorContext context, WeComDepartment[]? source)
    {
        if (source is null || source.Length == 0)
        {
            return Array.Empty<ExternalDepartment>();
        }
        var list = new List<ExternalDepartment>(source.Length);
        foreach (var d in source)
        {
            list.Add(MapDepartment(context, d));
        }
        return list;
    }

    private static ExternalDepartment MapDepartment(ConnectorContext context, WeComDepartment d)
        => new()
        {
            ProviderType = WeComConnectorMarker.ProviderType,
            ProviderTenantId = context.TenantId.ToString("D"),
            ExternalDepartmentId = d.Id.ToString(CultureInfo.InvariantCulture),
            ParentExternalDepartmentId = d.ParentId.ToString(CultureInfo.InvariantCulture),
            Name = d.Name ?? string.Empty,
            FullPath = null,
            Order = d.Order,
            LeaderExternalUserIds = d.DepartmentLeader,
            RawJson = JsonSerializer.Serialize(d),
        };

    private static ExternalUserProfile MapUser(string corpId, WeComUserDetailResponse u)
        => new()
        {
            ProviderType = WeComConnectorMarker.ProviderType,
            ProviderTenantId = corpId,
            ExternalUserId = u.UserId ?? string.Empty,
            OpenId = null,
            UnionId = u.OpenUserId,
            Name = u.Name,
            EnglishName = u.EnglishName,
            Email = u.Email ?? u.BizMail,
            Mobile = u.Mobile,
            Avatar = u.Avatar,
            Position = u.Position,
            DepartmentIds = u.Departments?.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray(),
            PrimaryDepartmentId = u.MainDepartment > 0 ? u.MainDepartment.ToString(CultureInfo.InvariantCulture) : null,
            Status = u.Status.ToString(CultureInfo.InvariantCulture),
            RawJson = JsonSerializer.Serialize(u),
        };

    private static bool IsScopeDenied(ConnectorException ex)
        => string.Equals(ex.Code, ConnectorErrorCodes.VisibilityScopeDenied, StringComparison.Ordinal);
}
