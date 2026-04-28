namespace Atlas.Connectors.Core.Models;

/// <summary>
/// 跨 provider 的统一外部用户档案。各 provider 把私有字段映射到这些字段；未提供的字段保持 null。
/// </summary>
public sealed record ExternalUserProfile
{
    public required string ProviderType { get; init; }

    /// <summary>租户内主键（企微 corp_id / 飞书 tenant_key / 钉钉 corp_id）。</summary>
    public required string ProviderTenantId { get; init; }

    /// <summary>provider 主用户 ID（企微 userid / 飞书 user_id / 钉钉 userid）。</summary>
    public required string ExternalUserId { get; init; }

    /// <summary>应用内 open id（企微 openid / open_userid / 飞书 open_id / 钉钉 unionid 不同取值）。</summary>
    public string? OpenId { get; init; }

    /// <summary>跨应用统一 ID（飞书 union_id / 企微 open_userid 跨应用）。</summary>
    public string? UnionId { get; init; }

    public string? Name { get; init; }

    public string? EnglishName { get; init; }

    public string? Email { get; init; }

    public string? Mobile { get; init; }

    public string? Avatar { get; init; }

    public string? Position { get; init; }

    public IReadOnlyList<string>? DepartmentIds { get; init; }

    /// <summary>主部门 ID（企微 main_department / 飞书 department_path 顶端）。</summary>
    public string? PrimaryDepartmentId { get; init; }

    /// <summary>provider 内的状态码原文（如企微 status / 飞书 status.is_activated 等），便于上层做策略判断。</summary>
    public string? Status { get; init; }

    /// <summary>provider 返回的原始结构（JSON 字符串），用于审计与未来字段扩展。</summary>
    public string? RawJson { get; init; }
}
