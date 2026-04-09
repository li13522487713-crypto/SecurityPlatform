using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public static class NodeLifecycleStateMachine
{
    public static bool CanTransit(NodeLifecycleState from, NodeLifecycleState to)
        => from switch
        {
            NodeLifecycleState.Pending => to is NodeLifecycleState.Running,
            NodeLifecycleState.Running => to is NodeLifecycleState.Succeeded or NodeLifecycleState.Failed or NodeLifecycleState.TimedOut,
            NodeLifecycleState.Succeeded => to is NodeLifecycleState.Compensated,
            NodeLifecycleState.Failed => false,
            NodeLifecycleState.TimedOut => false,
            NodeLifecycleState.Compensated => false,
            _ => false
        };

    public static string ToStatus(NodeLifecycleState state)
        => state switch
        {
            NodeLifecycleState.Pending => "Pending",
            NodeLifecycleState.Running => "Running",
            NodeLifecycleState.Succeeded => "Success",
            NodeLifecycleState.Failed => "Failed",
            NodeLifecycleState.TimedOut => "Timeout",
            NodeLifecycleState.Compensated => "Compensated",
            _ => "Unknown"
        };
}
