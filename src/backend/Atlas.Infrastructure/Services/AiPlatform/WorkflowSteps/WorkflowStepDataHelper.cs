using System.Collections;
using Atlas.WorkflowCore.Abstractions;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

internal static class WorkflowStepDataHelper
{
    public static Dictionary<string, object?> EnsureDataDictionary(IStepExecutionContext context)
    {
        if (context.Workflow.Data is Dictionary<string, object?> typed)
        {
            return typed;
        }

        if (context.Workflow.Data is IDictionary raw)
        {
            var copied = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in raw)
            {
                if (entry.Key is string key)
                {
                    copied[key] = entry.Value;
                }
            }

            context.Workflow.Data = copied;
            return copied;
        }

        var created = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        context.Workflow.Data = created;
        return created;
    }

    public static string ResolveTemplate(string? template, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var resolved = template;
        foreach (var (key, value) in data)
        {
            resolved = resolved.Replace($"{{{key}}}", Convert.ToString(value) ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return resolved;
    }
}
