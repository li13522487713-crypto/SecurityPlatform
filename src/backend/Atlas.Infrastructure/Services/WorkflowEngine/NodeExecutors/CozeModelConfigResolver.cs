using System.Globalization;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

internal static class CozeModelConfigResolver
{
    public static async Task<ModelConfigDto?> ResolveAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var queryService = context.ServiceProvider.GetService<IModelConfigQueryService>();
        if (queryService is null)
        {
            return null;
        }

        var modelType = context.GetConfigInt64("model.modelType", context.GetConfigInt64("modelType", 0L));
        if (modelType > 0)
        {
            var byId = await queryService.GetByIdAsync(context.TenantId, modelType, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        var modelName = context.GetConfigString("model.modelName", context.GetConfigString("modelName"));
        var all = await queryService.GetAllEnabledAsync(context.TenantId, workspaceId: null, cancellationToken);
        if (modelType > 0)
        {
            var byCozeModelType = all.FirstOrDefault(item => ToCozeModelType(item) == modelType);
            if (byCozeModelType is not null)
            {
                return byCozeModelType;
            }
        }

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            var byName = all.FirstOrDefault(item =>
                string.Equals(item.ModelId, modelName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.DefaultModel, modelName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }

        return null;
    }

    private static int ToCozeModelType(ModelConfigDto item)
    {
        var key = string.Join(
            "|",
            (item.ProviderType ?? string.Empty).Trim().ToLowerInvariant(),
            (item.ModelId ?? string.Empty).Trim().ToLowerInvariant(),
            (item.DefaultModel ?? string.Empty).Trim().ToLowerInvariant(),
            item.Id.ToString(CultureInfo.InvariantCulture));
        unchecked
        {
            const uint fnvOffset = 2166136261;
            const uint fnvPrime = 16777619;
            var hash = fnvOffset;
            foreach (var ch in key)
            {
                hash ^= ch;
                hash *= fnvPrime;
            }

            return (int)(hash % 2_000_000_000) + 1000;
        }
    }
}
