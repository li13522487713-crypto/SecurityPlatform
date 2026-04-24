using System.Text.Json;
using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 子工作流节点：递归调用目标工作流。
/// Config 参数：
/// - workflowId：目标工作流 ID（必填）
/// - maxDepth：最大递归深度（默认 4）
/// - inheritVariables：是否继承父流程全部变量（默认 true）
/// - inputsVariable：当 inheritVariables=false 时，指定输入对象变量路径
/// - mergeOutputs：是否将子流程输出合并到当前变量（默认 true）
/// - outputKey：将子流程输出整体写入该变量（默认 subworkflow_output）
/// </summary>
public sealed class SubWorkflowNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.SubWorkflow;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var targetWorkflowId = context.GetConfigInt64("workflowId");
        if (targetWorkflowId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "SubWorkflow 节点缺少有效 workflowId。");
        }

        var maxDepth = context.GetConfigInt32("maxDepth", 4);
        if (maxDepth <= 0)
        {
            maxDepth = 4;
        }

        if (context.WorkflowCallStack.Contains(targetWorkflowId))
        {
            return new NodeExecutionResult(
                false,
                outputs,
                $"检测到子工作流循环调用：{targetWorkflowId} 已存在于调用链中。");
        }

        if (context.WorkflowCallStack.Count >= maxDepth)
        {
            return new NodeExecutionResult(
                false,
                outputs,
                $"子工作流调用深度超过限制（maxDepth={maxDepth}）。");
        }

        var metaRepository = context.ServiceProvider.GetRequiredService<IWorkflowMetaRepository>();
        var draftRepository = context.ServiceProvider.GetRequiredService<IWorkflowDraftRepository>();
        var cozeMetaRepository = context.ServiceProvider.GetService<ICozeWorkflowMetaRepository>();
        var cozeDraftRepository = context.ServiceProvider.GetService<ICozeWorkflowDraftRepository>();
        var executionRepository = context.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
        var idGenerator = context.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var dagExecutor = context.ServiceProvider.GetRequiredService<DagExecutor>();

        var meta = await metaRepository.FindActiveByIdAsync(context.TenantId, targetWorkflowId, cancellationToken);
        var draft = meta is null
            ? null
            : await draftRepository.FindByWorkflowIdAsync(context.TenantId, targetWorkflowId, cancellationToken);
        var canvasJson = draft?.CanvasJson;
        var versionNumber = meta?.LatestVersionNumber ?? 0;

        if (meta is null && cozeMetaRepository is not null && cozeDraftRepository is not null)
        {
            var cozeMeta = await cozeMetaRepository.FindActiveByIdAsync(context.TenantId, targetWorkflowId, cancellationToken);
            var cozeDraft = cozeMeta is null
                ? null
                : await cozeDraftRepository.FindByWorkflowIdAsync(context.TenantId, targetWorkflowId, cancellationToken);
            canvasJson = cozeDraft?.SchemaJson;
            versionNumber = cozeMeta?.LatestVersionNumber ?? 0;
        }

        if (string.IsNullOrWhiteSpace(canvasJson))
        {
            return new NodeExecutionResult(false, outputs, $"子工作流 {targetWorkflowId} 不存在或草稿不存在。");
        }

        var canvas = DagExecutor.ParseCanvas(canvasJson);
        if (canvas is null)
        {
            return new NodeExecutionResult(false, outputs, $"子工作流 {targetWorkflowId} 画布无效。");
        }

        var parentExecution = await executionRepository.FindByIdAsync(
            context.TenantId,
            context.ExecutionId,
            cancellationToken);
        var createdByUserId = parentExecution?.CreatedByUserId ?? 0L;
        var childInputs = BuildChildInputs(context);
        var childExecution = new Domain.AiPlatform.Entities.WorkflowExecution(
            context.TenantId,
            targetWorkflowId,
            versionNumber,
            createdByUserId,
            JsonSerializer.Serialize(childInputs),
            idGenerator.NextId());
        await executionRepository.AddAsync(childExecution, cancellationToken);

        var nextStack = context.WorkflowCallStack
            .Concat(new[] { (long)targetWorkflowId })
            .ToArray();
        await dagExecutor.RunAsync(
            context.TenantId,
            childExecution,
            canvas,
            childInputs,
            context.EventChannel,
            cancellationToken,
            nextStack);

        var latestExecution = await executionRepository.FindByIdAsync(
                                  context.TenantId,
                                  childExecution.Id,
                                  cancellationToken)
                              ?? childExecution;

        outputs["subworkflow_id"] = JsonSerializer.SerializeToElement(targetWorkflowId);
        outputs["subworkflow_execution_id"] = JsonSerializer.SerializeToElement(childExecution.Id.ToString());
        outputs["subworkflow_status"] = VariableResolver.CreateStringElement(latestExecution.Status.ToString());

        if (latestExecution.Status == ExecutionStatus.Completed)
        {
            var childOutputVariables = VariableResolver.ParseVariableDictionary(latestExecution.OutputsJson);
            var mergeOutputs = context.GetConfigBoolean("mergeOutputs", true);
            var outputKey = context.GetConfigString("outputKey", "subworkflow_output");
            if (!string.IsNullOrWhiteSpace(outputKey))
            {
                outputs[outputKey] = JsonSerializer.SerializeToElement(childOutputVariables);
            }

            if (mergeOutputs)
            {
                foreach (var output in childOutputVariables)
                {
                    outputs[output.Key] = output.Value;
                }
            }

            return new NodeExecutionResult(true, outputs);
        }

        if (latestExecution.Status == ExecutionStatus.Interrupted)
        {
            return new NodeExecutionResult(
                false,
                outputs,
                latestExecution.ErrorMessage ?? "子工作流执行被中断。",
                latestExecution.InterruptType);
        }

        return new NodeExecutionResult(
            false,
            outputs,
            latestExecution.ErrorMessage ?? $"子工作流执行失败，状态：{latestExecution.Status}");
    }

    private static Dictionary<string, JsonElement> BuildChildInputs(NodeExecutionContext context)
    {
        var inheritVariables = context.GetConfigBoolean("inheritVariables", true);
        if (inheritVariables)
        {
            return new Dictionary<string, JsonElement>(context.Variables, StringComparer.OrdinalIgnoreCase);
        }

        var mapped = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (context.Node.Config.TryGetValue("inputs", out var inputsRaw) && inputsRaw.ValueKind == JsonValueKind.Object)
        {
            if (inputsRaw.TryGetProperty("inputParameters", out var ip) && ip.ValueKind == JsonValueKind.Array)
            {
                foreach (var param in ip.EnumerateArray())
                {
                    if (param.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    {
                        var name = n.GetString() ?? "";
                        if (context.Variables.TryGetValue(name, out var val))
                        {
                            mapped[name] = val.Clone();
                        }
                    }
                }
                return mapped;
            }
        }

        var inputsVariablePath = context.GetConfigString("inputsVariable");
        if (!string.IsNullOrWhiteSpace(inputsVariablePath) &&
            context.TryResolveVariable(inputsVariablePath, out var inputPayload) &&
            inputPayload.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in inputPayload.EnumerateObject())
            {
                mapped[property.Name] = property.Value.Clone();
            }

            return mapped;
        }

        return mapped;
    }
}
