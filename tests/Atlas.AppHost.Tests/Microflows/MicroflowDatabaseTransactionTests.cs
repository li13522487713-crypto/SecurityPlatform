using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Domain.Microflows.Entities;
using Atlas.Infrastructure.Services.Microflows;
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
        var store = new SqlSugarMicroflowRuntimeObjectStore(db);
        var runtime = RuntimeExecutionContext.Create(
            "run-db-uow",
            new MicroflowExecutionPlan { Id = "plan-db-uow", SchemaId = "plan-db-uow", ResourceId = "mf-db-uow" },
            MicroflowRuntimeExecutionMode.PublishedRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            databaseSession: session);

        await unitOfWork.BeginAsync(CancellationToken.None);
        await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-commit",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            Value = JsonSerializer.SerializeToElement(new { id = "order-commit", total = 10 }),
            RuntimeContext = runtime
        }, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        var afterCommit = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1"
        }, CancellationToken.None);
        Assert.Contains(afterCommit.Items, item => item.GetProperty("id").GetString() == "order-commit");

        var rollbackSession = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var rollbackUnitOfWork = new SqlSugarMicroflowDatabaseUnitOfWork(rollbackSession);
        var rollbackRuntime = RuntimeExecutionContext.Create(
            "run-db-uow-rollback",
            new MicroflowExecutionPlan { Id = "plan-db-uow", SchemaId = "plan-db-uow", ResourceId = "mf-db-uow" },
            MicroflowRuntimeExecutionMode.PublishedRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            databaseSession: rollbackSession);
        await rollbackUnitOfWork.BeginAsync(CancellationToken.None);
        await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-rollback",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            Value = JsonSerializer.SerializeToElement(new { id = "order-rollback", total = 20 }),
            RuntimeContext = rollbackRuntime
        }, CancellationToken.None);
        await rollbackUnitOfWork.RollbackAsync(CancellationToken.None);

        var afterRollback = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1"
        }, CancellationToken.None);
        Assert.DoesNotContain(afterRollback.Items, item => item.GetProperty("id").GetString() == "order-rollback");
    }

    private ISqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_dbPath}",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<MicroflowRuntimeObjectStateEntity>();
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
