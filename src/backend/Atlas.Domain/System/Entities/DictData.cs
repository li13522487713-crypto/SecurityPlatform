using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 字典数据（字典类型的具体键值对）
/// </summary>
public sealed class DictData : TenantEntity
{
    public DictData()
        : base(TenantId.Empty)
    {
        DictTypeCode = string.Empty;
        Label = string.Empty;
        Value = string.Empty;
    }

    public DictData(TenantId tenantId, string dictTypeCode, string label, string value, long id)
        : base(tenantId)
    {
        Id = id;
        DictTypeCode = dictTypeCode;
        Label = label;
        Value = value;
        Status = true;
        SortOrder = 0;
    }

    public string DictTypeCode { get; private set; }
    public string Label { get; private set; }
    public string Value { get; private set; }
    public int SortOrder { get; private set; }
    public bool Status { get; private set; }
    public string? CssClass { get; private set; }
    public string? ListClass { get; private set; }

    public void Update(string label, string value, int sortOrder, bool status, string? cssClass, string? listClass)
    {
        Label = label;
        Value = value;
        SortOrder = sortOrder;
        Status = status;
        CssClass = cssClass;
        ListClass = listClass;
    }
}
