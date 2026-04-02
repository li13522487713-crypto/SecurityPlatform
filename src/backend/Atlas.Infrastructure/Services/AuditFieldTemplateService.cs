using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;

namespace Atlas.Infrastructure.Services;

public sealed class AuditFieldTemplateService : IAuditFieldTemplateService
{
    private static readonly IReadOnlyList<DynamicFieldDefinition> AuditFields = new[]
    {
        new DynamicFieldDefinition("created_at", "创建时间", "DateTime", null, null, null, false, false, false, false, null, 9001),
        new DynamicFieldDefinition("created_by", "创建人", "Long", null, null, null, false, false, false, false, null, 9002),
        new DynamicFieldDefinition("updated_at", "更新时间", "DateTime", null, null, null, true, false, false, false, null, 9003),
        new DynamicFieldDefinition("updated_by", "更新人", "Long", null, null, null, true, false, false, false, null, 9004),
        new DynamicFieldDefinition("row_version", "行版本", "Int", null, null, null, false, false, false, false, "1", 9005)
    };

    public IReadOnlyList<DynamicFieldDefinition> GetAuditFields()
    {
        return AuditFields;
    }

    public IReadOnlyList<DynamicFieldDefinition> ApplyTemplate(
        IReadOnlyList<DynamicFieldDefinition> userFields,
        string? extensionPolicy)
    {
        if (!ShouldInject(extensionPolicy))
        {
            return userFields;
        }

        var existingNames = new HashSet<string>(
            userFields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        var result = new List<DynamicFieldDefinition>(userFields);
        foreach (var auditField in AuditFields)
        {
            if (!existingNames.Contains(auditField.Name))
            {
                result.Add(auditField);
            }
        }

        return result;
    }

    private static bool ShouldInject(string? extensionPolicy)
    {
        if (string.IsNullOrWhiteSpace(extensionPolicy))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(extensionPolicy);
            if (doc.RootElement.TryGetProperty("injectAuditFields", out var val))
            {
                return val.GetBoolean();
            }
        }
        catch
        {
            // 策略 JSON 不合法时默认注入
        }

        return true;
    }
}
