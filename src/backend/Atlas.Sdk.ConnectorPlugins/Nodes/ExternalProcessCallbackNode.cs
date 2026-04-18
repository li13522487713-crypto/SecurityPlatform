using Atlas.Application.ExternalConnectors.Abstractions;

namespace Atlas.Sdk.ConnectorPlugins.Nodes;

/// <summary>
/// "处理外部审批回调" 节点：手动重放某次入站事件（已落到 ExternalCallbackEvent 表中）。
/// inputs: providerId (long), topic (string), rawBodyJson (string), headersJson (string)
/// outputs: status, idempotencyKey
/// </summary>
public sealed class ExternalProcessCallbackNode : IConnectorPluginNode
{
    private readonly IConnectorCallbackInboxService _inbox;

    public ExternalProcessCallbackNode(IConnectorCallbackInboxService inbox) { _inbox = inbox; }

    public string NodeType => "external_process_callback";
    public string DisplayName => "处理外部审批/通讯录回调";
    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var providerId = Convert.ToInt64(context.Inputs.GetValueOrDefault("providerId") ?? 0);
            var topic = context.Inputs.GetValueOrDefault("topic")?.ToString() ?? "unknown";
            var bodyJson = context.Inputs.GetValueOrDefault("rawBodyJson")?.ToString() ?? "{}";
            var headersJson = context.Inputs.GetValueOrDefault("headersJson")?.ToString() ?? "{}";
            var headers = ParseDict(headersJson);
            var query = ParseDict(context.Inputs.GetValueOrDefault("queryJson")?.ToString() ?? "{}");
            var result = await _inbox.AcceptAsync(providerId, topic, query, headers, System.Text.Encoding.UTF8.GetBytes(bodyJson), cancellationToken).ConfigureAwait(false);
            return new ConnectorPluginNodeResult
            {
                Success = string.Equals(result.Status, "accepted", StringComparison.Ordinal),
                Outputs = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = result.Status,
                    ["idempotencyKey"] = result.IdempotencyKey,
                    ["topic"] = result.Topic,
                    ["reason"] = result.Reason,
                },
            };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }

    private static Dictionary<string, string> ParseDict(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String ? prop.Value.GetString() ?? string.Empty : prop.Value.GetRawText();
                }
            }
            return dict;
        }
        catch (System.Text.Json.JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

/// <summary>
/// "同步部门"节点：把"触发增量目录同步事件"暴露成节点。
/// inputs: providerId, eventKind ("UserCreated" 等), entityId
/// outputs: jobId, status
/// </summary>
public sealed class ExternalSyncDepartmentNode : IConnectorPluginNode
{
    private readonly IExternalDirectorySyncService _service;

    public ExternalSyncDepartmentNode(IExternalDirectorySyncService service) { _service = service; }

    public string NodeType => "external_sync_department";
    public string DisplayName => "同步部门变更";
    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var providerId = Convert.ToInt64(context.Inputs.GetValueOrDefault("providerId") ?? 0);
            var eventKind = context.Inputs.GetValueOrDefault("eventKind")?.ToString() ?? "DepartmentUpdated";
            var entityId = context.Inputs.GetValueOrDefault("entityId")?.ToString() ?? string.Empty;
            var evt = new Atlas.Connectors.Core.Models.ExternalDirectoryEvent
            {
                ProviderType = "wecom",
                ProviderTenantId = string.Empty,
                Kind = Enum.TryParse<Atlas.Connectors.Core.Models.ExternalDirectoryEventKind>(eventKind, true, out var k) ? k : Atlas.Connectors.Core.Models.ExternalDirectoryEventKind.DepartmentUpdated,
                EntityId = entityId,
            };
            var job = await _service.ApplyIncrementalEventAsync(providerId, evt, "workflow", cancellationToken).ConfigureAwait(false);
            return new ConnectorPluginNodeResult
            {
                Success = true,
                Outputs = new Dictionary<string, object?>(StringComparer.Ordinal) { ["jobId"] = job.Id, ["status"] = job.Status.ToString() },
            };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }
}

/// <summary>
/// "同步成员"节点。
/// </summary>
public sealed class ExternalSyncMemberNode : IConnectorPluginNode
{
    private readonly IExternalDirectorySyncService _service;

    public ExternalSyncMemberNode(IExternalDirectorySyncService service) { _service = service; }

    public string NodeType => "external_sync_member";
    public string DisplayName => "同步成员变更";
    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var providerId = Convert.ToInt64(context.Inputs.GetValueOrDefault("providerId") ?? 0);
            var eventKind = context.Inputs.GetValueOrDefault("eventKind")?.ToString() ?? "UserUpdated";
            var entityId = context.Inputs.GetValueOrDefault("entityId")?.ToString() ?? string.Empty;
            var evt = new Atlas.Connectors.Core.Models.ExternalDirectoryEvent
            {
                ProviderType = "wecom",
                ProviderTenantId = string.Empty,
                Kind = Enum.TryParse<Atlas.Connectors.Core.Models.ExternalDirectoryEventKind>(eventKind, true, out var k) ? k : Atlas.Connectors.Core.Models.ExternalDirectoryEventKind.UserUpdated,
                EntityId = entityId,
            };
            var job = await _service.ApplyIncrementalEventAsync(providerId, evt, "workflow", cancellationToken).ConfigureAwait(false);
            return new ConnectorPluginNodeResult
            {
                Success = true,
                Outputs = new Dictionary<string, object?>(StringComparer.Ordinal) { ["jobId"] = job.Id, ["status"] = job.Status.ToString() },
            };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }
}
