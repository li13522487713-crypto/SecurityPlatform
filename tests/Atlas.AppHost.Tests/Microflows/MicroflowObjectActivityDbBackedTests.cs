using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Application.Microflows.Runtime.Transactions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Atlas.Infrastructure.Services.Microflows;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowObjectActivityDbBackedTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"microflow-object-activity-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task CreateObjectActionExecutor_Persists_And_Produces_Output_Variable()
    {
        var db = CreateDb();
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var executor = new CreateObjectActionExecutor(store);
        var manager = new MicroflowTransactionManager(new FixedClock());
        var context = BuildActionContext(
            db,
            "createObject",
            new
            {
                entityType = "Sales.Order",
                entityQualifiedName = "Sales.Order",
                outputVariableName = "createdOrder",
                commit = new { enabled = true },
                memberChanges = new object[]
                {
                    new { memberQualifiedName = "Sales.Order/id", valueExpression = new { raw = "\"runtime-order-1\"" } },
                    new { memberQualifiedName = "Sales.Order/total", valueExpression = new { raw = "321" } },
                    new { memberQualifiedName = "Sales.Order/customer_id", valueExpression = new { raw = "\"cust-a\"" } }
                }
            },
            transactionManager: manager,
            mode: MicroflowRuntimeExecutionMode.PublishedRun);

        var result = await executor.ExecuteAsync(context, CancellationToken.None);
        manager.Commit(context.RuntimeExecutionContext, "test");

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.Single(result.ProducedVariables);
        Assert.Equal("createdOrder", result.ProducedVariables[0].Name);
        var count = db.Ado.GetInt("SELECT COUNT(1) FROM sales_order WHERE id=@id", new SugarParameter("@id", "runtime-order-1"));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RetrieveObjectActionExecutor_Reads_DbBacked_Association_Result()
    {
        var db = CreateDb();
        db.Ado.ExecuteCommand("INSERT INTO sales_order (id, total, customer_id) VALUES ('runtime-order-2', 50, 'cust-a')");
        db.Ado.ExecuteCommand("INSERT INTO sales_customer (id, name) VALUES ('cust-a', 'ACME')");
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var executor = new RetrieveObjectActionExecutor(store);
        var context = BuildActionContext(
            db,
            "retrieve",
            new
            {
                entityType = "Sales.Customer",
                outputVariableName = "customers",
                retrieveSource = new
                {
                    kind = "association",
                    associationQualifiedName = "Sales.Order_Customer",
                    startVariableName = "selectedOrder"
                }
            },
            seedVariables: variables =>
            {
                variables.Define(new MicroflowVariableDefinition
                {
                    Name = "selectedOrder",
                    DataTypeJson = JsonSerializer.Serialize(new { kind = "object", entityQualifiedName = "Sales.Order" }),
                    RawValueJson = JsonSerializer.Serialize(new Dictionary<string, object?> { ["id"] = "runtime-order-2", ["customer_id"] = "cust-a", ["$entity"] = "Sales.Order", ["$persisted"] = true }),
                    ValuePreview = "selectedOrder",
                    SourceKind = MicroflowVariableSourceKind.ActionOutput,
                    ScopeKind = MicroflowVariableScopeKind.Action
                });
            });

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(MicroflowActionExecutionStatus.Success, result.Status);
        Assert.NotNull(result.OutputJson);
        Assert.Equal(JsonValueKind.Array, result.OutputJson!.Value.ValueKind);
        Assert.Single(result.OutputJson.Value.EnumerateArray());
        Assert.Equal("cust-a", result.OutputJson.Value.EnumerateArray().First().GetProperty("id").GetString());
    }

    private ISqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_dbPath}",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true
        });
        db.Ado.ExecuteCommand("CREATE TABLE IF NOT EXISTS sales_order (id TEXT PRIMARY KEY, total NUMERIC, customer_id TEXT);");
        db.Ado.ExecuteCommand("CREATE TABLE IF NOT EXISTS sales_customer (id TEXT PRIMARY KEY, name TEXT);");
        return db;
    }

    private static MicroflowActionExecutionContext BuildActionContext(
        ISqlSugarClient db,
        string actionKind,
        object config,
        Action<IMicroflowVariableStore>? seedVariables = null,
        string mode = MicroflowRuntimeExecutionMode.TestRun,
        IMicroflowTransactionManager? transactionManager = null)
    {
        var executionPlan = new MicroflowExecutionPlan
        {
            Id = "mf-object-activity",
            SchemaId = "mf-object-activity",
            ResourceId = "mf-object-activity"
        };
        var manager = transactionManager ?? new MicroflowTransactionManager(new FixedClock());
        var runtime = RuntimeExecutionContext.Create(
            $"run-{actionKind}",
            executionPlan,
            mode,
            new Dictionary<string, JsonElement>(),
            new MicroflowRequestContext { WorkspaceId = "workspace-1", TenantId = "tenant-1", UserId = "user-1" },
            DateTimeOffset.UtcNow,
            transactionManager: manager,
            transactionOptions: new MicroflowRuntimeTransactionOptions(),
            metadataCatalog: MetadataCatalog(),
            databaseSession: new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true));
        seedVariables?.Invoke(runtime.VariableStore);
        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = executionPlan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "node-1", ActionId = "action-1" },
            ActionKind = actionKind,
            ObjectId = "node-1",
            ActionId = "action-1",
            ActionConfig = JsonSerializer.SerializeToElement(config),
            VariableStore = runtime.VariableStore,
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry(),
            MetadataCatalog = MetadataCatalog(),
            Options = new MicroflowActionExecutionOptions { Mode = mode }
        };
    }

    private static MicroflowMetadataCatalogDto MetadataCatalog()
        => new()
        {
            Entities =
            [
                new MetadataEntityDto
                {
                    Id = "sales-order",
                    Name = "Order",
                    QualifiedName = "Sales.Order",
                    ModuleName = "Sales",
                    SchemaName = "main",
                    TableName = "sales_order",
                    AiDatabaseId = "1",
                    DriverCode = "SQLite",
                    Attributes =
                    [
                        new MetadataAttributeDto { Id = "id", Name = "id", QualifiedName = "Sales.Order/id", ColumnName = "id", PrimaryKey = true, Type = JsonSerializer.SerializeToElement(new { kind = "string" }) },
                        new MetadataAttributeDto { Id = "total", Name = "total", QualifiedName = "Sales.Order/total", ColumnName = "total", Type = JsonSerializer.SerializeToElement(new { kind = "decimal" }) },
                        new MetadataAttributeDto { Id = "customer_id", Name = "customer_id", QualifiedName = "Sales.Order/customer_id", ColumnName = "customer_id", Type = JsonSerializer.SerializeToElement(new { kind = "string" }) }
                    ]
                },
                new MetadataEntityDto
                {
                    Id = "sales-customer",
                    Name = "Customer",
                    QualifiedName = "Sales.Customer",
                    ModuleName = "Sales",
                    SchemaName = "main",
                    TableName = "sales_customer",
                    AiDatabaseId = "1",
                    DriverCode = "SQLite",
                    Attributes =
                    [
                        new MetadataAttributeDto { Id = "cust-id", Name = "id", QualifiedName = "Sales.Customer/id", ColumnName = "id", PrimaryKey = true, Type = JsonSerializer.SerializeToElement(new { kind = "string" }) },
                        new MetadataAttributeDto { Id = "cust-name", Name = "name", QualifiedName = "Sales.Customer/name", ColumnName = "name", Type = JsonSerializer.SerializeToElement(new { kind = "string" }) }
                    ]
                }
            ],
            Associations =
            [
                new MetadataAssociationDto
                {
                    Id = "assoc-order-customer",
                    Name = "Order_Customer",
                    QualifiedName = "Sales.Order_Customer",
                    SourceEntityQualifiedName = "Sales.Order",
                    TargetEntityQualifiedName = "Sales.Customer",
                    SourceField = "customer_id",
                    TargetField = "id",
                    JoinType = "inner",
                    BindingMode = "logicalCrossDb"
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

    private sealed class FixedClock : Atlas.Application.Microflows.Infrastructure.IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
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
