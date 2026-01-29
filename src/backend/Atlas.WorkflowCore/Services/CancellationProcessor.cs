using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.WorkflowCore.Services;

/// <summary>
/// 取消处理器实现
/// </summary>
public class CancellationProcessor : ICancellationProcessor
{
    private readonly ILogger<CancellationProcessor> _logger;

    public CancellationProcessor(ILogger<CancellationProcessor> logger)
    {
        _logger = logger;
    }

    public Task<bool> ProcessCancellation(
        WorkflowInstance workflow,
        WorkflowDefinition definition,
        ExecutionPointer pointer,
        WorkflowStep step,
        CancellationToken cancellationToken)
    {
        // TODO: 实现取消条件检查
        // 1. 检查步骤的 CancelCondition
        // 2. 如果满足取消条件，标记步骤为取消
        // 3. 触发补偿逻辑（如果有）

        return Task.FromResult(false);
    }
}
