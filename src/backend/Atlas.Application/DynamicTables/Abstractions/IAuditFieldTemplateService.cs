using Atlas.Application.DynamicTables.Models;

namespace Atlas.Application.DynamicTables.Abstractions;

/// <summary>
/// 审计字段模板注入服务：为新建动态表自动生成
/// created_at / created_by / updated_at / updated_by / row_version 等标准字段。
/// </summary>
public interface IAuditFieldTemplateService
{
    IReadOnlyList<DynamicFieldDefinition> GetAuditFields();

    IReadOnlyList<DynamicFieldDefinition> ApplyTemplate(
        IReadOnlyList<DynamicFieldDefinition> userFields,
        string? extensionPolicy);
}
