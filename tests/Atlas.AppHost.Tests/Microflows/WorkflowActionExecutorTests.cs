using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Connectors;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class WorkflowActionExecutorTests
{
    [Fact]
    public async Task CallWorkflow_Delegates_To_Workflow_Runtime_Client()
    {
        var client = new RecordingWorkflowRuntimeClient
        {
            StartResult = new WorkflowRuntimeStartResult
            {
                Success = true,
                InstanceId = "wf-instance-1"
            }
        };
        var executor = new WorkflowActionExecutor(client);

        var result = await executor.ExecuteAsync(
            CreateContext(
                "callWorkflow",
                new
                {
                    workflowId = "wf-order-approval",
                    version = 2,
                    reference = "order-1001",
                    data = new { orderId = "1001" }
                }),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Equal("wf-order-approval", client.LastWorkflowId);
        Assert.Equal(2, client.LastVersion);
        Assert.Equal("order-1001", client.LastReference);
        Assert.Equal("wf-instance-1", result.OutputJson?.GetProperty("workflow").GetProperty("instanceId").GetString());
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("resume")]
    [InlineData("terminate")]
    public async Task ChangeWorkflowState_Delegates_State_Command(string state)
    {
        var client = new RecordingWorkflowRuntimeClient
        {
            CommandResult = new WorkflowRuntimeCommandResult { Success = true }
        };
        var executor = new WorkflowActionExecutor(client);

        var result = await executor.ExecuteAsync(
            CreateContext(
                "changeWorkflowState",
                new
                {
                    instanceId = "wf-instance-2",
                    state
                }),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Equal(state, client.LastCommand);
    }

    [Fact]
    public async Task CompleteUserTask_Publishes_Event_To_Workflow_Runtime()
    {
        var client = new RecordingWorkflowRuntimeClient
        {
            CommandResult = new WorkflowRuntimeCommandResult { Success = true }
        };
        var executor = new WorkflowActionExecutor(client);

        var result = await executor.ExecuteAsync(
            CreateContext(
                "completeUserTask",
                new
                {
                    taskId = "task-1",
                    eventName = "approval.completed",
                    data = new { approved = true }
                }),
            CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Equal("approval.completed", client.LastEventName);
        Assert.Equal("task-1", client.LastEventKey);
    }

    private static MicroflowActionExecutionContext CreateContext(string actionKind, object config)
    {
        var plan = new MicroflowExecutionPlan
        {
            Id = "wf-action-plan",
            SchemaId = "wf-action-plan",
            ResourceId = "mf-workflow-action"
        };
        var runtime = RuntimeExecutionContext.Create(
            "run-wf-action",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "wf-node", ActionId = "wf-action" },
            ObjectId = "wf-node",
            ActionId = "wf-action",
            ActionKind = actionKind,
            ActionConfig = JsonSerializer.SerializeToElement(config),
            VariableStore = runtime.VariableStore,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            Options = new MicroflowActionExecutionOptions { Mode = MicroflowRuntimeExecutionMode.TestRun }
        };
    }

    private sealed class RecordingWorkflowRuntimeClient : IWorkflowRuntimeClient
    {
        public string? LastWorkflowId { get; private set; }
        public int? LastVersion { get; private set; }
        public string? LastReference { get; private set; }
        public string? LastCommand { get; private set; }
        public string? LastEventName { get; private set; }
        public string? LastEventKey { get; private set; }
        public WorkflowRuntimeStartResult StartResult { get; init; } = new() { Success = true, InstanceId = "wf-default" };
        public WorkflowRuntimeCommandResult CommandResult { get; init; } = new() { Success = true };

        public MicroflowConnectorCapabilityStatus GetCapabilityStatus()
            => new("workflow.action", true, "available");

        public Task<WorkflowRuntimeStartResult> StartWorkflowAsync(string workflowId, int? version, JsonElement? data, string? reference, CancellationToken cancellationToken)
        {
            LastWorkflowId = workflowId;
            LastVersion = version;
            LastReference = reference;
            return Task.FromResult(StartResult);
        }

        public Task<WorkflowRuntimeCommandResult> SuspendWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        {
            LastCommand = "suspend";
            return Task.FromResult(CommandResult);
        }

        public Task<WorkflowRuntimeCommandResult> ResumeWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        {
            LastCommand = "resume";
            return Task.FromResult(CommandResult);
        }

        public Task<WorkflowRuntimeCommandResult> TerminateWorkflowAsync(string instanceId, CancellationToken cancellationToken)
        {
            LastCommand = "terminate";
            return Task.FromResult(CommandResult);
        }

        public Task<WorkflowRuntimeCommandResult> PublishEventAsync(string eventName, string eventKey, JsonElement? eventData, CancellationToken cancellationToken)
        {
            LastEventName = eventName;
            LastEventKey = eventKey;
            return Task.FromResult(CommandResult);
        }
    }
}
