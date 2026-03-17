using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using Atlas.Infrastructure.Repositories;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Repositories;

public sealed class DynamicRecordRepositoryTenantIsolationTests
{
    [Fact]
    public async Task GetByIdAsync_ShouldNotReturnCrossTenantRecord()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateDynamicTableAsync(db, "asset_records");

            var repository = new DynamicRecordRepository(db);
            var tenantA = new TenantId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
            var tableA = CreateDynamicTableEntity(tenantA, "asset_records", 1001);
            var fieldsA = CreateFields(tenantA, tableA.Id);

            var id = await repository.InsertAsync(
                tenantA,
                tableA,
                fieldsA,
                new DynamicRecordUpsertRequest(new[]
                {
                    new DynamicFieldValueDto { Field = "Name", ValueType = "String", StringValue = "tenant-a-record" }
                }),
                CancellationToken.None);

            var sameTenantRecord = await repository.GetByIdAsync(tenantA, tableA, fieldsA, id, CancellationToken.None);
            var crossTenantRecord = await repository.GetByIdAsync(tenantB, tableA, fieldsA, id, CancellationToken.None);

            Assert.NotNull(sameTenantRecord);
            Assert.Null(crossTenantRecord);
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    [Fact]
    public async Task QueryAndDelete_ShouldRespectTenantBoundary()
    {
        var dbPath = CreateTempDbPath();
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            await CreateDynamicTableAsync(db, "asset_records");

            var repository = new DynamicRecordRepository(db);
            var tenantA = new TenantId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
            var tenantB = new TenantId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
            var tableA = CreateDynamicTableEntity(tenantA, "asset_records", 2001);
            var tableB = CreateDynamicTableEntity(tenantB, "asset_records", 2002);
            var fields = CreateFields(tenantA, tableA.Id);

            var tenantARecordId = await repository.InsertAsync(
                tenantA,
                tableA,
                fields,
                new DynamicRecordUpsertRequest(new[]
                {
                    new DynamicFieldValueDto { Field = "Name", ValueType = "String", StringValue = "record-a" }
                }),
                CancellationToken.None);
            await repository.InsertAsync(
                tenantB,
                tableB,
                fields,
                new DynamicRecordUpsertRequest(new[]
                {
                    new DynamicFieldValueDto { Field = "Name", ValueType = "String", StringValue = "record-b" }
                }),
                CancellationToken.None);

            var tenantAQuery = await repository.QueryAsync(
                tenantA,
                tableA,
                fields,
                new DynamicRecordQueryRequest(1, 20, null, null, false, Array.Empty<DynamicFilterCondition>()),
                CancellationToken.None);

            Assert.Equal(1, tenantAQuery.Total);

            await repository.DeleteAsync(tenantB, tableB, fields, tenantARecordId, CancellationToken.None);
            var recordAfterCrossTenantDelete = await repository.GetByIdAsync(
                tenantA,
                tableA,
                fields,
                tenantARecordId,
                CancellationToken.None);
            Assert.NotNull(recordAfterCrossTenantDelete);
        }
        finally
        {
            db?.Dispose();
            CleanupDbFile(dbPath);
        }
    }

    private static SqlSugarClient CreateDb(string dbPath)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });
    }

    private static Task CreateDynamicTableAsync(SqlSugarClient db, string tableName)
    {
        var quotedTable = $"\"{tableName}\"";
        var sql =
            $"CREATE TABLE {quotedTable} (" +
            "\"TenantIdValue\" TEXT NOT NULL, " +
            "\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "\"Name\" TEXT NOT NULL" +
            ");";
        return db.Ado.ExecuteCommandAsync(sql);
    }

    private static DynamicTable CreateDynamicTableEntity(TenantId tenantId, string tableKey, long id)
    {
        return new DynamicTable(
            tenantId,
            tableKey,
            "资产记录",
            null,
            DynamicDbType.Sqlite,
            createdBy: 1,
            id,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<DynamicField> CreateFields(TenantId tenantId, long tableId)
    {
        var now = DateTimeOffset.UtcNow;
        return new[]
        {
            new DynamicField(
                tenantId,
                tableId,
                "Id",
                "主键",
                DynamicFieldType.Long,
                length: null,
                precision: null,
                scale: null,
                allowNull: false,
                isPrimaryKey: true,
                isAutoIncrement: true,
                isUnique: true,
                defaultValue: null,
                sortOrder: 1,
                id: 1,
                now),
            new DynamicField(
                tenantId,
                tableId,
                "Name",
                "名称",
                DynamicFieldType.String,
                length: 128,
                precision: null,
                scale: null,
                allowNull: false,
                isPrimaryKey: false,
                isAutoIncrement: false,
                isUnique: false,
                defaultValue: null,
                sortOrder: 2,
                id: 2,
                now)
        };
    }

    private static string CreateTempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"atlas-dynamic-record-{Guid.NewGuid():N}.db");
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
}
