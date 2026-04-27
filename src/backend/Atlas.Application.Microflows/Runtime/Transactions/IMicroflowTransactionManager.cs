using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Transactions;

public interface IMicroflowTransactionManager
{
    MicroflowRuntimeTransactionContext Begin(
        RuntimeExecutionContext context,
        MicroflowRuntimeTransactionOptions options);

    void Commit(
        RuntimeExecutionContext context,
        string? reason = null);

    void Rollback(
        RuntimeExecutionContext context,
        string? reason = null,
        MicroflowRuntimeErrorDto? error = null);

    MicroflowRuntimeSavepoint CreateSavepoint(
        RuntimeExecutionContext context,
        string name);

    void RollbackToSavepoint(
        RuntimeExecutionContext context,
        string savepointId,
        string? reason = null);

    void TrackCreate(
        RuntimeExecutionContext context,
        MicroflowRuntimeObjectChangeInput input);

    void TrackUpdate(
        RuntimeExecutionContext context,
        MicroflowRuntimeObjectChangeInput input);

    void TrackDelete(
        RuntimeExecutionContext context,
        MicroflowRuntimeObjectChangeInput input);

    void TrackRollbackObject(
        RuntimeExecutionContext context,
        MicroflowRuntimeObjectChangeInput input);

    void TrackCommitAction(
        RuntimeExecutionContext context,
        MicroflowRuntimeCommitActionInput input);

    MicroflowRuntimeTransactionSnapshot CreateSnapshot(
        RuntimeExecutionContext context,
        MicroflowRuntimeTransactionSnapshotOptions options);

    MicroflowRuntimeTransactionSnapshot RollbackForError(
        RuntimeExecutionContext context,
        MicroflowRuntimeErrorDto error,
        string? sourceObjectId,
        string? sourceActionId);

    MicroflowRuntimeTransactionSnapshot PrepareCustomWithRollback(
        RuntimeExecutionContext context,
        MicroflowRuntimeErrorDto error);

    MicroflowRuntimeTransactionSnapshot PrepareCustomWithoutRollback(
        RuntimeExecutionContext context,
        MicroflowRuntimeErrorDto error);

    MicroflowRuntimeTransactionSnapshot ContinueAfterError(
        RuntimeExecutionContext context,
        MicroflowRuntimeErrorDto error);
}
