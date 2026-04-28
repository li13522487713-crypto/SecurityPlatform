using Atlas.Application.Microflows.Repositories;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.Microflows;

public sealed class MicroflowStorageTransaction : IMicroflowStorageTransaction
{
    private readonly ISqlSugarClient _db;

    public MicroflowStorageTransaction(ISqlSugarClient db) => _db = db;

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _db.Ado.BeginTran();
        try
        {
            await operation();
            _db.Ado.CommitTran();
        }
        catch
        {
            _db.Ado.RollbackTran();
            throw;
        }
    }
}
