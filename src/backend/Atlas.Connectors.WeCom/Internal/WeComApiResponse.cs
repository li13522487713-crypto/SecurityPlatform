using System.Text.Json.Serialization;

namespace Atlas.Connectors.WeCom.Internal;

/// <summary>
/// 企微 API 通用响应包装。errcode == 0 表示成功，其他码必须由 provider 映射到 ConnectorErrorCodes。
/// </summary>
internal class WeComApiResponse
{
    [JsonPropertyName("errcode")]
    public int ErrCode { get; set; }

    [JsonPropertyName("errmsg")]
    public string? ErrMsg { get; set; }
}

internal sealed class WeComAccessTokenResponse : WeComApiResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

internal sealed class WeComGetUserInfoResponse : WeComApiResponse
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("openid")]
    public string? OpenId { get; set; }

    [JsonPropertyName("user_ticket")]
    public string? UserTicket { get; set; }

    [JsonPropertyName("external_userid")]
    public string? ExternalUserId { get; set; }
}

internal sealed class WeComUserDetailResponse : WeComApiResponse
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("english_name")]
    public string? EnglishName { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("biz_mail")]
    public string? BizMail { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("main_department")]
    public long MainDepartment { get; set; }

    [JsonPropertyName("department")]
    public long[]? Departments { get; set; }

    [JsonPropertyName("is_leader_in_dept")]
    public int[]? IsLeaderInDept { get; set; }

    [JsonPropertyName("order")]
    public long[]? Order { get; set; }

    [JsonPropertyName("open_userid")]
    public string? OpenUserId { get; set; }
}

internal sealed class WeComConvertOpenIdResponse : WeComApiResponse
{
    [JsonPropertyName("openid")]
    public string? OpenId { get; set; }
}

internal sealed class WeComConvertUserIdResponse : WeComApiResponse
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }
}

internal sealed class WeComBatchConvertResponse : WeComApiResponse
{
    [JsonPropertyName("open_userid_list")]
    public WeComOpenUserIdEntry[]? OpenUserIdList { get; set; }

    [JsonPropertyName("invalid_userid_list")]
    public string[]? InvalidUserIdList { get; set; }
}

internal sealed class WeComOpenUserIdEntry
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("open_userid")]
    public string? OpenUserId { get; set; }
}

internal sealed class WeComDepartmentListResponse : WeComApiResponse
{
    [JsonPropertyName("department")]
    public WeComDepartment[]? Department { get; set; }
}

internal sealed class WeComDepartment
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("name_en")]
    public string? NameEn { get; set; }

    [JsonPropertyName("parentid")]
    public long ParentId { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("department_leader")]
    public string[]? DepartmentLeader { get; set; }
}

internal sealed class WeComDepartmentSimpleListResponse : WeComApiResponse
{
    [JsonPropertyName("department_id")]
    public WeComDepartmentSimple[]? DepartmentId { get; set; }
}

internal sealed class WeComDepartmentSimple
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("parentid")]
    public long ParentId { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

internal sealed class WeComDepartmentMemberSimpleResponse : WeComApiResponse
{
    [JsonPropertyName("userlist")]
    public WeComMemberSimple[]? UserList { get; set; }
}

internal sealed class WeComMemberSimple
{
    [JsonPropertyName("userid")]
    public string? UserId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("department")]
    public long[]? Department { get; set; }

    [JsonPropertyName("open_userid")]
    public string? OpenUserId { get; set; }
}

internal sealed class WeComDepartmentMemberDetailResponse : WeComApiResponse
{
    [JsonPropertyName("userlist")]
    public WeComUserDetailResponse[]? UserList { get; set; }
}

/// <summary>
/// /cgi-bin/oa/getapprovalinfo 响应：只返回 sp_no 数组 + 分页 next_cursor。
/// 对应官方「批量获取审批单号」端点，配合 getapprovaldetail 做轻量状态轮询。
/// </summary>
internal sealed class WeComApprovalInfoResponse : WeComApiResponse
{
    [JsonPropertyName("sp_no_list")]
    public string[]? SpNoList { get; set; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }
}
