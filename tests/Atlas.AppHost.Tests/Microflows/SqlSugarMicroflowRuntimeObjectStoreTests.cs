using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Atlas.Infrastructure.Services.Microflows;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Microflows.Entities;
using SqlSugar;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class SqlSugarMicroflowRuntimeObjectStoreTests : IDisposable
{
    private readonly string _dbPath;

    public SqlSugarMicroflowRuntimeObjectStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"microflow-runtime-store-{Guid.NewGuid():N}.db");
    }

    [Fact(Skip = "Legacy SQL runtime object store test needs redesign for metadata-driven DB-backed store.")]
    public async Task Create_Then_Rollback_Does_Not_Leak_Row_To_Next_Run()
    {
        var db = CreateDb();
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var manager = new MicroflowTransactionManager(new TestClock());
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var context = CreateContext(manager, session);

        var create = await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-1",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            DryRun = false,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                outputVariableName = "order",
                commit = new { enabled = true },
                memberChanges = new object[]
                {
                    new { memberQualifiedName = "Sales.Order/id", valueExpression = new { raw = "\"order-1\"" } },
                    new { memberQualifiedName = "Sales.Order/total", valueExpression = new { raw = "100" } }
                }
            }),
            RuntimeContext = context
        }, CancellationToken.None);

        Assert.True(create.Success);
        var inTransaction = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            ActionConfig = JsonSerializer.SerializeToElement(new { outputVariableName = "order" }),
            RuntimeContext = context
        }, CancellationToken.None);
        Assert.Single(inTransaction.Items);

        manager.Rollback(context, "unit test");

        var afterRollback = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            ActionConfig = JsonSerializer.SerializeToElement(new { outputVariableName = "order" })
        }, CancellationToken.None);
        Assert.Empty(afterRollback.Items);
    }

    [Fact(Skip = "Legacy SQL runtime object store test needs redesign for metadata-driven DB-backed store.")]
    public async Task Create_Then_Commit_Persists_Row_For_Next_Run()
    {
        var db = CreateDb();
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var manager = new MicroflowTransactionManager(new TestClock());
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var context = CreateContext(manager, session);

        await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-2",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            DryRun = false,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                outputVariableName = "order",
                commit = new { enabled = true },
                memberChanges = new object[]
                {
                    new { memberQualifiedName = "Sales.Order/id", valueExpression = new { raw = "\"order-2\"" } },
                    new { memberQualifiedName = "Sales.Order/total", valueExpression = new { raw = "200" } }
                }
            }),
            RuntimeContext = context
        }, CancellationToken.None);

        manager.Commit(context, "unit test");

        var afterCommit = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            ActionConfig = JsonSerializer.SerializeToElement(new { outputVariableName = "order" })
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
        db.Ado.ExecuteCommand("CREATE TABLE IF NOT EXISTS sales_order (id TEXT PRIMARY KEY, total NUMERIC);");
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
            metadataCatalog: TestMetadataCatalog(),
            databaseSession: session);

    private static MicroflowMetadataCatalogDto TestMetadataCatalog()
        => new()
        {
            Entities =
            [
                new MetadataEntityDto
                {
                    Id = "sales-order",
                    Name = "SalesOrder",
                    QualifiedName = "Sales.Order",
                    ModuleName = "Sales",
                    TableName = "sales_order",
                    AiDatabaseId = "1",
                    DriverCode = "SQLite",
                    Attributes =
                    [
                        new MetadataAttributeDto
                        {
                            Id = "id",
                            Name = "id",
                            QualifiedName = "Sales.Order/id",
                            ColumnName = "id",
                            PrimaryKey = true,
                            Type = JsonSerializer.SerializeToElement(new { kind = "string" })
                        },
                        new MetadataAttributeDto
                        {
                            Id = "total",
                            Name = "total",
                            QualifiedName = "Sales.Order/total",
                            ColumnName = "total",
                            Type = JsonSerializer.SerializeToElement(new { kind = "decimal" })
                        }
                    ]
                }
            ]
        };

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

    private sealed class TestClientFactory : IAiDatabaseClientFactory
    {
        private readonly SqlSugarClient _db;

        public TestClientFactory(ISqlSugarClient db)
        {
            _db = (SqlSugarClient)db;
        }

        public Task<SqlSugarClient> GetClientAsync(TenantId tenantId, long databaseId, AiDatabaseRecordEnvironment environment, CancellationToken cancellationToken)
            => Task.FromResult(_db);

        public Task<(AiDatabase Database, SqlSugarClient Client)> CreateClientAsync(TenantId tenantId, long databaseId, AiDatabaseRecordEnvironment environment, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void RemoveFromCache(long databaseId, AiDatabaseRecordEnvironment environment)
        {
        }

        public Task<bool> TestConnectionAsync(TenantId tenantId, long databaseId, AiDatabaseRecordEnvironment environment, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }
}
