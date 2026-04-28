using Atlas.Application.ExternalConnectors.Abstractions;

namespace Atlas.Sdk.ConnectorPlugins.Nodes;

/// <summary>
/// "触发通讯录全量同步" 节点。
/// inputs: providerId (long)
/// outputs: jobId (long), status (string)
/// </summary>
public sealed class ExternalDirectorySyncTriggerNode : IConnectorPluginNode
{
    private readonly IExternalDirectorySyncService _service;

    public ExternalDirectorySyncTriggerNode(IExternalDirectorySyncService service) { _service = service; }

    public string NodeType => "external_directory_sync_trigger";
    public string DisplayName => "触发通讯录全量同步";
    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var providerId = Convert.ToInt64(context.Inputs.GetValueOrDefault("providerId") ?? 0);
            var job = await _service.RunFullSyncAsync(providerId, "workflow", cancellationToken).ConfigureAwait(false);
            return new ConnectorPluginNodeResult
            {
                Success = true,
                Outputs = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["jobId"] = job.Id,
                    ["status"] = job.Status.ToString(),
                    ["userCreated"] = job.UserCreated,
                    ["userUpdated"] = job.UserUpdated,
                    ["departmentCreated"] = job.DepartmentCreated,
                    ["departmentUpdated"] = job.DepartmentUpdated,
                },
            };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }
}
