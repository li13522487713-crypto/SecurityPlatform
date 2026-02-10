using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批状态回写处理器：审批完成/驳回/取消后，自动更新动态表记录的状态字段。
/// BusinessKey 格式为 "{tableKey}:{recordId}"
/// </summary>
public sealed class ApprovalStatusSyncHandler
{
    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicRecordRepository _recordRepository;
    private readonly ILogger<ApprovalStatusSyncHandler>? _logger;

    public ApprovalStatusSyncHandler(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicRecordRepository recordRepository,
        ILogger<ApprovalStatusSyncHandler>? logger = null)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _recordRepository = recordRepository;
        _logger = logger;
    }

    /// <summary>
    /// 尝试将审批结果回写到动态表记录的状态字段。
    /// 如果 BusinessKey 不符合动态表格式，安静返回（兼容非动态表审批）。
    /// </summary>
    public async Task SyncStatusAsync(
        TenantId tenantId,
        string? businessKey,
        string status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(businessKey))
        {
            return;
        }

        // 解析 BusinessKey: "{tableKey}:{recordId}"
        var colonIndex = businessKey.IndexOf(':');
        if (colonIndex <= 0 || colonIndex >= businessKey.Length - 1)
        {
            return; // 不是动态表格式，忽略
        }

        var tableKey = businessKey[..colonIndex];
        var recordIdStr = businessKey[(colonIndex + 1)..];

        if (!long.TryParse(recordIdStr, out var recordId))
        {
            return;
        }

        try
        {
            var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
            if (table is null || string.IsNullOrWhiteSpace(table.ApprovalStatusField))
            {
                return; // 表不存在或未绑定审批状态字段
            }

            var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);

            // 更新记录状态字段
            var statusFieldValue = new DynamicFieldValueDto
            {
                Field = table.ApprovalStatusField,
                ValueType = "String",
                StringValue = status
            };
            var updateRequest = new DynamicRecordUpsertRequest(new[] { statusFieldValue });
            await _recordRepository.UpdateAsync(tenantId, table, fields, recordId, updateRequest, cancellationToken);

            _logger?.LogInformation(
                "审批状态回写成功：租户={TenantId}, 表={TableKey}, 记录={RecordId}, 状态={Status}",
                tenantId, tableKey, recordId, status);
        }
        catch (Exception ex)
        {
            // 回写失败不应阻塞审批主流程
            _logger?.LogError(ex,
                "审批状态回写失败：租户={TenantId}, BusinessKey={BusinessKey}, 状态={Status}",
                tenantId, businessKey, status);
        }
    }
}
