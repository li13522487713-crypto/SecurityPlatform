using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OrchestrationCompiler : IOrchestrationCompiler
{
    private readonly ISqlSugarClient _db;

    public OrchestrationCompiler(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<CompiledOrchestrationPlan?> CompileByKeyAsync(
        TenantId tenantId,
        long appInstanceId,
        string planKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planKey))
        {
            return null;
        }

        var plan = await _db.Queryable<OrchestrationPlan>()
            .FirstAsync(
                item =>
                    item.TenantIdValue == tenantId.Value
                    && item.AppInstanceId == appInstanceId
                    && item.PlanKey == planKey.Trim()
                    && item.Status != OrchestrationPlanStatus.Archived,
                cancellationToken);
        return Compile(plan);
    }

    public async Task<CompiledOrchestrationPlan?> CompileByIdAsync(
        TenantId tenantId,
        long planId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _db.Queryable<OrchestrationPlan>()
            .FirstAsync(
                item =>
                    item.TenantIdValue == tenantId.Value
                    && item.Id == planId
                    && item.Status != OrchestrationPlanStatus.Archived,
                cancellationToken);
        return Compile(plan);
    }

    private static CompiledOrchestrationPlan? Compile(OrchestrationPlan? plan)
    {
        if (plan is null)
        {
            return null;
        }

        var nodes = ParseNodes(plan.NodeGraphJson);
        var hashSource = string.Join(
            "\n",
            plan.Id.ToString(),
            plan.PlanKey,
            plan.TriggerType,
            plan.PublishedVersion.ToString(),
            plan.NodeGraphJson,
            plan.RuntimePolicyJson);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashSource)));

        return new CompiledOrchestrationPlan(
            plan.Id,
            plan.PlanKey,
            plan.PlanName,
            plan.TriggerType,
            plan.PublishedVersion,
            nodes,
            plan.RuntimePolicyJson,
            plan.NodeGraphJson,
            hash,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<CompiledOrchestrationNode> ParseNodes(string? nodeGraphJson)
    {
        if (string.IsNullOrWhiteSpace(nodeGraphJson))
        {
            return Array.Empty<CompiledOrchestrationNode>();
        }

        try
        {
            using var document = JsonDocument.Parse(nodeGraphJson);
            var root = document.RootElement;
            var nodeArray = root.ValueKind switch
            {
                JsonValueKind.Array => root,
                JsonValueKind.Object when root.TryGetProperty("nodes", out var nodes) => nodes,
                _ => default
            };

            if (nodeArray.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CompiledOrchestrationNode>();
            }

            var result = new List<CompiledOrchestrationNode>(nodeArray.GetArrayLength());
            foreach (var node in nodeArray.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var nodeId = TryGetString(node, "id", "nodeId") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                var nodeType = TryGetString(node, "type", "nodeType") ?? "task";
                var dependsOn = ParseDependsOn(node);
                result.Add(new CompiledOrchestrationNode(nodeId, nodeType, dependsOn));
            }

            return result;
        }
        catch
        {
            return Array.Empty<CompiledOrchestrationNode>();
        }
    }

    private static IReadOnlyList<string> ParseDependsOn(JsonElement node)
    {
        if (!node.TryGetProperty("dependsOn", out var dependsOn) || dependsOn.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return dependsOn.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? TryGetString(JsonElement node, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }
}
