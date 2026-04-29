namespace Atlas.Application.Microflows.Audit;

/// <summary>
/// Microflow 模块的 audit 抽象。Application 层不直接依赖 Audit 的实体类型，避免 bounded
/// context 跨界。AppHost / Infrastructure 层提供具体实现把 <see cref="MicroflowAuditEvent"/>
/// 映射到 <c>IAuditWriter.WriteAsync(AuditRecord, ...)</c>。
///
/// 实现必须做到 best-effort：写审计失败不应影响主业务流程；具体实现应吞异常并 structured-log。
/// </summary>
public interface IMicroflowAuditWriter
{
    Task WriteAsync(MicroflowAuditEvent auditEvent, CancellationToken cancellationToken);
}

public sealed record MicroflowAuditEvent
{
    /// <summary>动作名（snake-case），例如 "microflow.create" / "microflow.publish" / "microflow.test_run"。</summary>
    public required string Action { get; init; }

    /// <summary>"success" | "failure"。失败必须配合 ErrorCode / Details 提供原因。</summary>
    public required string Result { get; init; }

    public required string ResourceId { get; init; }

    public string? ResourceName { get; init; }

    public string? WorkspaceId { get; init; }

    public string? Target { get; init; }

    /// <summary>额外结构化字段（diff 摘要 / 影响计数 / errorCode 等），实现侧 JSON 序列化为 details。</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; init; }

    public string? ErrorCode { get; init; }
}

/// <summary>NoOp 实现，便于无 audit infrastructure 的场景（单元测试、轻量启动）。</summary>
public sealed class NullMicroflowAuditWriter : IMicroflowAuditWriter
{
    public Task WriteAsync(MicroflowAuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
