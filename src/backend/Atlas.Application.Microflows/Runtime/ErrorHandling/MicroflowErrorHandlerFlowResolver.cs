using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.ErrorHandling;

public sealed class MicroflowErrorHandlerFlowResolver
{
    public MicroflowErrorHandlerFlowResolution Resolve(
        MicroflowExecutionPlan plan,
        MicroflowExecutionNode sourceNode,
        string errorHandlingType)
    {
        var diagnostics = new List<MicroflowErrorHandlingDiagnostic>();
        var flows = plan.ErrorHandlerFlows
            .Where(flow => string.Equals(flow.OriginObjectId, sourceNode.ObjectId, StringComparison.Ordinal))
            .ToArray();

        if (flows.Length > 1)
        {
            diagnostics.Add(Diagnostic(
                RuntimeErrorCode.RuntimeErrorHandlerRecursion,
                "error",
                "同一 sourceObjectId 存在多个 ErrorHandlerFlow，Runtime 将只使用排序后的第一条。",
                sourceNode,
                flows[0].FlowId));
        }

        var flow = flows
            .OrderBy(item => item.BranchOrder ?? int.MaxValue)
            .ThenBy(item => item.FlowId, StringComparer.Ordinal)
            .FirstOrDefault();

        if (string.Equals(errorHandlingType, MicroflowErrorHandlingType.Rollback, StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorHandlingType, MicroflowErrorHandlingType.Continue, StringComparison.OrdinalIgnoreCase))
        {
            if (flow is not null && string.Equals(errorHandlingType, MicroflowErrorHandlingType.Rollback, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Diagnostic(
                    RuntimeErrorCode.RuntimeErrorHandlerFailed,
                    "warning",
                    "rollback 模式不会进入 ErrorHandlerFlow，已忽略配置的错误分支。",
                    sourceNode,
                    flow.FlowId));
            }

            return new MicroflowErrorHandlerFlowResolution { Flow = null, Diagnostics = diagnostics };
        }

        if (flow is null)
        {
            diagnostics.Add(Diagnostic(
                RuntimeErrorCode.RuntimeErrorHandlerNotFound,
                "error",
                $"{errorHandlingType} 需要 ErrorHandlerFlow，但 sourceObjectId 未配置错误处理分支。",
                sourceNode));
            return new MicroflowErrorHandlerFlowResolution { Diagnostics = diagnostics };
        }

        if (string.IsNullOrWhiteSpace(flow.DestinationObjectId)
            || !plan.Nodes.Any(node => string.Equals(node.ObjectId, flow.DestinationObjectId, StringComparison.Ordinal)))
        {
            diagnostics.Add(Diagnostic(
                RuntimeErrorCode.RuntimeErrorHandlerNotFound,
                "error",
                "ErrorHandlerFlow 的目标节点不存在。",
                sourceNode,
                flow.FlowId));
        }

        if (!string.IsNullOrWhiteSpace(sourceNode.CollectionId)
            && !string.IsNullOrWhiteSpace(flow.CollectionId)
            && !string.Equals(sourceNode.CollectionId, flow.CollectionId, StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic(
                RuntimeErrorCode.RuntimeErrorHandlerFailed,
                "warning",
                "ErrorHandlerFlow 跨 collection，Runtime 将按 ExecutionPlan 保守执行。",
                sourceNode,
                flow.FlowId));
        }

        return new MicroflowErrorHandlerFlowResolution
        {
            Flow = flow,
            Diagnostics = diagnostics
        };
    }

    private static MicroflowErrorHandlingDiagnostic Diagnostic(
        string code,
        string severity,
        string message,
        MicroflowExecutionNode sourceNode,
        string? flowId = null)
        => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            ObjectId = sourceNode.ObjectId,
            ActionId = sourceNode.ActionId,
            FlowId = flowId
        };
}
