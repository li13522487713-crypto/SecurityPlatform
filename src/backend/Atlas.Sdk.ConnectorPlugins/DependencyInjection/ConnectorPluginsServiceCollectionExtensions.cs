using Atlas.Sdk.ConnectorPlugins.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Sdk.ConnectorPlugins.DependencyInjection;

public static class ConnectorPluginsServiceCollectionExtensions
{
    /// <summary>
    /// 注册 13 个外部连接器工作流节点（v4 报告 27-31 章 + N6 钉钉/飞书三方扩展）：
    /// 通用：身份绑定 / 部门同步 / 成员同步 / 同步触发 / 审批状态查询 / 回调处理；
    /// 渠道：WeCom/Feishu/DingTalk 发消息 + 创建审批；
    /// 飞书三方：sync third party approval（模式 B 专用）。
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
        services.AddScoped<IConnectorPluginNode, DingTalkSendMessageNode>();
        services.AddScoped<IConnectorPluginNode, WeComCreateApprovalNode>();
        services.AddScoped<IConnectorPluginNode, FeishuCreateApprovalNode>();
        services.AddScoped<IConnectorPluginNode, DingTalkCreateApprovalNode>();
        services.AddScoped<IConnectorPluginNode, FeishuSyncThirdPartyApprovalNode>();
        services.AddScoped<IConnectorPluginNode, ExternalQueryApprovalStatusNode>();
        services.AddScoped<IConnectorPluginNode, ExternalProcessCallbackNode>();
        return services;
    }
}
