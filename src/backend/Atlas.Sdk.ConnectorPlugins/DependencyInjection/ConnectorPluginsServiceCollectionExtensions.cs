using Atlas.Sdk.ConnectorPlugins.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Sdk.ConnectorPlugins.DependencyInjection;

public static class ConnectorPluginsServiceCollectionExtensions
{
    /// <summary>
    /// 注册 9 个外部连接器工作流节点；由 LogicFlow 编排层在节点执行时按 NodeType 解析对应 IConnectorPluginNode。
    /// </summary>
    public static IServiceCollection AddConnectorPluginNodes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IConnectorPluginNode, ExternalIdentityBindNode>();
        services.AddScoped<IConnectorPluginNode, ExternalDirectorySyncTriggerNode>();
        services.AddScoped<IConnectorPluginNode, ExternalSyncDepartmentNode>();
        services.AddScoped<IConnectorPluginNode, ExternalSyncMemberNode>();
        services.AddScoped<IConnectorPluginNode, WeComSendMessageNode>();
        services.AddScoped<IConnectorPluginNode, FeishuSendMessageNode>();
        services.AddScoped<IConnectorPluginNode, WeComCreateApprovalNode>();
        services.AddScoped<IConnectorPluginNode, FeishuCreateApprovalNode>();
        services.AddScoped<IConnectorPluginNode, ExternalQueryApprovalStatusNode>();
        services.AddScoped<IConnectorPluginNode, ExternalProcessCallbackNode>();
        return services;
    }
}
