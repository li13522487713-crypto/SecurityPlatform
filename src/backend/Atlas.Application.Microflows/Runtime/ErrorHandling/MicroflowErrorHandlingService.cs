using System.Text.Json;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.Application.Microflows.Runtime.ErrorHandling;

public sealed class MicroflowErrorHandlingService : IMicroflowErrorHandlingService
{
    private const int DefaultMaxErrorHandlingDepth = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroflowTransactionManager _transactionManager;
    private readonly IMicroflowClock _clock;
    private readonly MicroflowErrorHandlerFlowResolver _flowResolver;
    private readonly MicroflowErrorPropagationPolicy _policy;

    public MicroflowErrorHandlingService(IMicroflowTransactionManager transactionManager, IMicroflowClock clock)
    {
        _transactionManager = transactionManager;
        _clock = clock;
        _flowResolver = new MicroflowErrorHandlerFlowResolver();
        _policy = new MicroflowErrorPropagationPolicy();
    }

    public MicroflowErrorHandlingResult Handle(MicroflowErrorHandlingContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var mode = NormalizeMode(context.ErrorHandlingType);
        var resolution = _flowResolver.Resolve(context.Plan, context.SourceNode, mode);
        var diagnostics = new List<MicroflowErrorHandlingDiagnostic>(resolution.Diagnostics);
        var error = NormalizeError(context);
        var depth = context.ErrorDepth + 1;

        if (!_policy.IsErrorHandlerDepthAllowed(context.RuntimeContext, depth, DefaultMaxErrorHandlingDepth))
        {
            diagnostics.Add(Diagnostic(
                RuntimeErrorCode.RuntimeErrorHandlerMaxDepthExceeded,
                "error",
                $"ErrorHandler depth exceeded maxErrorHandlingDepth={DefaultMaxErrorHandlingDepth}.",
                context));
            return BuildResult(
                context,
                MicroflowErrorHandlingStatus.Failed,
                mode,
                error,
                diagnostics,
                transactionSnapshot: context.RuntimeContext.CreateTransactionSnapshot("errorHandlingMaxDepth"),
                shouldStopRun: true,
                message: "Error handler max depth exceeded.");
        }

        if (string.Equals(mode, MicroflowErrorHandlingType.Continue, StringComparison.OrdinalIgnoreCase))
        {
            if (!_policy.SupportsContinue(context.SourceNode))
            {
                diagnostics.Add(Diagnostic(
                    RuntimeErrorCode.RuntimeContinueNotAllowed,
                    "error",
                    "当前 action 不支持 continue 错误处理语义。",
                    context));
                return BuildResult(
                    context,
                    MicroflowErrorHandlingStatus.Failed,
                    mode,
                    error with { Code = RuntimeErrorCode.RuntimeContinueNotAllowed },
                    diagnostics,
                    context.RuntimeContext.CreateTransactionSnapshot("continueNotAllowed"),
                    shouldStopRun: true,
                    message: "Continue is not allowed for this action.");
            }

            var snapshot = _transactionManager.ContinueAfterError(context.RuntimeContext, error);
            context.RuntimeContext.RecordContinuedError(error);
            return BuildResult(
                context,
                MicroflowErrorHandlingStatus.Continued,
                mode,
                error,
                diagnostics,
                snapshot,
                nextFlowId: context.NormalOutgoingFlowId,
                shouldContinueNormalFlow: true,
                message: "Action failed and Runtime continued along normal flow.");
        }

        if (string.Equals(mode, MicroflowErrorHandlingType.Rollback, StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = _transactionManager.RollbackForError(context.RuntimeContext, error, context.SourceObjectId, context.SourceActionId);
            context.RuntimeContext.RecordRollback(error);
            return BuildResult(
                context,
                MicroflowErrorHandlingStatus.RolledBack,
                mode,
                error,
                diagnostics,
                snapshot,
                shouldStopRun: true,
                message: "Action failed and Runtime rolled back the transaction.");
        }

        if (resolution.Flow is null)
        {
            var snapshot = context.RuntimeContext.CreateTransactionSnapshot("missingErrorHandler");
            context.RuntimeContext.RecordUnhandledError(error);
            return BuildResult(
                context,
                MicroflowErrorHandlingStatus.Failed,
                mode,
                error with { Code = RuntimeErrorCode.RuntimeErrorHandlerNotFound },
                diagnostics,
                snapshot,
                shouldStopRun: true,
                message: "Custom error handling requires an ErrorHandlerFlow.");
        }

        if (_policy.WouldReenterSameHandler(context.RuntimeContext, resolution.Flow))
        {
            diagnostics.Add(Diagnostic(
                RuntimeErrorCode.RuntimeErrorHandlerRecursion,
                "error",
                "ErrorHandlerFlow recursion detected.",
                context,
                resolution.Flow.FlowId));
            context.RuntimeContext.RecordUnhandledError(error);
            return BuildResult(
                context,
                MicroflowErrorHandlingStatus.Failed,
                mode,
                error with { Code = RuntimeErrorCode.RuntimeErrorHandlerRecursion },
                diagnostics,
                context.RuntimeContext.CreateTransactionSnapshot("errorHandlerRecursion"),
                shouldStopRun: true,
                message: "Error handler recursion detected.");
        }

        var transactionSnapshot = string.Equals(mode, MicroflowErrorHandlingType.CustomWithRollback, StringComparison.OrdinalIgnoreCase)
            ? _transactionManager.PrepareCustomWithRollback(context.RuntimeContext, error)
            : _transactionManager.PrepareCustomWithoutRollback(context.RuntimeContext, error);
        context.RuntimeContext.RecordCustomHandler(error);

        return BuildResult(
            context,
            MicroflowErrorHandlingStatus.EnteredErrorHandler,
            mode,
            error,
            diagnostics,
            transactionSnapshot,
            nextFlowId: resolution.Flow.FlowId,
            nextObjectId: resolution.Flow.DestinationObjectId,
            latestErrorWritten: true,
            latestHttpResponseWritten: context.LatestHttpResponse.HasValue,
            latestSoapFaultWritten: context.LatestSoapFault.HasValue,
            message: "Entering custom error handler.");
    }

    public MicroflowErrorHandlingResult CompleteHandler(MicroflowErrorHandlingContext context, string terminalStatus, string? terminalObjectId, string? terminalKind)
    {
        var handled = string.Equals(terminalStatus, "success", StringComparison.OrdinalIgnoreCase)
            && string.Equals(terminalKind, "endEvent", StringComparison.OrdinalIgnoreCase);
        var diagnostics = Array.Empty<MicroflowErrorHandlingDiagnostic>();
        if (handled)
        {
            context.RuntimeContext.RecordHandledError(context.Error);
            return BuildResult(
                context,
                MicroflowErrorHandlingStatus.Handled,
                NormalizeMode(context.ErrorHandlingType),
                context.Error,
                diagnostics,
                context.RuntimeContext.CreateTransactionSnapshot("errorHandlerCompleted"),
                latestErrorWritten: true,
                latestHttpResponseWritten: context.LatestHttpResponse.HasValue,
                latestSoapFaultWritten: context.LatestSoapFault.HasValue,
                message: $"Error handler completed at {terminalObjectId ?? "EndEvent"}.");
        }

        context.RuntimeContext.RecordUnhandledError(context.Error);
        return BuildResult(
            context,
            MicroflowErrorHandlingStatus.Failed,
            NormalizeMode(context.ErrorHandlingType),
            context.Error,
            diagnostics,
            context.RuntimeContext.CreateTransactionSnapshot("errorHandlerFailed"),
            shouldStopRun: true,
            message: $"Error handler failed at {terminalObjectId ?? "unknown"}.");
    }

    public MicroflowRuntimeErrorContext BuildRuntimeErrorContext(MicroflowErrorHandlingContext context)
    {
        var error = NormalizeError(context);
        return new MicroflowRuntimeErrorContext
        {
            Code = error.Code,
            Message = error.Message,
            SourceObjectId = error.ObjectId ?? context.SourceObjectId,
            SourceActionId = error.ActionId ?? context.SourceActionId,
            SourceFlowId = error.FlowId ?? context.IncomingFlowId,
            CollectionId = context.CollectionId,
            ActionKind = context.SourceNode.ActionKind,
            Cause = error.Cause,
            LatestHttpResponse = context.LatestHttpResponse,
            LatestSoapFault = context.LatestSoapFault,
            Timestamp = _clock.UtcNow,
            CallStackFrameId = context.RuntimeContext.CurrentCallFrame?.FrameId,
            LoopIteration = context.LoopIteration,
            TransactionStatus = context.RuntimeContext.Transaction?.Status
        };
    }

    private MicroflowErrorHandlingResult BuildResult(
        MicroflowErrorHandlingContext context,
        string status,
        string mode,
        MicroflowRuntimeErrorDto error,
        IReadOnlyList<MicroflowErrorHandlingDiagnostic> diagnostics,
        MicroflowRuntimeTransactionSnapshot? transactionSnapshot,
        string? nextFlowId = null,
        string? nextObjectId = null,
        bool latestErrorWritten = false,
        bool latestHttpResponseWritten = false,
        bool latestSoapFaultWritten = false,
        bool shouldStopRun = false,
        bool shouldContinueNormalFlow = false,
        string? message = null)
    {
        var transactionRolledBack = string.Equals(transactionSnapshot?.Status, MicroflowRuntimeTransactionStatus.RolledBack, StringComparison.OrdinalIgnoreCase);
        var output = JsonSerializer.SerializeToElement(new
        {
            errorHandling = new
            {
                mode,
                sourceObjectId = context.SourceObjectId,
                sourceActionId = context.SourceActionId,
                sourceActionKind = context.SourceNode.ActionKind,
                enteredErrorHandler = string.Equals(status, MicroflowErrorHandlingStatus.EnteredErrorHandler, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, MicroflowErrorHandlingStatus.Handled, StringComparison.OrdinalIgnoreCase),
                errorHandlerFlowId = nextFlowId,
                errorHandlerTargetObjectId = nextObjectId,
                transactionRolledBack,
                continued = string.Equals(status, MicroflowErrorHandlingStatus.Continued, StringComparison.OrdinalIgnoreCase),
                latestErrorWritten,
                latestHttpResponseWritten,
                latestSoapFaultWritten,
                errorDepth = context.ErrorDepth + 1,
                handled = string.Equals(status, MicroflowErrorHandlingStatus.Handled, StringComparison.OrdinalIgnoreCase),
                diagnostics
            }
        }, JsonOptions);

        return new MicroflowErrorHandlingResult
        {
            Status = status,
            NextFlowId = nextFlowId,
            NextObjectId = nextObjectId,
            Diagnostics = diagnostics,
            TransactionSnapshot = transactionSnapshot,
            LatestErrorWritten = latestErrorWritten,
            LatestHttpResponseWritten = latestHttpResponseWritten,
            LatestSoapFaultWritten = latestSoapFaultWritten,
            Error = error,
            ShouldStopRun = shouldStopRun,
            ShouldContinueNormalFlow = shouldContinueNormalFlow,
            Message = message,
            Output = output
        };
    }

    private static MicroflowRuntimeErrorDto NormalizeError(MicroflowErrorHandlingContext context)
        => context.Error with
        {
            ObjectId = context.Error.ObjectId ?? context.SourceObjectId,
            ActionId = context.Error.ActionId ?? context.SourceActionId,
            FlowId = context.Error.FlowId ?? context.IncomingFlowId
        };

    private static string NormalizeMode(string? mode)
        => mode switch
        {
            null or "" => MicroflowErrorHandlingType.Rollback,
            var value when string.Equals(value, MicroflowErrorHandlingType.CustomWithRollback, StringComparison.OrdinalIgnoreCase) => MicroflowErrorHandlingType.CustomWithRollback,
            var value when string.Equals(value, MicroflowErrorHandlingType.CustomWithoutRollback, StringComparison.OrdinalIgnoreCase) => MicroflowErrorHandlingType.CustomWithoutRollback,
            var value when string.Equals(value, MicroflowErrorHandlingType.Continue, StringComparison.OrdinalIgnoreCase) => MicroflowErrorHandlingType.Continue,
            _ => MicroflowErrorHandlingType.Rollback
        };

    private static MicroflowErrorHandlingDiagnostic Diagnostic(
        string code,
        string severity,
        string message,
        MicroflowErrorHandlingContext context,
        string? flowId = null)
        => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            ObjectId = context.SourceObjectId,
            ActionId = context.SourceActionId,
            FlowId = flowId
        };
}
