using System.Text.Json;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 触发器创建/更新执行器（PLAN §M12 S12-3 + P0-2 修复）。
/// Config 参数：
///  - triggerId（可选；为空时新建）
///  - name（必填）
///  - kind（必填，cron/event/webhook）
///  - cron（kind=cron 时必填）
///  - eventName（kind=event 时必填）
///  - workflowId / chatflowId（可选）
///  - enabled（默认 true）
/// 输出：trigger_id / trigger_kind / trigger_enabled。
/// </summary>
public sealed class TriggerUpsertNodeExecutor : INodeExecutor
{
    private readonly IRuntimeTriggerService _triggers;

    public TriggerUpsertNodeExecutor(IRuntimeTriggerService triggers)
    {
        _triggers = triggers;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.TriggerUpsert;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var name = context.GetConfigString("name");
        var kind = context.GetConfigString("kind", "cron");
        if (string.IsNullOrWhiteSpace(name))
        {
            return new NodeExecutionResult(false, outputs, "TriggerUpsert 缺少 name。");
        }

        var triggerId = context.GetConfigString("triggerId");
        var cron = context.GetConfigString("cron");
        var eventName = context.GetConfigString("eventName");
        var workflowId = context.GetConfigString("workflowId");
        var chatflowId = context.GetConfigString("chatflowId");
        var enabled = context.GetConfigBoolean("enabled", true);

        var req = new TriggerUpsertRequest(
            string.IsNullOrWhiteSpace(triggerId) ? null : triggerId,
            name,
            kind,
            string.IsNullOrWhiteSpace(cron) ? null : cron,
            string.IsNullOrWhiteSpace(eventName) ? null : eventName,
            string.IsNullOrWhiteSpace(workflowId) ? null : workflowId,
            string.IsNullOrWhiteSpace(chatflowId) ? null : chatflowId,
            enabled);

        // 工作流执行器无 currentUserId 概念时，使用 0 作为系统调用占位
        // RuntimeTriggerService 的审计会记录 trigger 由工作流（Workflow={workflowId}）创建
        var info = await _triggers.UpsertAsync(context.TenantId, currentUserId: 0L, req, cancellationToken);
        outputs["trigger_id"] = JsonSerializer.SerializeToElement(info.Id);
        outputs["trigger_kind"] = JsonSerializer.SerializeToElement(info.Kind);
        outputs["trigger_enabled"] = JsonSerializer.SerializeToElement(info.Enabled);
        return new NodeExecutionResult(true, outputs);
    }
}

/// <summary>
/// 触发器读取执行器（PLAN §M12 S12-3 + P0-2 修复）。
/// Config：
///  - triggerId（可选，传入则单条返回；为空则返回当前租户全部启用触发器）
/// 输出：triggers（数组）+ trigger_count。
/// </summary>
public sealed class TriggerReadNodeExecutor : INodeExecutor
{
    private readonly IRuntimeTriggerService _triggers;
    private readonly ILowCodeTriggerRepository _repo;

    public TriggerReadNodeExecutor(IRuntimeTriggerService triggers, ILowCodeTriggerRepository repo)
    {
        _triggers = triggers;
        _repo = repo;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.TriggerRead;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var triggerId = context.GetConfigString("triggerId");
        if (!string.IsNullOrWhiteSpace(triggerId))
        {
            var single = await _repo.FindByTriggerIdAsync(context.TenantId, triggerId, cancellationToken);
            if (single is null)
            {
                outputs["triggers"] = JsonSerializer.SerializeToElement(Array.Empty<object>());
                outputs["trigger_count"] = JsonSerializer.SerializeToElement(0);
                return new NodeExecutionResult(true, outputs);
            }
            outputs["triggers"] = JsonSerializer.SerializeToElement(new[] { ToProjection(single) });
            outputs["trigger_count"] = JsonSerializer.SerializeToElement(1);
            return new NodeExecutionResult(true, outputs);
        }

        var list = await _triggers.ListAsync(context.TenantId, cancellationToken);
        outputs["triggers"] = JsonSerializer.SerializeToElement(list);
        outputs["trigger_count"] = JsonSerializer.SerializeToElement(list.Count);
        return new NodeExecutionResult(true, outputs);
    }

    private static object ToProjection(Atlas.Domain.LowCode.Entities.LowCodeTrigger t) => new
    {
        id = t.TriggerId,
        name = t.Name,
        kind = t.Kind,
        cron = t.Cron,
        eventName = t.EventName,
        workflowId = t.WorkflowId,
        chatflowId = t.ChatflowId,
        enabled = t.Enabled,
        createdAt = t.CreatedAt,
        updatedAt = t.UpdatedAt,
        lastFiredAt = t.LastFiredAt
    };
}

/// <summary>
/// 触发器删除执行器（PLAN §M12 S12-3 + P0-2 修复）。
/// Config：triggerId（必填）。输出：deleted（bool）+ trigger_id。
/// </summary>
public sealed class TriggerDeleteNodeExecutor : INodeExecutor
{
    private readonly IRuntimeTriggerService _triggers;

    public TriggerDeleteNodeExecutor(IRuntimeTriggerService triggers)
    {
        _triggers = triggers;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.TriggerDelete;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var triggerId = context.GetConfigString("triggerId");
        if (string.IsNullOrWhiteSpace(triggerId))
        {
            return new NodeExecutionResult(false, outputs, "TriggerDelete 缺少 triggerId。");
        }

        // currentUserId=0 表示系统/工作流上下文
        await _triggers.DeleteAsync(context.TenantId, currentUserId: 0L, triggerId, cancellationToken);
        outputs["deleted"] = JsonSerializer.SerializeToElement(true);
        outputs["trigger_id"] = JsonSerializer.SerializeToElement(triggerId);
        return new NodeExecutionResult(true, outputs);
    }
}
