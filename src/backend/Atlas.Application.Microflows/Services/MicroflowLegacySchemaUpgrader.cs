using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;

namespace Atlas.Application.Microflows.Services;

/// <summary>
/// 将历史旧版 schema（objectCollection/flows 格式，schemaVersion != flowgram.microflow.v1）
/// 升级为最新设计态协议（flowgram.microflow.v1，workflow.nodes/edges）。
/// 升级结果经 <see cref="MicroflowDesignSchemaHelper.NormalizeAndValidateDesignSchema"/> 校验后返回 JSON 字符串。
/// </summary>
internal static class MicroflowLegacySchemaUpgrader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 尝试将 <paramref name="schemaJson"/> 升级为最新协议。
    /// 若已是 flowgram.microflow.v1 则直接通过校验并返回；
    /// 若是可识别的旧版 objectCollection/flows 格式则执行升级；
    /// 否则抛出 422 <see cref="MicroflowApiException"/>。
    /// </summary>
    public static string Upgrade(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;

            // 已是新协议，直接校验后返回。
            if (MicroflowDesignSchemaHelper.IsDesignSchema(root))
            {
                return MicroflowDesignSchemaHelper.NormalizeAndValidateDesignSchema(root);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("legacy schema 必须是 JSON 对象。", "root");
            }

            // 不是新协议但也不含 objectCollection / workflowJson — 无法识别。
            bool hasObjectCollection = root.TryGetProperty("objectCollection", out _);
            bool hasWorkflowJson = root.TryGetProperty("workflowJson", out _);
            bool hasFlowgram = root.TryGetProperty("flowgram", out _);

            if (!hasObjectCollection && !hasWorkflowJson && !hasFlowgram)
            {
                throw Invalid(
                    "无法识别的 schema 格式：既不是 flowgram.microflow.v1，也不含 objectCollection / workflowJson 字段。",
                    "schemaVersion");
            }

            return UpgradeLegacy(root, schemaJson);
        }
        catch (JsonException ex)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowSchemaInvalid,
                $"旧版 schema JSON 无法解析：{ex.Message}",
                422,
                innerException: ex);
        }
    }

    // -------------------------------------------------------------------------
    // 核心升级逻辑
    // -------------------------------------------------------------------------

    private static string UpgradeLegacy(JsonElement root, string originalJson)
    {
        var result = new JsonObject();

        // 强制写入新版 schemaVersion。
        result["schemaVersion"] = MicroflowDesignSchemaHelper.DesignSchemaVersion;

        // 透传或修复顶层标量字段。
        CopyStringField(root, result, "id");
        CopyStringField(root, result, "stableId");
        CopyStringField(root, result, "name");

        // displayName 如果缺失则用 name 补齐（NormalizeAndValidate 要求非空）。
        if (root.TryGetProperty("displayName", out var displayName) && displayName.ValueKind == JsonValueKind.String)
        {
            result["displayName"] = displayName.GetString();
        }
        else if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
        {
            result["displayName"] = name.GetString();
        }

        CopyStringField(root, result, "moduleId");
        CopyStringField(root, result, "moduleName");
        CopyStringField(root, result, "description");
        CopyStringField(root, result, "documentation");

        // parameters / returnType — 直接透传。
        CopyRawField(root, result, "parameters");
        CopyRawField(root, result, "returnType");
        CopyStringField(root, result, "returnVariableName");

        // security / concurrency / exposure — 可选透传。
        CopyRawField(root, result, "security");
        CopyRawField(root, result, "concurrency");
        CopyRawField(root, result, "exposure");

        // 将 objectCollection/flows 转换为 workflow.nodes/edges。
        var (nodes, edges) = BuildWorkflowFromObjectCollection(root);
        var workflow = new JsonObject
        {
            ["nodes"] = nodes,
            ["edges"] = edges,
        };
        result["workflow"] = workflow;

        // variables / validation / editor / audit — 尽量保留，缺失则补默认。
        CopyRawFieldOrDefault(root, result, "variables", "[]");
        CopyRawFieldOrDefault(root, result, "validation", """{"issues":[]}""");
        CopyRawFieldOrDefault(root, result, "editor", BuildDefaultEditor());
        CopyRawFieldOrDefault(root, result, "audit", BuildDefaultAudit());

        var newJson = result.ToJsonString(JsonOptions);
        try
        {
            using var newDoc = JsonDocument.Parse(newJson);
            return MicroflowDesignSchemaHelper.NormalizeAndValidateDesignSchema(newDoc.RootElement);
        }
        catch (MicroflowApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Invalid($"旧版 schema 升级后校验失败：{ex.Message}", "workflow");
        }
    }

    // -------------------------------------------------------------------------
    // objectCollection / flows → workflow.nodes / edges
    // -------------------------------------------------------------------------

    private sealed record ObjectContext(string CollectionId, bool InsideLoop, string? ParentLoopObjectId);

    private static (JsonArray Nodes, JsonArray Edges) BuildWorkflowFromObjectCollection(JsonElement root)
    {
        var nodes = new JsonArray();
        var edges = new JsonArray();

        if (!root.TryGetProperty("objectCollection", out var collection)
            || collection.ValueKind != JsonValueKind.Object)
        {
            // 没有 objectCollection — 返回空 workflow。
            return (nodes, edges);
        }

        var rootCollectionId = ReadString(collection, "id") ?? "root-collection";
        var context = new ObjectContext(rootCollectionId, InsideLoop: false, ParentLoopObjectId: null);

        CollectNodesFromCollection(collection, context, nodes);

        // 顶层 flows。
        if (root.TryGetProperty("flows", out var topFlows) && topFlows.ValueKind == JsonValueKind.Array)
        {
            CollectEdgesFromFlowArray(topFlows, rootCollectionId, edges);
        }

        // objectCollection 内嵌 flows（旧版有时会写在这里）。
        CollectNestedEdgesFromCollection(collection, rootCollectionId, edges);

        return (nodes, edges);
    }

    private static void CollectNodesFromCollection(
        JsonElement collection,
        ObjectContext context,
        JsonArray nodes)
    {
        if (!collection.TryGetProperty("objects", out var objects)
            || objects.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var obj in objects.EnumerateArray())
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var node = ConvertObjectToNode(obj, context);
            nodes.Add(node);

            // 递归处理嵌套 objectCollection（loopedActivity）。
            if (obj.TryGetProperty("objectCollection", out var innerCollection)
                && innerCollection.ValueKind == JsonValueKind.Object)
            {
                var loopId = ReadString(obj, "id") ?? string.Empty;
                var innerCollectionId = ReadString(innerCollection, "id") ?? $"{loopId}-collection";
                var innerContext = new ObjectContext(innerCollectionId, InsideLoop: true, ParentLoopObjectId: loopId);
                CollectNodesFromCollection(innerCollection, innerContext, nodes);
            }
        }
    }

    private static JsonObject ConvertObjectToNode(JsonElement obj, ObjectContext context)
    {
        var id = ReadString(obj, "id") ?? Guid.NewGuid().ToString("N");
        var kind = ReadString(obj, "kind") ?? ReadString(obj, "officialType") ?? "unknown";

        // 位置从 relativeMiddlePoint 读取，缺失则补 {x:0, y:0}。
        JsonObject position;
        if (obj.TryGetProperty("relativeMiddlePoint", out var rmp) && rmp.ValueKind == JsonValueKind.Object
            && rmp.TryGetProperty("x", out var rx) && rmp.TryGetProperty("y", out var ry))
        {
            position = new JsonObject
            {
                ["x"] = rx.TryGetDouble(out var xd) ? JsonValue.Create(xd) : JsonValue.Create(0.0),
                ["y"] = ry.TryGetDouble(out var yd) ? JsonValue.Create(yd) : JsonValue.Create(0.0),
            };
        }
        else
        {
            position = new JsonObject { ["x"] = 0.0, ["y"] = 0.0 };
        }

        var meta = new JsonObject
        {
            ["position"] = position,
            ["collectionId"] = context.CollectionId,
        };
        if (context.ParentLoopObjectId is not null)
        {
            meta["parentObjectId"] = context.ParentLoopObjectId;
        }

        // data: 包含所有原始字段（保证 roundtrip 保真），再加上 design 协议所需字段。
        var data = new JsonObject
        {
            ["objectKind"] = kind,
            ["objectId"] = id,
            ["collectionId"] = context.CollectionId,
            ["insideLoop"] = context.InsideLoop,
        };
        if (context.ParentLoopObjectId is not null)
        {
            data["parentObjectId"] = context.ParentLoopObjectId;
        }

        // 透传所有原始字段到 data（保真）。
        foreach (var prop in obj.EnumerateObject())
        {
            var key = prop.Name;
            // 跳过已显式写入的结构字段，以及 objectCollection（子节点递归处理）。
            if (key is "id" or "kind" or "relativeMiddlePoint" or "objectCollection")
            {
                continue;
            }
            if (!data.ContainsKey(key))
            {
                data[key] = JsonNode.Parse(prop.Value.GetRawText());
            }
        }

        return new JsonObject
        {
            ["id"] = id,
            ["type"] = kind,
            ["meta"] = meta,
            ["data"] = data,
        };
    }

    private static void CollectNestedEdgesFromCollection(
        JsonElement collection,
        string collectionId,
        JsonArray edges)
    {
        if (collection.TryGetProperty("flows", out var flows) && flows.ValueKind == JsonValueKind.Array)
        {
            CollectEdgesFromFlowArray(flows, collectionId, edges);
        }

        if (!collection.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var obj in objects.EnumerateArray())
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (obj.TryGetProperty("objectCollection", out var innerCollection)
                && innerCollection.ValueKind == JsonValueKind.Object)
            {
                var loopId = ReadString(obj, "id") ?? string.Empty;
                var innerCollectionId = ReadString(innerCollection, "id") ?? $"{loopId}-collection";
                CollectNestedEdgesFromCollection(innerCollection, innerCollectionId, edges);
            }
        }
    }

    private static void CollectEdgesFromFlowArray(JsonElement flows, string collectionId, JsonArray edges)
    {
        foreach (var flow in flows.EnumerateArray())
        {
            if (flow.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var edge = ConvertFlowToEdge(flow, collectionId);
            edges.Add(edge);
        }
    }

    private static JsonObject ConvertFlowToEdge(JsonElement flow, string collectionId)
    {
        var id = ReadString(flow, "id") ?? Guid.NewGuid().ToString("N");
        var source = ReadString(flow, "originObjectId") ?? string.Empty;
        var target = ReadString(flow, "destinationObjectId") ?? string.Empty;

        // edgeKind: 优先从 editor.edgeKind 读取，其次 kind / 默认 sequence。
        string edgeKind;
        if (flow.TryGetProperty("editor", out var editor)
            && editor.ValueKind == JsonValueKind.Object
            && editor.TryGetProperty("edgeKind", out var edgeKindProp)
            && edgeKindProp.ValueKind == JsonValueKind.String)
        {
            edgeKind = edgeKindProp.GetString() ?? "sequence";
        }
        else
        {
            edgeKind = ReadString(flow, "kind") ?? "sequence";
        }

        var data = new JsonObject
        {
            ["flowId"] = id,
            ["flowKind"] = ReadString(flow, "kind") ?? "sequence",
            ["edgeKind"] = edgeKind,
            ["collectionId"] = collectionId,
        };

        // isErrorHandler。
        if (flow.TryGetProperty("isErrorHandler", out var isErr) && isErr.ValueKind == JsonValueKind.True)
        {
            data["isErrorHandler"] = true;
        }

        // caseValues。
        if (flow.TryGetProperty("caseValues", out var caseValues) && caseValues.ValueKind == JsonValueKind.Array)
        {
            data["caseValues"] = JsonNode.Parse(caseValues.GetRawText());
        }

        // 透传 editor 字段到 data（保真）。
        if (flow.TryGetProperty("editor", out var editorNode) && editorNode.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in editorNode.EnumerateObject())
            {
                if (!data.ContainsKey(prop.Name))
                {
                    data[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
                }
            }
        }

        // 透传其他原始字段（保真）。
        foreach (var prop in flow.EnumerateObject())
        {
            var key = prop.Name;
            if (key is "id" or "originObjectId" or "destinationObjectId" or "kind" or "isErrorHandler" or "caseValues" or "editor")
            {
                continue;
            }
            if (!data.ContainsKey(key))
            {
                data[key] = JsonNode.Parse(prop.Value.GetRawText());
            }
        }

        return new JsonObject
        {
            ["id"] = id,
            ["sourceNodeID"] = source,
            ["targetNodeID"] = target,
            ["data"] = data,
        };
    }

    // -------------------------------------------------------------------------
    // JSON 帮助方法
    // -------------------------------------------------------------------------

    private static void CopyStringField(JsonElement source, JsonObject target, string key)
    {
        if (source.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
        {
            target[key] = value.GetString();
        }
    }

    private static void CopyRawField(JsonElement source, JsonObject target, string key)
    {
        if (source.TryGetProperty(key, out var value)
            && value.ValueKind != JsonValueKind.Null
            && value.ValueKind != JsonValueKind.Undefined)
        {
            target[key] = JsonNode.Parse(value.GetRawText());
        }
    }

    private static void CopyRawFieldOrDefault(JsonElement source, JsonObject target, string key, string defaultJson)
    {
        if (source.TryGetProperty(key, out var value)
            && value.ValueKind != JsonValueKind.Null
            && value.ValueKind != JsonValueKind.Undefined)
        {
            target[key] = JsonNode.Parse(value.GetRawText());
        }
        else
        {
            target[key] = JsonNode.Parse(defaultJson);
        }
    }

    private static string? ReadString(JsonElement element, string key)
        => element.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string BuildDefaultEditor()
        => """{"viewport":{"x":0,"y":0,"zoom":1},"zoom":1,"selection":{},"gridEnabled":false,"showMiniMap":false}""";

    private static string BuildDefaultAudit()
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        return $$$"""{"version":"v3","status":"draft","createdBy":"system","createdAt":"{{{now}}}","updatedBy":"system","updatedAt":"{{{now}}}"}""";
    }

    private static MicroflowApiException Invalid(string message, string fieldPath)
        => new(MicroflowApiErrorCode.MicroflowSchemaInvalid,
            $"旧版 schema 升级失败（{fieldPath}）：{message}", 422);
}
