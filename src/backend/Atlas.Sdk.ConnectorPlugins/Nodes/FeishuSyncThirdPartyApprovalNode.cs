using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Connectors.Core.Models;

namespace Atlas.Sdk.ConnectorPlugins.Nodes;

/// <summary>
/// "飞书三方审批同步" 专用节点（模式 B）：
/// 把本地审批的最新状态推到飞书审批中心（external_instances + external_instances/check）。
/// inputs:
///   - localInstanceId: long
///   - newStatus: "Approved" | "Rejected" | "Canceled" | "Pending"
///   - commentText: string?
/// outputs:
///   - synced: bool
/// </summary>
public sealed class FeishuSyncThirdPartyApprovalNode : IConnectorPluginNode
{
    private readonly IExternalApprovalDispatchService _dispatch;

    public FeishuSyncThirdPartyApprovalNode(IExternalApprovalDispatchService dispatch)
    {
        _dispatch = dispatch;
    }

    public string NodeType => "feishu_sync_third_party_approval";

    public string DisplayName => "同步飞书三方审批";

    public string Category => "external_collaboration";

    public async Task<ConnectorPluginNodeResult> ExecuteAsync(ConnectorPluginNodeContext context, CancellationToken cancellationToken)
    {
        try
        {
            var localInstanceId = Convert.ToInt64(context.Inputs.GetValueOrDefault("localInstanceId") ?? 0);
            var newStatusText = context.Inputs.GetValueOrDefault("newStatus")?.ToString() ?? "Pending";
            var commentText = context.Inputs.GetValueOrDefault("commentText")?.ToString();

            if (!Enum.TryParse<ExternalApprovalStatus>(newStatusText, ignoreCase: true, out var newStatus))
            {
                newStatus = ExternalApprovalStatus.Pending;
            }

            await _dispatch.OnInstanceStatusChangedAsync(localInstanceId, newStatus, commentText, cancellationToken).ConfigureAwait(false);
            return new ConnectorPluginNodeResult
            {
                Success = true,
                Outputs = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["synced"] = true,
                    ["status"] = newStatus.ToString(),
                },
            };
        }
        catch (Exception ex)
        {
            return new ConnectorPluginNodeResult { Success = false, ErrorCode = ex.GetType().Name, ErrorMessage = ex.Message };
        }
    }
}
