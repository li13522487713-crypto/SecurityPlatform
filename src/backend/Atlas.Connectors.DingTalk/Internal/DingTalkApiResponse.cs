using System.Text.Json.Serialization;

namespace Atlas.Connectors.DingTalk.Internal;

/// <summary>
/// 钉钉 v1 旧版 OpenAPI（oapi.dingtalk.com）通用响应包装。errcode == 0 表示成功。
/// </summary>
internal class DingTalkLegacyResponse
{
    [JsonPropertyName("errcode")]
    public int ErrCode { get; set; }

    [JsonPropertyName("errmsg")]
    public string? ErrMsg { get; set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}

/// <summary>v1.0 新版 OpenAPI（api.dingtalk.com）access_token 响应。</summary>
internal sealed class DingTalkAccessTokenResponse
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expireIn")]
    public int ExpireIn { get; set; }
}

internal sealed class DingTalkLegacyGetUserInfoResponse : DingTalkLegacyResponse
{
    [JsonPropertyName("result")]
    public DingTalkUserInfoResult? Result { get; set; }
}

internal sealed class DingTalkUserInfoResult
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("sys")]
    public bool Sys { get; set; }

    [JsonPropertyName("associated_unionid")]
    public string? AssociatedUnionId { get; set; }

    [JsonPropertyName("unionid")]
    public string? UnionId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class DingTalkLegacyUserDetailResponse : DingTalkLegacyResponse
{
    [JsonPropertyName("result")]
    public DingTalkUserDetail? Result { get; set; }
}

internal sealed class DingTalkUserDetail
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("unionid")]
    public string? UnionId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("org_email")]
    public string? OrgEmail { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("dept_id_list")]
    public long[]? DeptIdList { get; set; }
}

internal sealed class DingTalkLegacyDepartmentListResponse : DingTalkLegacyResponse
{
    [JsonPropertyName("result")]
    public DingTalkDepartment[]? Result { get; set; }
}

internal sealed class DingTalkDepartment
{
    [JsonPropertyName("dept_id")]
    public long DeptId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("parent_id")]
    public long ParentId { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

internal sealed class DingTalkLegacyUserListResponse : DingTalkLegacyResponse
{
    [JsonPropertyName("result")]
    public DingTalkUserListResult? Result { get; set; }
}

internal sealed class DingTalkUserListResult
{
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("next_cursor")]
    public long NextCursor { get; set; }

    [JsonPropertyName("list")]
    public DingTalkUserDetail[]? List { get; set; }
}

internal sealed class DingTalkLegacyUserIdListResponse : DingTalkLegacyResponse
{
    [JsonPropertyName("result")]
    public DingTalkUserIdListResult? Result { get; set; }
}

internal sealed class DingTalkUserIdListResult
{
    [JsonPropertyName("userid_list")]
    public string[]? UserIdList { get; set; }
}

internal sealed class DingTalkLegacyGetUserIdByMobileResponse : DingTalkLegacyResponse
{
    [JsonPropertyName("result")]
    public DingTalkGetUserIdByMobileResult? Result { get; set; }
}

internal sealed class DingTalkGetUserIdByMobileResult
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("exclusive_account_user_id_list")]
    public string[]? ExclusiveAccountUserIdList { get; set; }
}

internal sealed class DingTalkCreateProcessInstanceResponse
{
    [JsonPropertyName("instanceId")]
    public string? InstanceId { get; set; }
}

internal sealed class DingTalkProcessInstanceDetailResponse
{
    [JsonPropertyName("result")]
    public DingTalkProcessInstance? Result { get; set; }
}

internal sealed class DingTalkProcessInstance
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("processCode")]
    public string? ProcessCode { get; set; }

    [JsonPropertyName("originatorUserId")]
    public string? OriginatorUserId { get; set; }

    [JsonPropertyName("createTime")]
    public string? CreateTime { get; set; }

    [JsonPropertyName("finishTime")]
    public string? FinishTime { get; set; }
}

internal sealed class DingTalkSendWorkNoticeResponse : DingTalkLegacyResponse
{
    [JsonPropertyName("task_id")]
    public long TaskId { get; set; }

    [JsonPropertyName("request_id")]
    public new string? RequestId { get; set; }
}
