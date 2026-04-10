using System.Globalization;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

internal static class ConversationNodeHelper
{
    public static long ResolveLong(NodeExecutionContext context, string configKey, string variablePath)
    {
        var configValue = context.GetConfigInt64(configKey, 0L);
        if (configValue > 0)
        {
            return configValue;
        }

        if (context.TryResolveVariable(variablePath, out var variableValue) &&
            long.TryParse(VariableResolver.ToDisplayText(variableValue), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0L;
    }

    public static int ResolveInt(NodeExecutionContext context, string configKey, int defaultValue)
    {
        return Math.Max(1, context.GetConfigInt32(configKey, defaultValue));
    }

    public static JsonElement Serialize<T>(T value) => JsonSerializer.SerializeToElement(value);
}
