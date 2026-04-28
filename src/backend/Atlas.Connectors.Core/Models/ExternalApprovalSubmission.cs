namespace Atlas.Connectors.Core.Models;

/// <summary>
/// 跨 provider 的审批提单请求。
/// </summary>
public sealed record ExternalApprovalSubmission
{
    public required string ExternalTemplateId { get; init; }

    /// <summary>提单人在外部系统中的 user id（必填，通常由 ExternalIdentityBinding 解析得到）。</summary>
    public required string ApplicantExternalUserId { get; init; }

    /// <summary>提单业务主键，用于回调幂等关联本地实例。</summary>
    public required string BusinessKey { get; init; }

    /// <summary>表单字段值：key 为外部模板控件 ID，value 为字段统一值（已经过 mapping 转换）。</summary>
    public required IReadOnlyDictionary<string, ExternalApprovalFieldValue> Fields { get; init; }

    /// <summary>审批人列表（部分 provider 支持指定）。</summary>
    public IReadOnlyList<string>? ApproverExternalUserIds { get; init; }

    /// <summary>抄送人列表。</summary>
    public IReadOnlyList<string>? CcExternalUserIds { get; init; }

    /// <summary>外部部门冗余（企微部分模板要求）。</summary>
    public string? DepartmentExternalId { get; init; }

    public string? SummaryText { get; init; }
}

/// <summary>
/// 字段值统一封装：使用 ValueType + Raw 双轨，避免 object 滥用。
/// </summary>
public sealed record ExternalApprovalFieldValue
{
    public required string ValueType { get; init; }

    /// <summary>JSON 字符串原文。基础类型（string / number / bool）也以 JSON 字面量形式存放，便于 provider 实现一次反序列化。</summary>
    public required string RawJson { get; init; }
}
