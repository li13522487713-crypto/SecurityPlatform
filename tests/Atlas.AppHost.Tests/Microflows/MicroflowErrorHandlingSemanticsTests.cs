using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.ErrorHandling;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowErrorHandlingSemanticsTests
{
    [Theory]
    [InlineData(MicroflowErrorHandlingType.Continue, MicroflowErrorHandlingStatus.Continued)]
    [InlineData(MicroflowErrorHandlingType.Rollback, MicroflowErrorHandlingStatus.RolledBack)]
    [InlineData(MicroflowErrorHandlingType.CustomWithRollback, MicroflowErrorHandlingStatus.EnteredErrorHandler)]
    [InlineData(MicroflowErrorHandlingType.CustomWithoutRollback, MicroflowErrorHandlingStatus.EnteredErrorHandler)]
    public void Handle_Returns_Expected_Status(string mode, string expectedStatus)
    {
        var manager = new MicroflowTransactionManager(new TestClock());
        var service = new MicroflowErrorHandlingService(manager, new TestClock());
        var runtime = RuntimeExecutionContext.Create(
            "run-error-handling",
            new MicroflowExecutionPlan
            {
                Id = "plan-error-handling",
                SchemaId = "plan-error-handling",
                StartNodeId = "start",
                Nodes =
                [
                    new MicroflowExecutionNode
                    {
                        ObjectId = "source",
                        Kind = "loopedActivity",
                        ActionId = "source-action",
                        ActionKind = "restCall",
                        ErrorHandling = new MicroflowRuntimeErrorHandlingDto { ErrorHandlingType = mode }
                    },
                    new MicroflowExecutionNode { ObjectId = "handler", Kind = "exclusiveMerge" }
                ],
                Flows =
                [
                    new MicroflowExecutionFlow
                    {
                        FlowId = "error-flow",
                        ControlFlow = "errorHandler",
                        OriginObjectId = "source",
                        DestinationObjectId = "handler",
                        IsErrorHandler = true
                    }
                ],
                ErrorHandlerFlows =
                [
                    new MicroflowExecutionFlow
                    {
                        FlowId = "error-flow",
                        ControlFlow = "errorHandler",
                        OriginObjectId = "source",
                        DestinationObjectId = "handler",
                        IsErrorHandler = true
                    }
                ]
            },
            MicroflowRuntimeExecutionMode.TestRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext(),
            DateTimeOffset.UtcNow,
            transactionManager: manager);
        var node = runtime.ExecutionPlan.Nodes.First();

        var result = service.Handle(new MicroflowErrorHandlingContext
        {
            RuntimeContext = runtime,
            Plan = runtime.ExecutionPlan,
            SourceNode = node,
            ActionResult = new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Failed,
                Error = new MicroflowRuntimeErrorDto
                {
                    Code = "DOMAIN_FAIL",
                    Message = "boom",
                    ObjectId = "source",
                    ActionId = "source-action"
                }
            },
            Error = new MicroflowRuntimeErrorDto
            {
                Code = "DOMAIN_FAIL",
                Message = "boom",
                ObjectId = "source",
                ActionId = "source-action"
            },
            ErrorHandlingType = mode,
            SourceObjectId = "source",
            SourceActionId = "source-action",
            NormalOutgoingFlowId = "normal-flow"
        });

        Assert.Equal(expectedStatus, result.Status);
    }

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
