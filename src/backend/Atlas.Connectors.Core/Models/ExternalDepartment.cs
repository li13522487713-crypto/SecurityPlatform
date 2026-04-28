namespace Atlas.Connectors.Core.Models;

/// <summary>
/// 跨 provider 的部门统一表示。
/// </summary>
public sealed record ExternalDepartment
{
    public required string ProviderType { get; init; }

    public required string ProviderTenantId { get; init; }

    public required string ExternalDepartmentId { get; init; }

    public string? ParentExternalDepartmentId { get; init; }

    public required string Name { get; init; }

    public string? FullPath { get; init; }

    public int Order { get; init; }

    public IReadOnlyList<string>? LeaderExternalUserIds { get; init; }

    public string? RawJson { get; init; }
}
