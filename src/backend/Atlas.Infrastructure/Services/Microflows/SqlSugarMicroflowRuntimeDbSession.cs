using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Runtime.Calls;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Domain.Microflows.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class SqlSugarMicroflowRuntimeDbSession : IMicroflowRuntimeDbSession
{
    private readonly ISqlSugarClient _db;

    public SqlSugarMicroflowRuntimeDbSession(ISqlSugarClient db, bool ownsLifecycle)
    {
        _db = db;
        OwnsLifecycle = ownsLifecycle;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public bool HasActiveTransaction { get; private set; }

    public bool OwnsLifecycle { get; }

    public void Begin(string? transactionId = null)
    {
        if (!OwnsLifecycle || HasActiveTransaction)
        {
            return;
        }

        _db.Ado.BeginTran();
        HasActiveTransaction = true;
    }

    public void Commit(string? transactionId = null, string? reason = null)
    {
        if (!OwnsLifecycle || !HasActiveTransaction)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            _db.Updateable<MicroflowRuntimeObjectStateEntity>()
                .SetColumns(item => new MicroflowRuntimeObjectStateEntity
                {
                    State = MicroflowRuntimeObjectChangeStatus.Committed,
                    UpdatedAt = DateTimeOffset.UtcNow
                })
                .Where(item => item.TransactionId == transactionId && item.State == MicroflowRuntimeObjectChangeStatus.Staged)
                .ExecuteCommand();
        }
        _db.Ado.CommitTran();
        HasActiveTransaction = false;
    }

    public void Rollback(string? transactionId = null, string? reason = null)
    {
        if (!OwnsLifecycle || !HasActiveTransaction)
        {
            return;
        }

        _db.Ado.RollbackTran();
        HasActiveTransaction = false;
    }

    public T? GetNativeSession<T>() where T : class
        => _db as T;
}

public sealed class SqlSugarMicroflowRuntimeDbSessionFactory : IMicroflowRuntimeDbSessionFactory
{
    private readonly ISqlSugarClient _db;

    public SqlSugarMicroflowRuntimeDbSessionFactory(ISqlSugarClient db)
    {
        _db = db;
    }

    public IMicroflowRuntimeDbSession? Create(
        MicroflowRequestContext requestContext,
        IMicroflowRuntimeDbSession? parentSession,
        string transactionBoundary)
    {
        if (string.Equals(transactionBoundary, MicroflowCallTransactionBoundary.NoTransaction, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (parentSession is not null
            && (string.Equals(transactionBoundary, MicroflowCallTransactionBoundary.Inherit, StringComparison.OrdinalIgnoreCase)
                || string.Equals(transactionBoundary, MicroflowCallTransactionBoundary.SharedTransaction, StringComparison.OrdinalIgnoreCase)))
        {
            return parentSession;
        }

        if (string.Equals(transactionBoundary, MicroflowCallTransactionBoundary.ChildTransaction, StringComparison.OrdinalIgnoreCase))
        {
            var config = _db.CurrentConnectionConfig;
            var childDb = new SqlSugarClient(config);
            if (Guid.TryParse(requestContext.TenantId, out var tenantId))
            {
                childDb.QueryFilter.AddTableFilter<Atlas.Core.Abstractions.TenantEntity>(it => it.TenantIdValue == tenantId);
            }

            return new SqlSugarMicroflowRuntimeDbSession(childDb, ownsLifecycle: true);
        }

        return new SqlSugarMicroflowRuntimeDbSession(_db, ownsLifecycle: true);
    }
}
