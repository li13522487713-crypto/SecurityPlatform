using Atlas.Connectors.Core.Models;

namespace Atlas.Connectors.Core.Abstractions;

/// <summary>
/// 外部通讯录 Provider：仅暴露读取能力，写入由各企业目录系统/管理员维护。
/// 所有方法都应在 provider 内部做"应用可见范围"错误降级，碰到 60011/权限不足时返回空集合而非抛出。
/// </summary>
public interface IExternalDirectoryProvider
{
    string ProviderType { get; }

    Task<IReadOnlyList<ExternalDepartment>> ListChildDepartmentsAsync(ConnectorContext context, string parentExternalDepartmentId, bool recursive, CancellationToken cancellationToken);

    Task<ExternalDepartment?> GetDepartmentAsync(ConnectorContext context, string externalDepartmentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListDepartmentMemberIdsAsync(ConnectorContext context, string externalDepartmentId, bool recursive, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalUserProfile>> ListDepartmentMembersAsync(ConnectorContext context, string externalDepartmentId, bool recursive, CancellationToken cancellationToken);

    Task<ExternalUserProfile?> GetUserAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken);

    /// <summary>
    /// 通过手机号或邮箱反查外部 user id（飞书 contact/v3/users/batch_get_id）。
    /// 不支持的 provider 返回空字典；找不到的键不写入字典。
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ResolveExternalUserIdsAsync(ConnectorContext context, ExternalDirectoryLookupKind kind, IReadOnlyList<string> values, CancellationToken cancellationToken);
}

public enum ExternalDirectoryLookupKind
{
    Mobile = 1,
    Email = 2,
}
