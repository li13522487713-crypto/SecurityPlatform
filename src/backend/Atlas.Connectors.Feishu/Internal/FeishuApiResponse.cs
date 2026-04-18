using System.Text.Json.Serialization;

namespace Atlas.Connectors.Feishu.Internal;

/// <summary>
/// 飞书 OpenAPI 通用响应包装：{ code, msg, data }。code == 0 表示成功。
/// </summary>
internal class FeishuApiResponse<TData>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("data")]
    public TData? Data { get; set; }
}

internal sealed class FeishuTenantAccessTokenResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("tenant_access_token")]
    public string? TenantAccessToken { get; set; }

    [JsonPropertyName("expire")]
    public int Expire { get; set; }
}

internal sealed class FeishuUserAccessTokenResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("data")]
    public FeishuUserAccessTokenData? Data { get; set; }
}

internal sealed class FeishuUserAccessTokenData
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
}

internal sealed class FeishuUserInfoData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("en_name")]
    public string? EnName { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("open_id")]
    public string? OpenId { get; set; }

    [JsonPropertyName("union_id")]
    public string? UnionId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("enterprise_email")]
    public string? EnterpriseEmail { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("tenant_key")]
    public string? TenantKey { get; set; }

    [JsonPropertyName("employee_no")]
    public string? EmployeeNo { get; set; }
}

internal sealed class FeishuBatchGetIdData
{
    [JsonPropertyName("user_list")]
    public FeishuBatchGetIdEntry[]? UserList { get; set; }
}

internal sealed class FeishuBatchGetIdEntry
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

internal sealed class FeishuContactUserData
{
    [JsonPropertyName("user")]
    public FeishuContactUser? User { get; set; }
}

internal sealed class FeishuContactUser
{
    [JsonPropertyName("union_id")]
    public string? UnionId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("open_id")]
    public string? OpenId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("en_name")]
    public string? EnName { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("enterprise_email")]
    public string? EnterpriseEmail { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("avatar")]
    public FeishuAvatar? Avatar { get; set; }

    [JsonPropertyName("status")]
    public FeishuUserStatus? Status { get; set; }

    [JsonPropertyName("department_ids")]
    public string[]? DepartmentIds { get; set; }

    [JsonPropertyName("job_title")]
    public string? JobTitle { get; set; }
}

internal sealed class FeishuAvatar
{
    [JsonPropertyName("avatar_72")]
    public string? Avatar72 { get; set; }

    [JsonPropertyName("avatar_240")]
    public string? Avatar240 { get; set; }
}

internal sealed class FeishuUserStatus
{
    [JsonPropertyName("is_activated")]
    public bool IsActivated { get; set; }

    [JsonPropertyName("is_resigned")]
    public bool IsResigned { get; set; }

    [JsonPropertyName("is_frozen")]
    public bool IsFrozen { get; set; }
}

internal sealed class FeishuChildDepartmentsData
{
    [JsonPropertyName("items")]
    public FeishuDepartment[]? Items { get; set; }

    [JsonPropertyName("page_token")]
    public string? PageToken { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

internal sealed class FeishuDepartment
{
    [JsonPropertyName("department_id")]
    public string? DepartmentId { get; set; }

    [JsonPropertyName("open_department_id")]
    public string? OpenDepartmentId { get; set; }

    [JsonPropertyName("parent_department_id")]
    public string? ParentDepartmentId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("leader_user_id")]
    public string? LeaderUserId { get; set; }
}

internal sealed class FeishuDepartmentMembersData
{
    [JsonPropertyName("items")]
    public FeishuContactUser[]? Items { get; set; }

    [JsonPropertyName("page_token")]
    public string? PageToken { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}
