using Atlas.Application.Options;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批模块种子数据服务（CodeFirst + 幂等）
/// </summary>
public sealed class ApprovalSeedDataService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly ApprovalSeedDataOptions _options;
    private readonly ILogger<ApprovalSeedDataService> _logger;

    public ApprovalSeedDataService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGeneratorAccessor,
        IAppContextAccessor appContextAccessor,
        IOptions<ApprovalSeedDataOptions> options,
        ILogger<ApprovalSeedDataService> logger)
    {
        _db = db;
        _idGeneratorAccessor = idGeneratorAccessor;
        _appContextAccessor = appContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 初始化种子数据（幂等）
    /// </summary>
    public async Task InitializeSeedDataAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("审批模块种子数据初始化已禁用");
            return;
        }

        using var appContextScope = _appContextAccessor.BeginScope(CreateSystemContext(tenantId));
        if (_options.InitializeButtonConfigs)
        {
            await InitializeDefaultButtonConfigsAsync(tenantId, cancellationToken);
        }

        if (_options.InitializeExampleFlows)
        {
            // 示例流程定义暂不实现，按需扩展
            _logger.LogInformation("示例流程定义初始化已跳过（暂未实现）");
        }
    }

    /// <summary>
    /// 初始化默认按钮配置（幂等）
    /// </summary>
    private async Task InitializeDefaultButtonConfigsAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        // 默认按钮配置：为所有流程定义提供标准按钮能力
        // 注意：这里不绑定到特定流程定义，而是提供全局默认配置
        // 实际使用时，流程定义可以继承或覆盖这些默认配置

        var defaultButtonConfigs = new[]
        {
            // 发起人视图按钮
            (ApprovalViewType.Initiator, ApprovalButtonType.Submit, "提交"),
            (ApprovalViewType.Initiator, ApprovalButtonType.Resubmit, "重新提交"),
            (ApprovalViewType.Initiator, ApprovalButtonType.ProcessDrawBack, "撤回"),
            (ApprovalViewType.Initiator, ApprovalButtonType.Preview, "预览"),
            (ApprovalViewType.Initiator, ApprovalButtonType.Print, "打印"),

            // 审批人视图按钮
            (ApprovalViewType.Approver, ApprovalButtonType.Agree, "同意"),
            (ApprovalViewType.Approver, ApprovalButtonType.Disagree, "不同意"),
            (ApprovalViewType.Approver, ApprovalButtonType.BackToModify, "打回修改"),
            (ApprovalViewType.Approver, ApprovalButtonType.BackToPrevModify, "打回上节点修改"),
            (ApprovalViewType.Approver, ApprovalButtonType.BackToAnyNode, "退回任意节点"),
            (ApprovalViewType.Approver, ApprovalButtonType.Transfer, "转办"),
            (ApprovalViewType.Approver, ApprovalButtonType.AddAssignee, "加签"),
            (ApprovalViewType.Approver, ApprovalButtonType.RemoveAssignee, "减签"),
            (ApprovalViewType.Approver, ApprovalButtonType.DrawBackAgree, "撤销同意"),
            (ApprovalViewType.Approver, ApprovalButtonType.Preview, "预览"),
            (ApprovalViewType.Approver, ApprovalButtonType.Print, "打印")
        };

        // 使用 DefinitionId = 0 表示全局默认配置（不绑定到特定流程）
        const long globalDefinitionId = 0;

        foreach (var (viewType, buttonType, buttonName) in defaultButtonConfigs)
        {
            var exists = await _db.Queryable<ApprovalFlowButtonConfig>()
                .Where(x => x.TenantIdValue == tenantId.Value
                    && x.DefinitionId == globalDefinitionId
                    && x.ViewType == viewType
                    && x.ButtonType == buttonType)
                .AnyAsync();

            if (!exists)
            {
                var config = new ApprovalFlowButtonConfig(
                    tenantId,
                    globalDefinitionId,
                    viewType,
                    buttonType,
                    buttonName,
                    _idGeneratorAccessor.NextId());
                await _db.Insertable(config).ExecuteCommandAsync(cancellationToken);
                _logger.LogInformation("已创建默认按钮配置：{ViewType} - {ButtonType} - {ButtonName}",
                    viewType, buttonType, buttonName);
            }
        }

        _logger.LogInformation("审批模块默认按钮配置初始化完成");
    }

    private IAppContext CreateSystemContext(TenantId tenantId)
    {
        var appId = _appContextAccessor.GetAppId();
        var clientContext = new ClientContext(
            ClientType.Backend,
            ClientPlatform.Web,
            ClientChannel.App,
            ClientAgent.Other);
        return new AppContextSnapshot(tenantId, appId, null, clientContext, null);
    }
}




