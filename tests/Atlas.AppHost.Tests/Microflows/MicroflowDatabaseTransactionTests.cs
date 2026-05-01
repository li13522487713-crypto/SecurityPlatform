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

public sealed class MicroflowDatabaseTransactionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"microflow-db-uow-{Guid.NewGuid():N}.db");

    [Fact(Skip = "DB-backed runtime object store transaction test needs redesign for metadata-driven SQL store semantics.")]
    public async Task DatabaseUnitOfWork_Commit_And_Rollback_Are_Distinguishable()
    {
        var db = CreateDb();
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var unitOfWork = new SqlSugarMicroflowDatabaseUnitOfWork(session);
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var runtime = RuntimeExecutionContext.Create(
            "run-db-uow",
            new MicroflowExecutionPlan { Id = "plan-db-uow", SchemaId = "plan-db-uow", ResourceId = "mf-db-uow" },
            MicroflowRuntimeExecutionMode.PublishedRun,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            metadataCatalog: TestMetadataCatalog(),
            databaseSession: session);

        await unitOfWork.BeginAsync(CancellationToken.None);
        await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-commit",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            DryRun = false,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                outputVariableName = "order",
                commit = new { enabled = true },
                memberChanges = new object[]
                {
                    new { memberQualifiedName = "Sales.Order/id", valueExpression = new { raw = "\"order-commit\"" } },
                    new { memberQualifiedName = "Sales.Order/total", valueExpression = new { raw = "10" } }
                }
            }),
            RuntimeContext = runtime
        }, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        var afterCommit = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            ActionConfig = JsonSerializer.SerializeToElement(new { outputVariableName = "order" })
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
            metadataCatalog: TestMetadataCatalog(),
            databaseSession: rollbackSession);
        await rollbackUnitOfWork.BeginAsync(CancellationToken.None);
        await store.CreateAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            ObjectId = "order-rollback",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            DryRun = false,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                outputVariableName = "order",
                commit = new { enabled = true },
                memberChanges = new object[]
                {
                    new { memberQualifiedName = "Sales.Order/id", valueExpression = new { raw = "\"order-rollback\"" } },
                    new { memberQualifiedName = "Sales.Order/total", valueExpression = new { raw = "20" } }
                }
            }),
            RuntimeContext = rollbackRuntime
        }, CancellationToken.None);
        await rollbackUnitOfWork.RollbackAsync(CancellationToken.None);

        var afterRollback = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            ActionConfig = JsonSerializer.SerializeToElement(new { outputVariableName = "order" })
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
        db.Ado.ExecuteCommand("CREATE TABLE IF NOT EXISTS sales_order (id TEXT PRIMARY KEY, total NUMERIC);");
        db.CodeFirst.InitTables<MicroflowRuntimeObjectStateEntity>();
        return db;
    }

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
        }
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
