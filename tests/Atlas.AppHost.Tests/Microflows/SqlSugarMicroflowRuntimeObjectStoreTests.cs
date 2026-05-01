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

    [Fact]
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
        var visibleInsideTransaction = db.Ado.GetInt("SELECT COUNT(1) FROM sales_order WHERE id=@id", new SugarParameter("@id", "order-1"));
        Assert.Equal(1, visibleInsideTransaction);

        manager.Rollback(context, "unit test");

        var visibleAfterRollback = db.Ado.GetInt("SELECT COUNT(1) FROM sales_order WHERE id=@id", new SugarParameter("@id", "order-1"));
        Assert.Equal(0, visibleAfterRollback);
    }

    [Fact]
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

        var visibleAfterCommit = db.Ado.GetInt("SELECT COUNT(1) FROM sales_order WHERE id=@id", new SugarParameter("@id", "order-2"));
        Assert.Equal(1, visibleAfterCommit);
    }

    [Fact]
    public async Task Retrieve_Returns_Real_Table_Row_With_Limit_And_Sort()
    {
        var db = CreateDb();
        SeedOrders(db,
            ("order-3", 300m, "cust-a"),
            ("order-4", 120m, "cust-a"),
            ("order-5", 260m, "cust-b"));
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var context = CreateContext(new MicroflowTransactionManager(new TestClock()), new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true));

        var result = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            Limit = 10,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                outputVariableName = "orders",
                retrieveSource = new
                {
                    kind = "database",
                    range = new { kind = "custom", limitExpression = new { raw = "2" }, offsetExpression = new { raw = "0" } },
                    sortItemList = new
                    {
                        items = new object[]
                        {
                            new { attributeQualifiedName = "Sales.Order.total", direction = "desc" }
                        }
                    }
                }
            }),
            RuntimeContext = context
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("order-3", result.Items[0].GetProperty("id").GetString());
        Assert.Equal("order-5", result.Items[1].GetProperty("id").GetString());
        Assert.Single(result.ProducedVariables);
        Assert.Equal("orders", result.ProducedVariables[0].Name);
    }

    [Fact]
    public async Task ChangeMembers_With_CommitEnabled_Updates_Row()
    {
        var db = CreateDb();
        SeedOrders(db, ("order-6", 180m, "cust-a"));
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var manager = new MicroflowTransactionManager(new TestClock());
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var context = CreateContext(manager, session);
        DefineObjectVariable(context, "orderVar", "Sales.Order", JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["id"] = "order-6",
            ["total"] = 180m,
            ["customer_id"] = "cust-a",
            ["$persisted"] = true
        }));

        var result = await store.ChangeAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            DryRun = false,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                changeVariableName = "orderVar",
                commit = new { enabled = true },
                memberChanges = new object[]
                {
                    new { memberQualifiedName = "Sales.Order.total", valueExpression = new { raw = "250" } }
                }
            }),
            RuntimeContext = context
        }, CancellationToken.None);

        Assert.True(result.Success);
        var total = db.Ado.GetDecimal("SELECT total FROM sales_order WHERE id=@id", new SugarParameter("@id", "order-6"));
        Assert.Equal(250m, total);
        Assert.Single(result.ProducedVariables);
        Assert.Equal("orderVar", result.ProducedVariables[0].Name);
    }

    [Fact]
    public async Task Commit_ObjectVariable_Persists_Staged_Object()
    {
        var db = CreateDb();
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var manager = new MicroflowTransactionManager(new TestClock());
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var context = CreateContext(manager, session);
        DefineObjectVariable(context, "newOrder", "Sales.Order", JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["id"] = "order-7",
            ["total"] = 88m,
            ["customer_id"] = "cust-z",
            ["$persisted"] = false
        }));

        var result = await store.CommitAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            DryRun = false,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                objectOrListVariableName = "newOrder"
            }),
            RuntimeContext = context
        }, CancellationToken.None);

        Assert.True(result.Success);
        var count = db.Ado.GetInt("SELECT COUNT(1) FROM sales_order WHERE id=@id", new SugarParameter("@id", "order-7"));
        Assert.Equal(1, count);
        Assert.Single(result.ProducedVariables);
        Assert.Equal("newOrder", result.ProducedVariables[0].Name);
    }

    [Fact]
    public async Task Delete_ObjectVariable_Deletes_Row_And_Emits_Null_Variable()
    {
        var db = CreateDb();
        SeedOrders(db, ("order-8", 90m, "cust-a"));
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var manager = new MicroflowTransactionManager(new TestClock());
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var context = CreateContext(manager, session);
        DefineObjectVariable(context, "deleteOrder", "Sales.Order", JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["id"] = "order-8",
            ["total"] = 90m,
            ["customer_id"] = "cust-a",
            ["$persisted"] = true
        }));

        var result = await store.DeleteAsync(new MicroflowRuntimeObjectMutation
        {
            EntityType = "Sales.Order",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            DryRun = false,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                objectOrListVariableName = "deleteOrder"
            }),
            RuntimeContext = context
        }, CancellationToken.None);

        Assert.True(result.Success);
        var count = db.Ado.GetInt("SELECT COUNT(1) FROM sales_order WHERE id=@id", new SugarParameter("@id", "order-8"));
        Assert.Equal(0, count);
        Assert.Single(result.ProducedVariables);
        Assert.Equal("deleteOrder", result.ProducedVariables[0].Name);
        Assert.True(result.Value.HasValue);
    }

    [Fact]
    public async Task Retrieve_By_Association_Uses_JoinSpec_And_StartVariable()
    {
        var db = CreateDb();
        SeedOrders(db, ("order-9", 300m, "cust-a"));
        SeedCustomers(db, ("cust-a", "ACME"), ("cust-b", "Other"));
        var store = new SqlSugarMicroflowRuntimeObjectStore(new TestClientFactory(db), new DatabaseDialectRegistry([new SqliteDatabaseDialect()]));
        var manager = new MicroflowTransactionManager(new TestClock());
        var session = new SqlSugarMicroflowRuntimeDbSession(db, ownsLifecycle: true);
        var context = CreateContext(manager, session);
        DefineObjectVariable(context, "selectedOrder", "Sales.Order", JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["id"] = "order-9",
            ["total"] = 300m,
            ["customer_id"] = "cust-a",
            ["$persisted"] = true
        }));

        var result = await store.RetrieveAsync(new MicroflowRuntimeObjectQuery
        {
            EntityType = "Sales.Customer",
            WorkspaceId = "workspace-1",
            TenantId = "tenant-1",
            Limit = 10,
            ActionConfig = JsonSerializer.SerializeToElement(new
            {
                outputVariableName = "customers",
                retrieveSource = new
                {
                    kind = "association",
                    associationQualifiedName = "Sales.Order_Customer",
                    startVariableName = "selectedOrder"
                }
            }),
            RuntimeContext = context
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Items);
        Assert.Equal("cust-a", result.Items[0].GetProperty("id").GetString());
        Assert.Equal("customers", result.ProducedVariables[0].Name);
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
                    SchemaName = "main",
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
                        },
                        new MetadataAttributeDto
                        {
                            Id = "customer_id",
                            Name = "customer_id",
                            QualifiedName = "Sales.Order/customer_id",
                            ColumnName = "customer_id",
                            Type = JsonSerializer.SerializeToElement(new { kind = "string" })
                        }
                    ]
                },
                new MetadataEntityDto
                {
                    Id = "sales-customer",
                    Name = "SalesCustomer",
                    QualifiedName = "Sales.Customer",
                    ModuleName = "Sales",
                    TableName = "sales_customer",
                    SchemaName = "main",
                    AiDatabaseId = "1",
                    DriverCode = "SQLite",
                    Attributes =
                    [
                        new MetadataAttributeDto
                        {
                            Id = "cust-id",
                            Name = "id",
                            QualifiedName = "Sales.Customer/id",
                            ColumnName = "id",
                            PrimaryKey = true,
                            Type = JsonSerializer.SerializeToElement(new { kind = "string" })
                        },
                        new MetadataAttributeDto
                        {
                            Id = "cust-name",
                            Name = "name",
                            QualifiedName = "Sales.Customer/name",
                            ColumnName = "name",
                            Type = JsonSerializer.SerializeToElement(new { kind = "string" })
                        }
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

    private static void SeedOrders(ISqlSugarClient db, params (string Id, decimal Total, string CustomerId)[] rows)
    {
        foreach (var row in rows)
        {
            db.Ado.ExecuteCommand(
                "INSERT INTO sales_order (id, total, customer_id) VALUES (@id, @total, @customerId)",
                new SugarParameter("@id", row.Id),
                new SugarParameter("@total", row.Total),
                new SugarParameter("@customerId", row.CustomerId));
        }
    }

    private static void SeedCustomers(ISqlSugarClient db, params (string Id, string Name)[] rows)
    {
        foreach (var row in rows)
        {
            db.Ado.ExecuteCommand(
                "INSERT INTO sales_customer (id, name) VALUES (@id, @name)",
                new SugarParameter("@id", row.Id),
                new SugarParameter("@name", row.Name));
        }
    }

    private static void DefineObjectVariable(RuntimeExecutionContext context, string variableName, string entityQualifiedName, JsonElement payload)
    {
        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = variableName,
            DataTypeJson = JsonSerializer.Serialize(new { kind = "object", entityQualifiedName }),
            RawValueJson = payload.GetRawText(),
            ValuePreview = payload.GetRawText(),
            SourceKind = MicroflowVariableSourceKind.ActionOutput,
            ScopeKind = MicroflowVariableScopeKind.Action
        });
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
