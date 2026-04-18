using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Connectors.Core.Models;

namespace Atlas.Sdk.ConnectorPlugins.Nodes;

/// <summary>
/// "查询/同步外部审批状态" 节点：仅作回流触发；具体推进由 ExternalApprovalDispatchService.OnInstanceStatusChangedAsync 完成。
/// inputs:
///   - localInstanceId: long
///   - newStatus: "Pending" / "Approved" / "Rejected" / "Canceled" / "Deleted" / "Reverted"
///   - commentText: string?
/// outputs: { applied: bool }
/// </summary>
public sealed class ExternalQueryApprovalStatusNode : IConnectorPluginNode
{
    private readonly IExternalApprovalDispatchService _dispatch;

    public ExternalQueryApprovalStatusNode(IExternalApprovalDispatchService dispatch) { _dispatch = dispatch; }

    public string NodeType => "external_query_approval_status";
    public string DisplayName => "同步外部审批状态";
    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var localInstanceId = Convert.ToInt64(context.Inputs.GetValueOrDefault("localInstanceId") ?? 0);
            var statusRaw = context.Inputs.GetValueOrDefault("newStatus")?.ToString() ?? "Unknown";
            if (!Enum.TryParse<ExternalApprovalStatus>(statusRaw, true, out var status))
            {
                status = ExternalApprovalStatus.Unknown;
            }
            var comment = context.Inputs.GetValueOrDefault("commentText")?.ToString();
            await _dispatch.OnInstanceStatusChangedAsync(localInstanceId, status, comment, cancellationToken).ConfigureAwait(false);
            return new ConnectorPluginNodeResult { Success = true, Outputs = new Dictionary<string, object?> { ["applied"] = true } };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }
}
