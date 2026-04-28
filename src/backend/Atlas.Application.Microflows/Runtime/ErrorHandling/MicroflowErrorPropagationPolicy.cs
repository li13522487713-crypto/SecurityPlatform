using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.ErrorHandling;

public sealed class MicroflowErrorPropagationPolicy
{
    private static readonly HashSet<string> ContinueSupportedActionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "callMicroflow",
        "restCall"
    };

    public bool SupportsContinue(MicroflowExecutionNode sourceNode)
    {
        if (string.Equals(sourceNode.Kind, "loopedActivity", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(sourceNode.ActionKind)
            && ContinueSupportedActionKinds.Contains(sourceNode.ActionKind);
    }

    public bool IsErrorHandlerDepthAllowed(RuntimeExecutionContext runtimeContext, int nextDepth, int maxDepth)
        => nextDepth <= Math.Max(1, maxDepth);

    public bool WouldReenterSameHandler(RuntimeExecutionContext runtimeContext, MicroflowExecutionFlow? flow)
    {
        if (flow is null)
        {
            return false;
        }

        return runtimeContext.ErrorStack.Any(frame =>
            string.Equals(frame.ErrorHandlerFlowId, flow.FlowId, StringComparison.Ordinal));
    }
}
