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
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(5000);
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
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
    public async Task AlterAsync_ShouldRejectUpdateOrRemoveFields()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            var db = CreateDb(dbPath);
            await CreateSchemaAsync(db);

            var tableRepository = new DynamicTableRepository(db);
            var fieldRepository = new DynamicFieldRepository(db);
            var indexRepository = new DynamicIndexRepository(db);
            var recordRepository = new DynamicRecordRepository(db);
            var migrationRepository = new DynamicSchemaMigrationRepository(db);
            var idGenerator = new SequentialIdGenerator(6000);
            var service = new DynamicTableCommandService(
                tableRepository,
                fieldRepository,
                indexRepository,
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
                        new DynamicFieldUpdateDefinition("orderNo", "订单号-更新", null, null, null, null, null, null, null)
                    },
                    Array.Empty<string>()),
                CancellationToken.None));
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
