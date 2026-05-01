using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Infrastructure.Services.Microflows;
using Atlas.Domain.Microflows.Entities;
using SqlSugar;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowDatabaseTransactionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"microflow-db-uow-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task DatabaseUnitOfWork_Commit_And_Rollback_Are_Distinguishable()
    {
        var db = CreateDb();
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var unitOfWork = new SqlSugarMicroflowDatabaseUnitOfWork(session);
        await unitOfWork.BeginAsync(CancellationToken.None);
        db.Ado.ExecuteCommand("INSERT INTO sales_order (id, total) VALUES (@id, @total)",
            new SugarParameter("@id", "order-commit"),
            new SugarParameter("@total", 10m));
        await unitOfWork.CommitAsync(CancellationToken.None);

        var committedRows = db.Ado.GetInt("SELECT COUNT(1) FROM sales_order WHERE id=@id", new SugarParameter("@id", "order-commit"));
        Assert.Equal(1, committedRows);

        var rollbackSession = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var rollbackUnitOfWork = new SqlSugarMicroflowDatabaseUnitOfWork(rollbackSession);
        await rollbackUnitOfWork.BeginAsync(CancellationToken.None);
        db.Ado.ExecuteCommand("INSERT INTO sales_order (id, total) VALUES (@id, @total)",
            new SugarParameter("@id", "order-rollback"),
            new SugarParameter("@total", 20m));
        await rollbackUnitOfWork.RollbackAsync(CancellationToken.None);

        var rolledBackRows = db.Ado.GetInt("SELECT COUNT(1) FROM sales_order WHERE id=@id", new SugarParameter("@id", "order-rollback"));
        Assert.Equal(0, rolledBackRows);
    }

    private ISqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_dbPath}",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true
        });
        db.Ado.ExecuteCommand("CREATE TABLE IF NOT EXISTS sales_order (id TEXT PRIMARY KEY, total NUMERIC);");
        return db;
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
        }
    }
}
