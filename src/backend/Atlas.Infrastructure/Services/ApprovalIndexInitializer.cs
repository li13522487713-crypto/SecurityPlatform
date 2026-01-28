using Atlas.Domain.Approval.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 审批模块数据库索引初始化器（CodeFirst 后创建索引）
/// </summary>
public sealed class ApprovalIndexInitializer
{
    private readonly ISqlSugarClient _db;
    private readonly ILogger<ApprovalIndexInitializer> _logger;

    public ApprovalIndexInitializer(
        ISqlSugarClient db,
        ILogger<ApprovalIndexInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 创建审批模块的高频查询索引
    /// </summary>
    public async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // ApprovalTask 索引
            // 索引1: TenantId + AssigneeValue + Status（我的待办查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalTask),
                "IX_ApprovalTask_TenantId_AssigneeValue_Status",
                $"{nameof(ApprovalTask.TenantIdValue)}, {nameof(ApprovalTask.AssigneeValue)}, {nameof(ApprovalTask.Status)}",
                cancellationToken);

            // 索引2: TenantId + InstanceId（实例任务查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalTask),
                "IX_ApprovalTask_TenantId_InstanceId",
                $"{nameof(ApprovalTask.TenantIdValue)}, {nameof(ApprovalTask.InstanceId)}",
                cancellationToken);

            // 索引3: TenantId + InstanceId + NodeId（节点任务查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalTask),
                "IX_ApprovalTask_TenantId_InstanceId_NodeId",
                $"{nameof(ApprovalTask.TenantIdValue)}, {nameof(ApprovalTask.InstanceId)}, {nameof(ApprovalTask.NodeId)}",
                cancellationToken);

            // 索引3a: TenantId + InstanceId + Status（按状态查询任务）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalTask),
                "IX_ApprovalTask_TenantId_InstanceId_Status",
                $"{nameof(ApprovalTask.TenantIdValue)}, {nameof(ApprovalTask.InstanceId)}, {nameof(ApprovalTask.Status)}",
                cancellationToken);

            // ApprovalHistoryEvent 索引
            // 索引4: TenantId + InstanceId（历史查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalHistoryEvent),
                "IX_ApprovalHistoryEvent_TenantId_InstanceId",
                $"{nameof(ApprovalHistoryEvent.TenantIdValue)}, {nameof(ApprovalHistoryEvent.InstanceId)}",
                cancellationToken);

            // ApprovalNodeExecution 索引
            // 索引5: TenantId + InstanceId（节点执行记录查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalNodeExecution),
                "IX_ApprovalNodeExecution_TenantId_InstanceId",
                $"{nameof(ApprovalNodeExecution.TenantIdValue)}, {nameof(ApprovalNodeExecution.InstanceId)}",
                cancellationToken);

            // 索引6: TenantId + InstanceId + NodeId（特定节点执行记录查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalNodeExecution),
                "IX_ApprovalNodeExecution_TenantId_InstanceId_NodeId",
                $"{nameof(ApprovalNodeExecution.TenantIdValue)}, {nameof(ApprovalNodeExecution.InstanceId)}, {nameof(ApprovalNodeExecution.NodeId)}",
                cancellationToken);

            // ApprovalOperationRecord 索引
            // 索引7: TenantId + InstanceId + IdempotencyKey（幂等性检查）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalOperationRecord),
                "IX_ApprovalOperationRecord_TenantId_InstanceId_IdempotencyKey",
                $"{nameof(ApprovalOperationRecord.TenantIdValue)}, {nameof(ApprovalOperationRecord.InstanceId)}, {nameof(ApprovalOperationRecord.IdempotencyKey)}",
                cancellationToken);

            // ApprovalProcessInstance 索引
            // 索引8: TenantId + InitiatorUserId（我发起的流程查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalProcessInstance),
                "IX_ApprovalProcessInstance_TenantId_InitiatorUserId",
                $"{nameof(ApprovalProcessInstance.TenantIdValue)}, {nameof(ApprovalProcessInstance.InitiatorUserId)}",
                cancellationToken);

            // ApprovalFlowButtonConfig 索引
            // 索引9: TenantId + DefinitionId + ViewType（按钮配置查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalFlowButtonConfig),
                "IX_ApprovalFlowButtonConfig_TenantId_DefinitionId_ViewType",
                $"{nameof(ApprovalFlowButtonConfig.TenantIdValue)}, {nameof(ApprovalFlowButtonConfig.DefinitionId)}, {nameof(ApprovalFlowButtonConfig.ViewType)}",
                cancellationToken);

            // ApprovalParallelToken 索引
            // 索引10: TenantId + InstanceId + GatewayNodeId（并行token查询）
            await CreateIndexIfNotExistsAsync(
                nameof(ApprovalParallelToken),
                "IX_ApprovalParallelToken_TenantId_InstanceId_GatewayNodeId",
                $"{nameof(ApprovalParallelToken.TenantIdValue)}, {nameof(ApprovalParallelToken.InstanceId)}, {nameof(ApprovalParallelToken.GatewayNodeId)}",
                cancellationToken);

            _logger.LogInformation("审批模块数据库索引初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审批模块数据库索引初始化失败");
            throw;
        }
    }

    private async Task CreateIndexIfNotExistsAsync(
        string tableName,
        string indexName,
        string columns,
        CancellationToken cancellationToken)
    {
        try
        {
            // SQLite 创建索引语法（注意：SQLite 不支持 IF NOT EXISTS，需要先检查）
            // 使用 SqlSugar 的 CreateIndex 方法
            // 检查索引是否已存在
            var exists = await _db.Ado.GetDataTableAsync(
                $"SELECT name FROM sqlite_master WHERE type='index' AND name='{indexName}'",
                cancellationToken);

            if (exists.Rows.Count == 0)
            {
                var sql = $"CREATE INDEX {indexName} ON {tableName} ({columns})";
                await _db.Ado.ExecuteCommandAsync(sql, cancellationToken);
                _logger.LogDebug("已创建索引：{IndexName} on {TableName}", indexName, tableName);
            }
            else
            {
                _logger.LogDebug("索引已存在，跳过：{IndexName} on {TableName}", indexName, tableName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建索引失败：{IndexName} on {TableName}", indexName, tableName);
        }
    }
}
