using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// X2：知识库 / AI 数据库 工作流节点统一观测工具。
/// - <see cref="ActivitySource"/>: OpenTelemetry trace 入口（节点开始 / 结束 span）
/// - <see cref="WriteAuditAsync"/>: 写入等保审计记录
/// - <see cref="Mask"/>: 基于列名/正则的敏感字段脱敏
/// </summary>
public static class AiNodeObservability
{
    public const string SourceName = "Atlas.AiPlatform.WorkflowNodes";
    public static readonly ActivitySource ActivitySource = new(SourceName);

    private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "pwd",
        "token", "secret", "apiKey", "api_key", "accessToken", "access_token", "refreshToken", "refresh_token",
        "ssn", "idCard", "id_card", "idnumber", "id_number",
        "phone", "mobile", "telephone",
        "email", "mail",
        "creditCard", "credit_card", "cardNumber", "card_number", "cvv",
        "address", "addr"
    };

    private static readonly Regex EmailPattern = new(@"^[\w\.\-]+@[\w\.\-]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^\+?\d{6,15}$", RegexOptions.Compiled);

    /// <summary>启动一个节点级 span，自动包含 tenantId、databaseId/knowledgeId 与 nodeKey。</summary>
    public static Activity? StartNodeActivity(
        string operationName,
        TenantId tenantId,
        long? userId,
        string? channelId,
        string nodeKey,
        IDictionary<string, object?>? extraTags = null)
    {
        var activity = ActivitySource.StartActivity(operationName, ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("tenant.id", tenantId.Value.ToString());
        activity.SetTag("workflow.node_key", nodeKey);
        if (userId.HasValue)
        {
            activity.SetTag("user.id", userId.Value);
        }
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            activity.SetTag("user.channel", channelId);
        }
        if (extraTags is not null)
        {
            foreach (var pair in extraTags)
            {
                activity.SetTag(pair.Key, pair.Value);
            }
        }
        return activity;
    }

    /// <summary>写入审计记录；缺少 IAuditWriter / 无 userId 时静默跳过。</summary>
    public static async Task WriteAuditAsync(
        IServiceProvider serviceProvider,
        TenantId tenantId,
        long? userId,
        string action,
        string result,
        string? target,
        CancellationToken cancellationToken)
    {
        var auditWriter = serviceProvider.GetService<IAuditWriter>();
        if (auditWriter is null)
        {
            return;
        }
        try
        {
            var actor = userId?.ToString() ?? "system:workflow";
            await auditWriter.WriteAsync(
                new AuditRecord(tenantId, actor, action, result, target, null, null),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // 审计失败仅日志，不中断节点执行。
            var logger = serviceProvider.GetService<ILogger<object>>();
            logger?.LogWarning(ex, "AiNodeObservability 审计写入失败 action={Action}", action);
        }
    }

    /// <summary>遍历 JSON 对象/数组，按字段名/值模式自动脱敏；返回深拷贝结果。</summary>
    public static JsonElement Mask(JsonElement source)
    {
        return source.ValueKind switch
        {
            JsonValueKind.Object => MaskObject(source),
            JsonValueKind.Array => MaskArray(source),
            JsonValueKind.String => MaskScalarString(propertyName: null, source.GetString() ?? string.Empty),
            _ => source.Clone()
        };
    }

    /// <summary>对一组记录列表脱敏（典型用法：KB 检索结果 / DB 查询结果）。</summary>
    public static List<JsonElement> MaskAll(IEnumerable<JsonElement> source)
    {
        var list = new List<JsonElement>();
        foreach (var item in source)
        {
            list.Add(Mask(item));
        }
        return list;
    }

    private static JsonElement MaskObject(JsonElement obj)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.EnumerateObject())
        {
            dict[prop.Name] = MaskField(prop.Name, prop.Value);
        }
        return JsonSerializer.SerializeToElement(dict);
    }

    private static JsonElement MaskArray(JsonElement arr)
    {
        var list = new List<JsonElement>();
        foreach (var item in arr.EnumerateArray())
        {
            list.Add(Mask(item));
        }
        return JsonSerializer.SerializeToElement(list);
    }

    private static JsonElement MaskField(string propertyName, JsonElement value)
    {
        if (IsSensitiveFieldName(propertyName))
        {
            return MaskValueByName(value);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => MaskObject(value),
            JsonValueKind.Array => MaskArray(value),
            JsonValueKind.String => MaskScalarString(propertyName, value.GetString() ?? string.Empty),
            _ => value.Clone()
        };
    }

    private static JsonElement MaskScalarString(string? propertyName, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return JsonSerializer.SerializeToElement(text);
        }
        if (EmailPattern.IsMatch(text))
        {
            return JsonSerializer.SerializeToElement(MaskEmail(text));
        }
        if (PhonePattern.IsMatch(text))
        {
            return JsonSerializer.SerializeToElement(MaskPhone(text));
        }
        return JsonSerializer.SerializeToElement(text);
    }

    private static bool IsSensitiveFieldName(string name)
    {
        if (SensitiveFieldNames.Contains(name)) return true;
        // 模糊匹配 contains
        foreach (var sensitive in SensitiveFieldNames)
        {
            if (name.Contains(sensitive, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static JsonElement MaskValueByName(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => JsonSerializer.SerializeToElement(MaskString(value.GetString() ?? string.Empty)),
            JsonValueKind.Number => JsonSerializer.SerializeToElement("***"),
            JsonValueKind.True or JsonValueKind.False => JsonSerializer.SerializeToElement("***"),
            JsonValueKind.Null => value.Clone(),
            _ => JsonSerializer.SerializeToElement("***")
        };
    }

    public static string MaskString(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (text.Length <= 2) return "**";
        if (text.Length <= 6) return text[0] + new string('*', text.Length - 2) + text[^1];
        return string.Concat(text.AsSpan(0, 2), new string('*', text.Length - 4), text.AsSpan(text.Length - 2, 2));
    }

    public static string MaskEmail(string email)
    {
        var atIdx = email.IndexOf('@');
        if (atIdx < 1) return MaskString(email);
        var name = email[..atIdx];
        var domain = email[atIdx..];
        return MaskString(name) + domain;
    }

    public static string MaskPhone(string phone)
    {
        if (phone.Length < 7) return new string('*', phone.Length);
        // 保留前 3 位与后 2 位
        return string.Concat(phone.AsSpan(0, 3), new string('*', phone.Length - 5), phone.AsSpan(phone.Length - 2, 2));
    }
}
