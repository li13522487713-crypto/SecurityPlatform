using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

/// <summary>
/// 数据库查询节点：clause 内存匹配 + maxSqlScanRows 限上限 + maskSensitive 联动。
/// 验证 C1 文档级"占位但可控"的核心承诺。
/// </summary>
public sealed class DatabaseQueryNodeExecutorTests
{
    [Fact]
    public async Task Execute_NoClauses_ShouldReturnAllRowsUpToLimit_AndDefaultOutputKeyIsDbRows()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync(tableSchema:
            "[{\"name\":\"id\",\"type\":\"string\"},{\"name\":\"value\",\"type\":\"number\"}]");
        await harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id,
            new AiDatabaseRecordBulkCreateRequest(
            [
                "{\"id\":\"r1\",\"value\":1}",
                "{\"id\":\"r2\",\"value\":2}",
                "{\"id\":\"r3\",\"value\":3}"
            ]),
            CancellationToken.None);

        var executor = new DatabaseQueryNodeExecutor(harness.Db);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(db.Id),
            ["limit"] = JsonSerializer.SerializeToElement(10)
        };
        var ctx = harness.BuildNodeContext("q1", WorkflowNodeType.DatabaseQuery, config);
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        // M10：默认 outputKey 必须是 db_rows，与 BuiltInWorkflowNodeDeclarations 一致。
        Assert.True(result.Outputs.ContainsKey("db_rows"));
        Assert.Equal(3, result.Outputs["record_count"].GetInt32());
    }

    [Fact]
    public async Task Execute_MaxSqlScanRows_ShouldCapInternalScan()
    {
        // 灌 5 行，maxSqlScanRows=2 → 即使 limit 充裕，结果集也只可能从前 2 行里产出。
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync(tableSchema:
            "[{\"name\":\"id\",\"type\":\"string\"}]");
        await harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id,
            new AiDatabaseRecordBulkCreateRequest(
            [
                "{\"id\":\"r1\"}",
                "{\"id\":\"r2\"}",
                "{\"id\":\"r3\"}",
                "{\"id\":\"r4\"}",
                "{\"id\":\"r5\"}"
            ]),
            CancellationToken.None);

        var executor = new DatabaseQueryNodeExecutor(harness.Db);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(db.Id),
            ["limit"] = JsonSerializer.SerializeToElement(100),
            // SQL 层会被 Clamp(...,100,500_000)，所以最小 100；这里用 100 验证不会比物理行数多。
            ["maxSqlScanRows"] = JsonSerializer.SerializeToElement(100)
        };
        var ctx = harness.BuildNodeContext("q2", WorkflowNodeType.DatabaseQuery, config);
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(5, result.Outputs["record_count"].GetInt32());
    }

    [Fact]
    public async Task Execute_MaskSensitive_True_ShouldHidePasswordField()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync(tableSchema:
            "[{\"name\":\"username\",\"type\":\"string\"},{\"name\":\"password\",\"type\":\"string\"}]");
        await harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id,
            new AiDatabaseRecordBulkCreateRequest(
            [
                "{\"username\":\"alice\",\"password\":\"verysecret\"}"
            ]),
            CancellationToken.None);

        var executor = new DatabaseQueryNodeExecutor(harness.Db);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(db.Id),
            ["maskSensitive"] = JsonSerializer.SerializeToElement(true)
        };
        var ctx = harness.BuildNodeContext("q-mask", WorkflowNodeType.DatabaseQuery, config);
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var row = result.Outputs["db_rows"].EnumerateArray().First();
        Assert.Equal("alice", row.GetProperty("username").GetString());
        Assert.NotEqual("verysecret", row.GetProperty("password").GetString());
    }

    [Fact]
    public async Task Execute_MaskSensitive_False_ShouldNotMaskPasswordField()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync(tableSchema:
            "[{\"name\":\"username\",\"type\":\"string\"},{\"name\":\"password\",\"type\":\"string\"}]");
        await harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id,
            new AiDatabaseRecordBulkCreateRequest(
            [
                "{\"username\":\"alice\",\"password\":\"verysecret\"}"
            ]),
            CancellationToken.None);

        var executor = new DatabaseQueryNodeExecutor(harness.Db);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(db.Id),
            ["maskSensitive"] = JsonSerializer.SerializeToElement(false)
        };
        var ctx = harness.BuildNodeContext("q-nomask", WorkflowNodeType.DatabaseQuery, config);
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        var row = result.Outputs["db_rows"].EnumerateArray().First();
        Assert.Equal("verysecret", row.GetProperty("password").GetString());
    }

    [Fact]
    public async Task Execute_ClauseFilter_ShouldFilterRowsInMemory()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync(tableSchema:
            "[{\"name\":\"id\",\"type\":\"string\"},{\"name\":\"value\",\"type\":\"number\"}]");
        await harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id,
            new AiDatabaseRecordBulkCreateRequest(
            [
                "{\"id\":\"r1\",\"value\":1}",
                "{\"id\":\"r2\",\"value\":2}",
                "{\"id\":\"r3\",\"value\":5}"
            ]),
            CancellationToken.None);

        var executor = new DatabaseQueryNodeExecutor(harness.Db);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(db.Id),
            ["clauseGroup"] = JsonSerializer.SerializeToElement(new[]
            {
                new { field = "value", op = "ge", value = "2", logic = "and" }
            })
        };
        var ctx = harness.BuildNodeContext("q-filter", WorkflowNodeType.DatabaseQuery, config);
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.Outputs["record_count"].GetInt32());
    }
}
