namespace Atlas.Application.Approval.Models;

/// <summary>
/// 发起审批流程实例请求
/// </summary>
public record ApprovalStartRequest
{
    /// <summary>流程定义 ID</summary>
    public required long DefinitionId { get; init; }

    /// <summary>业务 key（用于关联业务数据）</summary>
    public required string BusinessKey { get; init; }

    /// <summary>业务数据 JSON</summary>
    public string? DataJson { get; init; }

    /// <summary>穿越时空：覆盖创建时间（仅用于测试或特殊场景）</summary>
    public DateTimeOffset? OverrideCreateTime { get; init; }
}
