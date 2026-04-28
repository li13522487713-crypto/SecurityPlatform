using System.Text.Json;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

/// <summary>
/// D9：DatabaseInsertNodeExecutor 上下文注入。
/// SingleUser/Channel 模式下 owner/channel 必须从 context 注入到 record 行级元数据。
/// </summary>
public sealed class DatabaseInsertNodeExecutorTests
{
    [Fact]
    public async Task Execute_SingleUserChannel_ShouldInjectOwnerAndChannelFromContext()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync(
            queryMode: AiDatabaseQueryMode.SingleUser,
            channelScope: AiDatabaseChannelScope.ChannelIsolated);

        var executor = new DatabaseInsertNodeExecutor(harness.Db, harness.IdGenerator);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(db.Id),
            ["rows"] = JsonSerializer.SerializeToElement(new[]
            {
                new Dictionary<string, object?> { ["orderId"] = "NO-1", ["amount"] = 1.5 }
            }),
            ["injectUserContext"] = JsonSerializer.SerializeToElement(true)
        };
        var ctx = harness.BuildNodeContext("insert-1", WorkflowNodeType.DatabaseInsert, config,
            userId: 42, channelId: "agent-x");

        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1, result.Outputs["affected_rows"].GetInt32());

        var rows = await harness.Service.GetRecordsAsync(
            AiDatabaseTestHarness.Tenant,
            db.Id,
            1,
            50,
            AiDatabaseRecordEnvironment.Draft,
            CancellationToken.None,
            ownerUserId: 42,
            channelId: "agent-x");
        var row = Assert.Single(rows.Items);
        Assert.Equal(42, row.OwnerUserId);
        Assert.Equal("agent-x", row.ChannelId);
    }

    [Fact]
    public async Task Execute_InjectUserContextDisabled_ShouldNotPersistOwnerOrChannel()
    {
        using var harness = new AiDatabaseTestHarness();
        // MultiUser + Open（默认）下，injectUserContext=false 时 owner/channel 应保留为 NULL。
        var db = await harness.SeedDatabaseAsync();

        var executor = new DatabaseInsertNodeExecutor(harness.Db, harness.IdGenerator);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(db.Id),
            ["rows"] = JsonSerializer.SerializeToElement(new[]
            {
                new Dictionary<string, object?> { ["orderId"] = "NO-2", ["amount"] = 2 }
            }),
            ["injectUserContext"] = JsonSerializer.SerializeToElement(false)
        };
        var ctx = harness.BuildNodeContext("insert-2", WorkflowNodeType.DatabaseInsert, config,
            userId: 99, channelId: "ch-99");

        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);

        var rows = await harness.Service.GetRecordsAsync(
            AiDatabaseTestHarness.Tenant,
            db.Id,
            1,
            50,
            AiDatabaseRecordEnvironment.Draft,
            CancellationToken.None);
        var row = Assert.Single(rows.Items);
        Assert.Null(row.OwnerUserId);
        Assert.True(string.IsNullOrEmpty(row.ChannelId));
    }

    [Fact]
    public async Task Execute_MissingDatabaseInfoId_ShouldFailFast()
    {
        using var harness = new AiDatabaseTestHarness();
        var executor = new DatabaseInsertNodeExecutor(harness.Db, harness.IdGenerator);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["rows"] = JsonSerializer.SerializeToElement(new[]
            {
                new Dictionary<string, object?> { ["orderId"] = "X" }
            })
        };
        var ctx = harness.BuildNodeContext("insert-bad", WorkflowNodeType.DatabaseInsert, config);
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("databaseInfoId", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task Execute_EmptyRows_ShouldShortCircuitToZeroRows()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync();
        var executor = new DatabaseInsertNodeExecutor(harness.Db, harness.IdGenerator);
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(db.Id),
            ["rows"] = JsonSerializer.SerializeToElement(Array.Empty<object>())
        };
        var ctx = harness.BuildNodeContext("insert-empty", WorkflowNodeType.DatabaseInsert, config);
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(0, result.Outputs["affected_rows"].GetInt32());
    }
}
