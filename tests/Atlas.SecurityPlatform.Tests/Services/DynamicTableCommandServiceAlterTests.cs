using Atlas.Application.DynamicTables.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class DynamicTableCommandServiceAlterTests
{
    private const long DefaultAppInstanceId = 1001;

    [Fact]
    public async Task AlterAsync_ShouldAddField_AndSupportRecordWrite()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(5000);
            var appContextAccessor = new FakeAppContextAccessor(DefaultAppInstanceId.ToString());
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
                relationRepository,
                fieldPermissionRepository,
                recordRepository,
                migrationRepository,
                idGenerator,
                db,
                TimeProvider.System,
                appContextAccessor);

            var tenantId = new TenantId(Guid.Parse("66666666-6666-6666-6666-666666666666"));
            await service.CreateAsync(
                tenantId,
                userId: 1,
                new DynamicTableCreateRequest(
                    "orders_alter",
                    "订单-变更测试",
                    "M9 alter 测试",
                    "Sqlite",
                    new[]
                    {
                        new DynamicFieldDefinition("id", "主键", "Long", null, null, null, false, true, true, true, null, 0),
                        new DynamicFieldDefinition("orderNo", "订单号", "String", 50, null, null, false, false, false, false, null, 1)
                    },
                    Array.Empty<DynamicIndexDefinition>(),
                    DefaultAppInstanceId.ToString()),
                CancellationToken.None);

            await service.AlterAsync(
                tenantId,
                userId: 1,
                "orders_alter",
                new DynamicTableAlterRequest(
                    new[]
                    {
                        new DynamicFieldDefinition("remark", "备注", "String", 200, null, null, true, false, false, false, null, 2)
                    },
                    Array.Empty<DynamicFieldUpdateDefinition>(),
                    Array.Empty<string>()),
                CancellationToken.None);

            var table = await tableRepository.FindByKeyAsync(tenantId, "orders_alter", DefaultAppInstanceId, CancellationToken.None);
            Assert.NotNull(table);
            var fields = await fieldRepository.ListByTableIdAsync(tenantId, table!.Id, CancellationToken.None);
            Assert.Contains(fields, x => x.Name.Equals("remark", StringComparison.OrdinalIgnoreCase));
            var (migrations, migrationCount) = await migrationRepository.QueryPageAsync(tenantId, table.Id, 1, 20, CancellationToken.None);
            Assert.Equal(1, migrationCount);
            Assert.Contains(migrations, x => x.OperationType == "ADD_FIELDS" && x.Status == "Succeeded");

            var recordId = await recordRepository.InsertAsync(
                tenantId,
                table,
                fields,
                new DynamicRecordUpsertRequest(
                    new[]
                    {
                        new DynamicFieldValueDto { Field = "orderNo", ValueType = "String", StringValue = "SO-ALTER-1" },
                        new DynamicFieldValueDto { Field = "remark", ValueType = "String", StringValue = "新增字段写入成功" }
                    }),
                CancellationToken.None);

            var record = await recordRepository.GetByIdAsync(tenantId, table, fields, recordId, CancellationToken.None);
            Assert.NotNull(record);
            Assert.Contains(record!.Values, x => x.Field == "remark" && x.StringValue == "新增字段写入成功");
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task AlterAsync_ShouldRejectUnsupportedFieldStructuralUpdate()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(6000);
            var appContextAccessor = new FakeAppContextAccessor(DefaultAppInstanceId.ToString());
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
                relationRepository,
                fieldPermissionRepository,
                recordRepository,
                migrationRepository,
                idGenerator,
                db,
                TimeProvider.System,
                appContextAccessor);

            var tenantId = new TenantId(Guid.Parse("77777777-7777-7777-7777-777777777777"));
            await service.CreateAsync(
                tenantId,
                userId: 1,
                new DynamicTableCreateRequest(
                    "orders_alter_reject",
                    "订单-不支持变更",
                    null,
                    "Sqlite",
                    new[]
                    {
                        new DynamicFieldDefinition("id", "主键", "Long", null, null, null, false, true, true, true, null, 0),
                        new DynamicFieldDefinition("orderNo", "订单号", "String", 50, null, null, false, false, false, false, null, 1)
                    },
                    Array.Empty<DynamicIndexDefinition>(),
                    DefaultAppInstanceId.ToString()),
                CancellationToken.None);

            await Assert.ThrowsAsync<BusinessException>(() => service.AlterAsync(
                tenantId,
                userId: 1,
                "orders_alter_reject",
                new DynamicTableAlterRequest(
                    Array.Empty<DynamicFieldDefinition>(),
                    new[]
                    {
                        new DynamicFieldUpdateDefinition("orderNo", "订单号-更新", 128, null, null, null, null, null, null)
                    },
                    Array.Empty<string>()),
                CancellationToken.None));
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task AlterAsync_ShouldUpdateFieldDisplayNameAndSortOrder()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(6500);
            var appContextAccessor = new FakeAppContextAccessor(DefaultAppInstanceId.ToString());
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
                relationRepository,
                fieldPermissionRepository,
                recordRepository,
                migrationRepository,
                idGenerator,
                db,
                TimeProvider.System,
                appContextAccessor);

            var tenantId = new TenantId(Guid.Parse("71717171-7171-7171-7171-717171717171"));
            await service.CreateAsync(
                tenantId,
                userId: 1,
                new DynamicTableCreateRequest(
                    "orders_alter_update_meta",
                    "订单-更新字段元数据",
                    null,
                    "Sqlite",
                    new[]
                    {
                        new DynamicFieldDefinition("id", "主键", "Long", null, null, null, false, true, true, true, null, 0),
                        new DynamicFieldDefinition("orderNo", "订单号", "String", 50, null, null, false, false, false, false, null, 1)
                    },
                    Array.Empty<DynamicIndexDefinition>(),
                    DefaultAppInstanceId.ToString()),
                CancellationToken.None);

            await service.AlterAsync(
                tenantId,
                userId: 1,
                "orders_alter_update_meta",
                new DynamicTableAlterRequest(
                    Array.Empty<DynamicFieldDefinition>(),
                    new[]
                    {
                        new DynamicFieldUpdateDefinition("orderNo", "订单编号", null, null, null, null, null, null, 9)
                    },
                    Array.Empty<string>()),
                CancellationToken.None);

            var table = await tableRepository.FindByKeyAsync(tenantId, "orders_alter_update_meta", DefaultAppInstanceId, CancellationToken.None);
            Assert.NotNull(table);
            var fields = await fieldRepository.ListByTableIdAsync(tenantId, table!.Id, CancellationToken.None);
            var orderNo = fields.Single(x => x.Name == "orderNo");
            Assert.Equal("订单编号", orderNo.DisplayName);
            Assert.Equal(9, orderNo.SortOrder);
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task PreviewAlterAsync_ShouldReturnSql_WithoutChangingSchema()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(7000);
            var appContextAccessor = new FakeAppContextAccessor(DefaultAppInstanceId.ToString());
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
                relationRepository,
                fieldPermissionRepository,
                recordRepository,
                migrationRepository,
                idGenerator,
                db,
                TimeProvider.System,
                appContextAccessor);

            var tenantId = new TenantId(Guid.Parse("88888888-8888-8888-8888-888888888888"));
            await service.CreateAsync(
                tenantId,
                userId: 1,
                new DynamicTableCreateRequest(
                    "orders_preview",
                    "订单-预览测试",
                    null,
                    "Sqlite",
                    new[]
                    {
                        new DynamicFieldDefinition("id", "主键", "Long", null, null, null, false, true, true, true, null, 0),
                        new DynamicFieldDefinition("orderNo", "订单号", "String", 50, null, null, false, false, false, false, null, 1)
                    },
                    Array.Empty<DynamicIndexDefinition>(),
                    DefaultAppInstanceId.ToString()),
                CancellationToken.None);

            var preview = await service.PreviewAlterAsync(
                tenantId,
                "orders_preview",
                new DynamicTableAlterRequest(
                    new[]
                    {
                        new DynamicFieldDefinition("remark_preview", "备注预览", "String", 80, null, null, true, false, false, true, null, 3)
                    },
                    Array.Empty<DynamicFieldUpdateDefinition>(),
                    Array.Empty<string>()),
                CancellationToken.None);

            Assert.Equal("orders_preview", preview.TableKey);
            Assert.Equal("ADD_FIELDS", preview.OperationType);
            Assert.Contains(preview.SqlScripts, script => script.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(preview.SqlScripts, script => script.Contains("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase));

            var table = await tableRepository.FindByKeyAsync(tenantId, "orders_preview", DefaultAppInstanceId, CancellationToken.None);
            Assert.NotNull(table);
            var fields = await fieldRepository.ListByTableIdAsync(tenantId, table!.Id, CancellationToken.None);
            Assert.DoesNotContain(fields, x => x.Name.Equals("remark_preview", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistNullApprovalBinding_WhenNotBound()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(8000);
            var appContextAccessor = new FakeAppContextAccessor(DefaultAppInstanceId.ToString());
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
                relationRepository,
                fieldPermissionRepository,
                recordRepository,
                migrationRepository,
                idGenerator,
                db,
                TimeProvider.System,
                appContextAccessor);

            var tenantId = new TenantId(Guid.Parse("91919191-9191-9191-9191-919191919191"));
            await service.CreateAsync(
                tenantId,
                userId: 1,
                new DynamicTableCreateRequest(
                    "orders_create_approval_null",
                    "订单-默认未绑定审批",
                    null,
                    "Sqlite",
                    new[]
                    {
                        new DynamicFieldDefinition("id", "主键", "Long", null, null, null, false, true, true, true, null, 0),
                        new DynamicFieldDefinition("orderNo", "订单号", "String", 50, null, null, false, false, false, false, null, 1),
                        new DynamicFieldDefinition("approvalStatus", "审批状态", "String", 20, null, null, true, false, false, false, null, 2)
                    },
                    Array.Empty<DynamicIndexDefinition>(),
                    DefaultAppInstanceId.ToString()),
                CancellationToken.None);

            var table = await tableRepository.FindByKeyAsync(
                tenantId,
                "orders_create_approval_null",
                DefaultAppInstanceId,
                CancellationToken.None);

            Assert.NotNull(table);
            Assert.Null(table!.ApprovalFlowDefinitionId);
            Assert.Null(table.ApprovalStatusField);
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task BindApprovalFlowAsync_ShouldBindAndUnbindSuccessfully()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(9000);
            var appContextAccessor = new FakeAppContextAccessor(DefaultAppInstanceId.ToString());
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
                relationRepository,
                fieldPermissionRepository,
                recordRepository,
                migrationRepository,
                idGenerator,
                db,
                TimeProvider.System,
                appContextAccessor);

            var tenantId = new TenantId(Guid.Parse("92929292-9292-9292-9292-929292929292"));
            await service.CreateAsync(
                tenantId,
                userId: 1,
                new DynamicTableCreateRequest(
                    "orders_bind_unbind",
                    "订单-绑定解绑审批",
                    null,
                    "Sqlite",
                    new[]
                    {
                        new DynamicFieldDefinition("id", "主键", "Long", null, null, null, false, true, true, true, null, 0),
                        new DynamicFieldDefinition("orderNo", "订单号", "String", 50, null, null, false, false, false, false, null, 1),
                        new DynamicFieldDefinition("approvalStatus", "审批状态", "String", 20, null, null, true, false, false, false, null, 2)
                    },
                    Array.Empty<DynamicIndexDefinition>(),
                    DefaultAppInstanceId.ToString()),
                CancellationToken.None);

            await service.BindApprovalFlowAsync(
                tenantId,
                userId: 1,
                tableKey: "orders_bind_unbind",
                request: new DynamicTableApprovalBindingRequest(20001, "approvalStatus"),
                cancellationToken: CancellationToken.None);

            var table = await tableRepository.FindByKeyAsync(
                tenantId,
                "orders_bind_unbind",
                DefaultAppInstanceId,
                CancellationToken.None);
            Assert.NotNull(table);
            Assert.Equal(20001, table!.ApprovalFlowDefinitionId);
            Assert.Equal("approvalStatus", table.ApprovalStatusField);

            await service.BindApprovalFlowAsync(
                tenantId,
                userId: 1,
                tableKey: "orders_bind_unbind",
                request: new DynamicTableApprovalBindingRequest(null, null),
                cancellationToken: CancellationToken.None);

            var unbound = await tableRepository.FindByKeyAsync(
                tenantId,
                "orders_bind_unbind",
                DefaultAppInstanceId,
                CancellationToken.None);
            Assert.NotNull(unbound);
            Assert.Null(unbound!.ApprovalFlowDefinitionId);
            Assert.Null(unbound.ApprovalStatusField);
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldAutoRepairLegacyNotNullApprovalColumns()
    {
        var mainDbPath = CreateTempDbPath();
        var appDbPath = CreateTempDbPath();
        SqlSugarClient? mainDb = null;
        SqlSugarClient? appDb = null;
        MemoryCache? cache = null;
        try
        {
            mainDb = CreateDb(mainDbPath);
            appDb = CreateDb(appDbPath);
            cache = new MemoryCache(new MemoryCacheOptions());

            mainDb.CodeFirst.InitTables<AppDataRoutePolicy>();
            await CreateSchemaAsync(appDb);
            await ForceLegacyDynamicTableApprovalNotNullAsync(appDb);

            Assert.True(IsColumnNotNull(appDb, "DynamicTable", "ApprovalFlowDefinitionId"));
            Assert.True(IsColumnNotNull(appDb, "DynamicTable", "ApprovalStatusField"));

            var tenantId = new TenantId(Guid.Parse("93939393-9393-9393-9393-939393939393"));
            const long appInstanceId = 3001;
            var appContextAccessor = new FakeAppContextAccessor(appInstanceId.ToString());
            var scopeFactory = new AppDbScopeFactory(
                new FakeTenantDbConnectionFactory(new TenantDbConnectionInfo($"Data Source={appDbPath}", "Sqlite")),
                mainDb,
                cache);

            var tableRepository = new DynamicTableRepository(mainDb, scopeFactory, appContextAccessor);
            var fieldRepository = new DynamicFieldRepository(mainDb, scopeFactory, appContextAccessor);
            var indexRepository = new DynamicIndexRepository(mainDb, scopeFactory, appContextAccessor);
            var relationRepository = new DynamicRelationRepository(mainDb, scopeFactory, appContextAccessor);
            var fieldPermissionRepository = new FieldPermissionRepository(mainDb, scopeFactory, appContextAccessor);
            var recordRepository = new DynamicRecordRepository(mainDb, scopeFactory);
            var migrationRepository = new DynamicSchemaMigrationRepository(mainDb, scopeFactory, appContextAccessor);
            var idGenerator = new SequentialIdGenerator(10000);
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
                relationRepository,
                fieldPermissionRepository,
                recordRepository,
                migrationRepository,
                idGenerator,
                scopeFactory,
                TimeProvider.System,
                appContextAccessor);

            await service.CreateAsync(
                tenantId,
                userId: 1,
                new DynamicTableCreateRequest(
                    "orders_legacy_repair",
                    "订单-旧结构修复",
                    null,
                    "Sqlite",
                    new[]
                    {
                        new DynamicFieldDefinition("id", "主键", "Long", null, null, null, false, true, true, true, null, 0),
                        new DynamicFieldDefinition("orderNo", "订单号", "String", 50, null, null, false, false, false, false, null, 1)
                    },
                    Array.Empty<DynamicIndexDefinition>(),
                    appInstanceId.ToString()),
                CancellationToken.None);

            var repairedDb = await scopeFactory.GetAppClientAsync(tenantId, appInstanceId, CancellationToken.None);
            Assert.False(IsColumnNotNull(repairedDb, "DynamicTable", "ApprovalFlowDefinitionId"));
            Assert.False(IsColumnNotNull(repairedDb, "DynamicTable", "ApprovalStatusField"));

            var table = await tableRepository.FindByKeyAsync(
                tenantId,
                "orders_legacy_repair",
                appInstanceId,
                CancellationToken.None);
            Assert.NotNull(table);
            Assert.Null(table!.ApprovalFlowDefinitionId);
            Assert.Null(table.ApprovalStatusField);
        }
        finally
        {
            cache?.Dispose();
            mainDb?.Dispose();
            appDb?.Dispose();
            CleanupDbFile(mainDbPath);
            CleanupDbFile(appDbPath);
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldAutoRepairLegacyDynamicFieldLengthNotNull()
    {
        var mainDbPath = CreateTempDbPath();
        var appDbPath = CreateTempDbPath();
        SqlSugarClient? mainDb = null;
        SqlSugarClient? appDb = null;
        MemoryCache? cache = null;
        try
        {
            mainDb = CreateDb(mainDbPath);
            appDb = CreateDb(appDbPath);
            cache = new MemoryCache(new MemoryCacheOptions());

            mainDb.CodeFirst.InitTables<AppDataRoutePolicy>();
            await CreateSchemaAsync(appDb);
            await ForceLegacyDynamicFieldLengthNotNullAsync(appDb);

            Assert.True(IsColumnNotNull(appDb, "DynamicField", "Length"));

            var tenantId = new TenantId(Guid.Parse("94949494-9494-9494-9494-949494949494"));
            const long appInstanceId = 3002;
            var appContextAccessor = new FakeAppContextAccessor(appInstanceId.ToString());
            var scopeFactory = new AppDbScopeFactory(
                new FakeTenantDbConnectionFactory(new TenantDbConnectionInfo($"Data Source={appDbPath}", "Sqlite")),
                mainDb,
                cache);

            var tableRepository = new DynamicTableRepository(mainDb, scopeFactory, appContextAccessor);
            var fieldRepository = new DynamicFieldRepository(mainDb, scopeFactory, appContextAccessor);
            var indexRepository = new DynamicIndexRepository(mainDb, scopeFactory, appContextAccessor);
            var relationRepository = new DynamicRelationRepository(mainDb, scopeFactory, appContextAccessor);
            var fieldPermissionRepository = new FieldPermissionRepository(mainDb, scopeFactory, appContextAccessor);
            var recordRepository = new DynamicRecordRepository(mainDb, scopeFactory);
            var migrationRepository = new DynamicSchemaMigrationRepository(mainDb, scopeFactory, appContextAccessor);
            var idGenerator = new SequentialIdGenerator(11000);
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
                relationRepository,
                fieldPermissionRepository,
                recordRepository,
                migrationRepository,
                idGenerator,
                scopeFactory,
                TimeProvider.System,
                appContextAccessor);

            await service.CreateAsync(
                tenantId,
                userId: 1,
                new DynamicTableCreateRequest(
                    "orders_field_len_repair",
                    "订单-Length列修复",
                    null,
                    "Sqlite",
                    new[]
                    {
                        new DynamicFieldDefinition("id", "主键", "Long", null, null, null, false, true, true, true, null, 0),
                        new DynamicFieldDefinition("orderNo", "订单号", "String", 50, null, null, false, false, false, false, null, 1)
                    },
                    Array.Empty<DynamicIndexDefinition>(),
                    appInstanceId.ToString()),
                CancellationToken.None);

            var repairedDb = await scopeFactory.GetAppClientAsync(tenantId, appInstanceId, CancellationToken.None);
            Assert.False(IsColumnNotNull(repairedDb, "DynamicField", "Length"));
        }
        finally
        {
            cache?.Dispose();
            mainDb?.Dispose();
            appDb?.Dispose();
            CleanupDbFile(mainDbPath);
            CleanupDbFile(appDbPath);
        }
    }

    private static SqlSugarClient CreateDb(string dbPath)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        });
    }

    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"atlas-dynamic-alter-{Guid.NewGuid():N}.db");
    }

    private static Task CreateSchemaAsync(SqlSugarClient db)
    {
        var sql = """
                  CREATE TABLE "DynamicTable"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "TableKey" TEXT NOT NULL,
                    "DisplayName" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "DbType" INTEGER NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "UpdatedBy" INTEGER NOT NULL,
                    "AppId" INTEGER NULL,
                    "ApprovalFlowDefinitionId" INTEGER NULL,
                    "ApprovalStatusField" TEXT NULL,
                    "SchemaVersion" INTEGER NOT NULL DEFAULT 1,
                    "CompatibilityMode" TEXT NULL,
                    "ExtensionPolicy" TEXT NULL
                  );

                  CREATE TABLE "DynamicField"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "TableId" INTEGER NOT NULL,
                    "Name" TEXT NOT NULL,
                    "DisplayName" TEXT NOT NULL,
                    "FieldType" INTEGER NOT NULL,
                    "Length" INTEGER NULL,
                    "Precision" INTEGER NULL,
                    "Scale" INTEGER NULL,
                    "AllowNull" INTEGER NOT NULL,
                    "IsPrimaryKey" INTEGER NOT NULL,
                    "IsAutoIncrement" INTEGER NOT NULL,
                    "IsUnique" INTEGER NOT NULL,
                    "DefaultValue" TEXT NULL,
                    "SortOrder" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "IsComputed" INTEGER NOT NULL DEFAULT 0,
                    "ComputedExprId" INTEGER NULL,
                    "IsStatusField" INTEGER NOT NULL DEFAULT 0,
                    "IsRowVersionField" INTEGER NOT NULL DEFAULT 0
                  );

                  CREATE TABLE "DynamicIndex"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "TableId" INTEGER NOT NULL,
                    "Name" TEXT NOT NULL,
                    "IsUnique" INTEGER NOT NULL,
                    "FieldsJson" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                  );

                  CREATE TABLE "DynamicSchemaMigration"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "TableId" INTEGER NOT NULL,
                    "TableKey" TEXT NOT NULL,
                    "OperationType" TEXT NOT NULL,
                    "AppliedSql" TEXT NOT NULL,
                    "RollbackSql" TEXT NULL,
                    "Status" TEXT NOT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                  );
                  """;

        return db.Ado.ExecuteCommandAsync(sql);
    }

    private static Task ForceLegacyDynamicTableApprovalNotNullAsync(SqlSugarClient db)
    {
        var sql = """
                  ALTER TABLE "DynamicTable" RENAME TO "DynamicTable__old";

                  CREATE TABLE "DynamicTable"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "TableKey" TEXT NOT NULL,
                    "DisplayName" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "DbType" INTEGER NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "CreatedBy" INTEGER NOT NULL,
                    "UpdatedBy" INTEGER NOT NULL,
                    "AppId" INTEGER NULL,
                    "ApprovalFlowDefinitionId" INTEGER NOT NULL DEFAULT 0,
                    "ApprovalStatusField" TEXT NOT NULL DEFAULT '',
                    "SchemaVersion" INTEGER NOT NULL DEFAULT 1,
                    "CompatibilityMode" TEXT NULL,
                    "ExtensionPolicy" TEXT NULL
                  );

                  INSERT INTO "DynamicTable"(
                    "TenantIdValue", "Id", "TableKey", "DisplayName", "Description", "DbType", "Status",
                    "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "AppId",
                    "ApprovalFlowDefinitionId", "ApprovalStatusField",
                    "SchemaVersion", "CompatibilityMode", "ExtensionPolicy")
                  SELECT
                    "TenantIdValue", "Id", "TableKey", "DisplayName", "Description", "DbType", "Status",
                    "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "AppId",
                    COALESCE("ApprovalFlowDefinitionId", 0), COALESCE("ApprovalStatusField", ''),
                    1, NULL, NULL
                  FROM "DynamicTable__old";

                  DROP TABLE "DynamicTable__old";
                  """;
        return db.Ado.ExecuteCommandAsync(sql);
    }

    private static Task ForceLegacyDynamicFieldLengthNotNullAsync(SqlSugarClient db)
    {
        var sql = """
                  ALTER TABLE "DynamicField" RENAME TO "DynamicField__old";

                  CREATE TABLE "DynamicField"(
                    "TenantIdValue" TEXT NOT NULL,
                    "Id" INTEGER PRIMARY KEY,
                    "TableId" INTEGER NOT NULL,
                    "Name" TEXT NOT NULL,
                    "DisplayName" TEXT NOT NULL,
                    "FieldType" INTEGER NOT NULL,
                    "Length" INTEGER NOT NULL DEFAULT 0,
                    "Precision" INTEGER NULL,
                    "Scale" INTEGER NULL,
                    "AllowNull" INTEGER NOT NULL,
                    "IsPrimaryKey" INTEGER NOT NULL,
                    "IsAutoIncrement" INTEGER NOT NULL,
                    "IsUnique" INTEGER NOT NULL,
                    "DefaultValue" TEXT NULL,
                    "SortOrder" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "IsComputed" INTEGER NOT NULL DEFAULT 0,
                    "ComputedExprId" INTEGER NULL,
                    "IsStatusField" INTEGER NOT NULL DEFAULT 0,
                    "IsRowVersionField" INTEGER NOT NULL DEFAULT 0
                  );

                  INSERT INTO "DynamicField"(
                    "TenantIdValue", "Id", "TableId", "Name", "DisplayName", "FieldType",
                    "Length", "Precision", "Scale", "AllowNull", "IsPrimaryKey", "IsAutoIncrement", "IsUnique",
                    "DefaultValue", "SortOrder", "CreatedAt", "UpdatedAt",
                    "IsComputed", "ComputedExprId", "IsStatusField", "IsRowVersionField")
                  SELECT
                    "TenantIdValue", "Id", "TableId", "Name", "DisplayName", "FieldType",
                    COALESCE("Length", 0), "Precision", "Scale", "AllowNull", "IsPrimaryKey", "IsAutoIncrement", "IsUnique",
                    "DefaultValue", "SortOrder", "CreatedAt", "UpdatedAt",
                    0, NULL, 0, 0
                  FROM "DynamicField__old";

                  DROP TABLE "DynamicField__old";
                  """;
        return db.Ado.ExecuteCommandAsync(sql);
    }

    private static bool IsColumnNotNull(ISqlSugarClient db, string tableName, string columnName)
    {
        db.Ado.Open();
        using var cmd = db.Ado.Connection.CreateCommand();
        var escaped = tableName.Replace("\"", "\"\"", StringComparison.Ordinal);
        cmd.CommandText = $"PRAGMA table_info(\"{escaped}\");";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return reader.GetInt64(3) == 1;
        }

        return false;
    }

    private static void CleanupDbFile(string dbPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!File.Exists(dbPath))
            {
                return;
            }

            try
            {
                File.Delete(dbPath);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
        }
    }

    private sealed class SequentialIdGenerator : IIdGeneratorAccessor
    {
        private long _current;

        public SequentialIdGenerator(long seed)
        {
            _current = seed;
        }

        public long NextId()
        {
            _current += 1;
            return _current;
        }
    }

    private sealed class FakeTenantDbConnectionFactory : ITenantDbConnectionFactory
    {
        private readonly TenantDbConnectionInfo _connectionInfo;

        public FakeTenantDbConnectionFactory(TenantDbConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
        }

        public Task<string?> GetConnectionStringAsync(string tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<string?>(_connectionInfo.ConnectionString);
        }

        public Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<TenantDbConnectionInfo?>(_connectionInfo);
        }

        public Task<TenantDbConnectionInfo?> GetConnectionInfoAsync(string tenantId, long tenantAppInstanceId, CancellationToken ct = default)
        {
            return Task.FromResult<TenantDbConnectionInfo?>(_connectionInfo);
        }

        public void InvalidateCache(string tenantId)
        {
        }

        public void InvalidateCache(string tenantId, long? tenantAppInstanceId)
        {
        }
    }

    private sealed class FakeAppContextAccessor : IAppContextAccessor
    {
        private readonly string _appId;

        public FakeAppContextAccessor(string appId = "")
        {
            _appId = appId;
        }

        public IAppContext GetCurrent()
        {
            return new AppContextSnapshot(
                new TenantId(Guid.Empty),
                _appId,
                null,
                new ClientContext(ClientType.Backend, ClientPlatform.Web, ClientChannel.Browser, ClientAgent.Other),
                null);
        }

        public string GetAppId()
        {
            return _appId;
        }

        public IDisposable BeginScope(IAppContext context)
        {
            return new NoopDisposable();
        }

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
