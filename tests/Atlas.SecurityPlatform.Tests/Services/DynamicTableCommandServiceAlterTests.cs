using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class DynamicTableCommandServiceAlterTests
{
    [Fact]
    public async Task AlterAsync_ShouldAddField_AndSupportRecordWrite()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            var db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(5000);
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
                TimeProvider.System);

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
                    Array.Empty<DynamicIndexDefinition>()),
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

            var table = await tableRepository.FindByKeyAsync(tenantId, "orders_alter", CancellationToken.None);
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
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task AlterAsync_ShouldRejectUnsupportedFieldStructuralUpdate()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            var db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(6000);
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
                TimeProvider.System);

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
                    Array.Empty<DynamicIndexDefinition>()),
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
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task AlterAsync_ShouldUpdateFieldDisplayNameAndSortOrder()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            var db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(6500);
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
                TimeProvider.System);

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
                    Array.Empty<DynamicIndexDefinition>()),
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

            var table = await tableRepository.FindByKeyAsync(tenantId, "orders_alter_update_meta", CancellationToken.None);
            Assert.NotNull(table);
            var fields = await fieldRepository.ListByTableIdAsync(tenantId, table!.Id, CancellationToken.None);
            var orderNo = fields.Single(x => x.Name == "orderNo");
            Assert.Equal("订单编号", orderNo.DisplayName);
            Assert.Equal(9, orderNo.SortOrder);
        }
        finally
        {
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task PreviewAlterAsync_ShouldReturnSql_WithoutChangingSchema()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            var db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var relationRepository = new DynamicRelationRepository(db);
            var fieldPermissionRepository = new FieldPermissionRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(7000);
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
                TimeProvider.System);

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
                    Array.Empty<DynamicIndexDefinition>()),
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
            Assert.Contains(preview.SqlScripts, script => script.Contains("ALTER TABLE", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(preview.SqlScripts, script => script.Contains("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase));

            var table = await tableRepository.FindByKeyAsync(tenantId, "orders_preview", CancellationToken.None);
            Assert.NotNull(table);
            var fields = await fieldRepository.ListByTableIdAsync(tenantId, table!.Id, CancellationToken.None);
            Assert.DoesNotContain(fields, x => x.Name.Equals("remark_preview", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupDbFile(dbPath);
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
                    "ApprovalFlowDefinitionId" INTEGER NULL,
                    "ApprovalStatusField" TEXT NULL
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
                    "UpdatedAt" TEXT NOT NULL
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

    private static void CleanupDbFile(string dbPath)
    {
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
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
}
