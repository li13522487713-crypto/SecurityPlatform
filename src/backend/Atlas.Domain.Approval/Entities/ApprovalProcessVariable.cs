using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批流流程变量（用于存储流程运行时的变量数据）
/// TODO: 流程变量功能预留，待实现条件规则评估器时使用
/// </summary>
public sealed class ApprovalProcessVariable : TenantEntity
{
    public ApprovalProcessVariable()
        : base(TenantId.Empty)
    {
        VariableName = string.Empty;
        VariableValue = null;
    }

    public ApprovalProcessVariable(
        TenantId tenantId,
        long instanceId,
        string variableName,
        string? variableValue,
        long id)
        : base(tenantId)
    {
        Id = id;
        InstanceId = instanceId;
        VariableName = variableName;
        VariableValue = variableValue;
    }

    /// <summary>流程实例 ID</summary>
    public long InstanceId { get; private set; }

    /// <summary>变量名</summary>
    public string VariableName { get; private set; }

    /// <summary>变量值（JSON 字符串）</summary>
    public string? VariableValue { get; private set; }

    public void UpdateValue(string? value)
    {
        VariableValue = value;
    }
}
