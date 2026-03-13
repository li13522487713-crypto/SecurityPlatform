using System.Text.Json;
using Atlas.WorkflowCore.Abstractions;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

internal static class WorkflowDataHelper
{
    public static Dictionary<string, object?> GetDataDictionary(IStepExecutionContext context)
    {
        if (context.Workflow.Data is Dictionary<string, object?> dict)
        {
            return dict;
        }

        if (context.Workflow.Data is Dictionary<string, object> dict2)
        {
            return dict2.ToDictionary(x => x.Key, x => (object?)x.Value);
        }

        if (context.Workflow.Data is string json && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                if (parsed is not null)
                {
                    context.Workflow.Data = parsed;
                    return parsed;
                }
            }
            catch
            {
                // ignore and fallback to empty dictionary
            }
        }

        var created = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        context.Workflow.Data = created;
        return created;
    }

    public static string RenderTemplate(string? template, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var output = template;
        foreach (var kv in data)
        {
            output = output.Replace($"{{{{{kv.Key}}}}}", kv.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }
}
