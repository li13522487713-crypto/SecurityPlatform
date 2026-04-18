namespace Atlas.Connectors.Core.Models;

/// <summary>
/// 通讯录变更事件的统一封装。Provider 收到原始 webhook 后做 verify+decode，再转成本结构推到 DirectorySync 服务。
/// </summary>
public sealed record ExternalDirectoryEvent
{
    public required string ProviderType { get; init; }

    public required string ProviderTenantId { get; init; }

    public required ExternalDirectoryEventKind Kind { get; init; }

    /// <summary>变更的实体 ID。Provider 必须保证此值非空，否则视为非法事件。</summary>
    public required string EntityId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public IReadOnlyDictionary<string, string>? Extra { get; init; }

    public string? RawJson { get; init; }
}

public enum ExternalDirectoryEventKind
{
    UserCreated = 1,
    UserUpdated = 2,
    UserDeleted = 3,
    DepartmentCreated = 4,
    DepartmentUpdated = 5,
    DepartmentDeleted = 6,
}
