using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 字典类型（等保2.0：固定数据集合，所有修改需可追溯）
/// </summary>
public sealed class DictType : TenantEntity
{
    public DictType()
        : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public DictType(TenantId tenantId, string code, string name, long id)
        : base(tenantId)
    {
        Id = id;
        Code = code;
        Name = name;
        Status = true;
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public bool Status { get; private set; }
    public string? Remark { get; private set; }

    public void Update(string name, bool status, string? remark)
    {
        Name = name;
        Status = status;
        Remark = remark;
    }
}
