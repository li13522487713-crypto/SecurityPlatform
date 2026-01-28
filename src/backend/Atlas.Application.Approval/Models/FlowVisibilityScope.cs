namespace Atlas.Application.Approval.Models;

/// <summary>
/// 流程可见范围配置
/// </summary>
public sealed class FlowVisibilityScope
{
    /// <summary>可见范围类型</summary>
    public FlowVisibilityScopeType ScopeType { get; set; } = FlowVisibilityScopeType.All;

    /// <summary>部门ID列表（当 ScopeType 为 Department 时使用）</summary>
    public IReadOnlyList<long>? DepartmentIds { get; set; }

    /// <summary>角色代码列表（当 ScopeType 为 Role 时使用）</summary>
    public IReadOnlyList<string>? RoleCodes { get; set; }

    /// <summary>用户ID列表（当 ScopeType 为 User 时使用）</summary>
    public IReadOnlyList<long>? UserIds { get; set; }
}

/// <summary>
/// 流程可见范围类型
/// </summary>
public enum FlowVisibilityScopeType
{
    /// <summary>全部可见（默认）</summary>
    All = 0,

    /// <summary>指定部门</summary>
    Department = 1,

    /// <summary>指定角色</summary>
    Role = 2,

    /// <summary>指定用户</summary>
    User = 3
}
