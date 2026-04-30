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

public sealed class SqlSugarMicroflowRuntimeObjectStoreTests : IDisposable
{
    private readonly string _dbPath;

    public SqlSugarMicroflowRuntimeObjectStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"microflow-runtime-store-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public async Task Create_Then_Rollback_Does_Not_Leak_Row_To_Next_Run()
    {
        var db = CreateDb();
        var store = new SqlSugarMicroflowRuntimeObjectStore(db);
        var manager = new MicroflowTransactionManager(new TestClock());
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var context = CreateContext(manager, session);

        var create = await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-1",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            Value = JsonSerializer.SerializeToElement(new { id = "order-1", total = 100m }),
            RuntimeContext = context
        }, CancellationToken.None);

        Assert.True(create.Success);
        var inTransaction = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            RuntimeContext = context
        }, CancellationToken.None);
        Assert.Single(inTransaction.Items);

        manager.Rollback(context, "unit test");

        var afterRollback = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1"
        }, CancellationToken.None);
        Assert.Empty(afterRollback.Items);
    }

    [Fact]
    public async Task Create_Then_Commit_Persists_Row_For_Next_Run()
    {
        var db = CreateDb();
        var store = new SqlSugarMicroflowRuntimeObjectStore(db);
        var manager = new MicroflowTransactionManager(new TestClock());
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var context = CreateContext(manager, session);

        await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-2",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            Value = JsonSerializer.SerializeToElement(new { id = "order-2", total = 200m }),
            RuntimeContext = context
        }, CancellationToken.None);

        manager.Commit(context, "unit test");

        var afterCommit = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1"
        }, CancellationToken.None);
        Assert.Single(afterCommit.Items);
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

    private static RuntimeExecutionContext CreateContext(IMicroflowTransactionManager manager, IMicroflowRuntimeDbSession session)
        => RuntimeExecutionContext.Create(
            "run-object-store-test",
            new MicroflowExecutionPlan
            {
                Id = "object-store-test",
                SchemaId = "object-store-test",
                ResourceId = "mf-object-store"
            },
            MicroflowRuntimeExecutionMode.TestRun,
            input: new Dictionary<string, JsonElement>(),
            securityContext: new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            startedAt: DateTimeOffset.UtcNow,
            transactionManager: manager,
            transactionOptions: new MicroflowRuntimeTransactionOptions(),
            databaseSession: session);

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
            // best-effort cleanup
        }
    }

    private sealed class TestClock : Atlas.Application.Microflows.Infrastructure.IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
