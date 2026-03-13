using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiWorkflowDslBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public string BuildDefinitionJson(string workflowId, int version, string canvasJson)
    {
        if (string.IsNullOrWhiteSpace(canvasJson))
        {
            return BuildEmptyDefinition(workflowId, version);
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(canvasJson);
        }
        catch
        {
            return BuildEmptyDefinition(workflowId, version);
        }

        // If payload already looks like DSL envelope, keep it.
        if (root?["steps"] is JsonArray existingSteps && existingSteps.Count > 0)
        {
            var cloned = root.Deserialize<Dictionary<string, object?>>() ?? new Dictionary<string, object?>();
            cloned["id"] = workflowId;
            cloned["version"] = version;
            return JsonSerializer.Serialize(cloned, JsonOptions);
        }

        var nodes = root?["nodes"]?.AsArray() ?? [];
        var edges = root?["edges"]?.AsArray() ?? [];
        var idToType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var id = node?["id"]?.GetValue<string>();
            var type = node?["type"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                idToType[id] = NormalizeNodeType(type);
            }
        }

        var outgoing = edges
            .Select(x => new
            {
                Source = x?["source"]?.GetValue<string>(),
                Target = x?["target"]?.GetValue<string>()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Source) && !string.IsNullOrWhiteSpace(x.Target))
            .GroupBy(x => x.Source!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Target!).ToList(), StringComparer.OrdinalIgnoreCase);

        var orderedIds = TopologicalOrder(nodes, outgoing);
        var steps = new JsonArray();
        foreach (var nodeId in orderedIds)
        {
            var step = new JsonObject
            {
                ["id"] = nodeId,
                ["name"] = nodeId,
                ["stepType"] = ResolveStepTypeName(idToType.GetValueOrDefault(nodeId, "http")),
            };
            if (outgoing.TryGetValue(nodeId, out var next) && next.Count > 0)
            {
                step["nextStepId"] = next[0];
            }

            steps.Add(step);
        }

        var definition = new JsonObject
        {
            ["id"] = workflowId,
            ["version"] = version,
            ["dataType"] = "System.Collections.Generic.Dictionary`2[[System.String],[System.Object]]",
            ["steps"] = steps
        };
        return definition.ToJsonString(JsonOptions);
    }

    private static string BuildEmptyDefinition(string workflowId, int version)
    {
        var definition = new JsonObject
        {
            ["id"] = workflowId,
            ["version"] = version,
            ["dataType"] = "System.Collections.Generic.Dictionary`2[[System.String],[System.Object]]",
            ["steps"] = new JsonArray()
        };
        return definition.ToJsonString();
    }

    private static string NormalizeNodeType(string? nodeType)
    {
        return (nodeType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "llm" => "llm",
            "plugin" => "plugin",
            "coderunner" => "code",
            "knowledgeretriever" => "rag",
            "textprocessor" => "text",
            "httprequester" => "http",
            "outputemitter" => "output",
            _ => "http"
        };
    }

    private static string ResolveStepTypeName(string normalizedType)
    {
        var type = normalizedType switch
        {
            "llm" => typeof(LlmStep),
            "plugin" => typeof(PluginStep),
            "code" => typeof(CodeRunnerStep),
            "rag" => typeof(KnowledgeRetrieverStep),
            "text" => typeof(TextProcessorStep),
            "output" => typeof(OutputEmitterStep),
            _ => typeof(HttpRequesterStep)
        };

        return type.AssemblyQualifiedName ?? type.FullName ?? typeof(HttpRequesterStep).FullName!;
    }

    private static List<string> TopologicalOrder(JsonArray nodes, Dictionary<string, List<string>> outgoing)
    {
        var nodeIds = nodes
            .Select(x => x?["id"]?.GetValue<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
        if (nodeIds.Count == 0)
        {
            return [];
        }

        var indegree = nodeIds.ToDictionary(x => x, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var targets in outgoing.Values)
        {
            foreach (var target in targets)
            {
                if (indegree.ContainsKey(target))
                {
                    indegree[target]++;
                }
            }
        }

        var queue = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var ordered = new List<string>(nodeIds.Count);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            ordered.Add(id);
            if (!outgoing.TryGetValue(id, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                if (!indegree.ContainsKey(target))
                {
                    continue;
                }

                indegree[target]--;
                if (indegree[target] == 0)
                {
                    queue.Enqueue(target);
                }
            }
        }

        // Cycle fallback: append missing nodes in original order.
        foreach (var id in nodeIds)
        {
            if (!ordered.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(id);
            }
        }

        return ordered;
    }
}
