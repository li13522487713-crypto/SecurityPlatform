using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

/// <summary>
/// D5：AiDatabaseService 批量同步 / 异步 / 后台 Inline 任务的服务级覆盖。
/// 用临时 SQLite + 真实仓储跑通：行级 coerce、行级失败保留明细、配额硬失败、后台任务静默失败防护。
/// </summary>
public sealed class AiDatabaseServiceBulkTests
{
    [Fact]
    public async Task CreateRecordsBulkAsync_HappyPath_ShouldInsertAllRows_AndPropagateRowOwnerChannel()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync();

        var request = new AiDatabaseRecordBulkCreateRequest(
        [
            "{\"orderId\":\"NO-1\",\"amount\":1.0}",
            "{\"orderId\":\"NO-2\",\"amount\":2.5}"
        ]);

        var result = await harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant,
            db.Id,
            request,
            CancellationToken.None,
            ownerUserId: 7,
            creatorUserId: 7,
            channelId: "bot-channel");

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.All(result.Rows, r => Assert.True(r.Success));

        var rows = await harness.RecordRepository.GetPagedByDatabaseAsync(
            AiDatabaseTestHarness.Tenant, db.Id, 1, 50, CancellationToken.None);
        Assert.Equal(2, rows.Items.Count);
        Assert.All(rows.Items, r =>
        {
            Assert.Equal(7, r.OwnerUserId);
            Assert.Equal("bot-channel", r.ChannelId);
        });
    }

    [Fact]
    public async Task CreateRecordsBulkAsync_RowFailure_ShouldKeepRowDetail_AndStillInsertOthers()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync();

        // 第二行 amount 是非数字字符串 → AiDatabaseValueCoercer 抛 BusinessException → 行级 failure。
        var request = new AiDatabaseRecordBulkCreateRequest(
        [
            "{\"orderId\":\"OK-1\",\"amount\":1}",
            "{\"orderId\":\"BAD-2\",\"amount\":\"not-a-number\"}",
            "{\"orderId\":\"OK-3\",\"amount\":3}"
        ]);

        var result = await harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id, request, CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Failed);
        var failedRow = result.Rows.Single(r => !r.Success);
        Assert.Equal(1, failedRow.Index);
        Assert.False(string.IsNullOrWhiteSpace(failedRow.ErrorMessage));

        var rows = await harness.RecordRepository.GetPagedByDatabaseAsync(
            AiDatabaseTestHarness.Tenant, db.Id, 1, 50, CancellationToken.None);
        Assert.Equal(2, rows.Items.Count);
    }

    [Fact]
    public async Task CreateRecordsBulkAsync_OverSyncBulkLimit_ShouldThrow_WhenEnforced()
    {
        using var harness = new AiDatabaseTestHarness(new AiDatabaseQuotaOptions { MaxBulkInsertRows = 2 });
        var db = await harness.SeedDatabaseAsync();

        var request = new AiDatabaseRecordBulkCreateRequest(
        [
            "{\"orderId\":\"A\",\"amount\":1}",
            "{\"orderId\":\"B\",\"amount\":2}",
            "{\"orderId\":\"C\",\"amount\":3}"
        ]);

        await Assert.ThrowsAsync<BusinessException>(() => harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateRecordsBulkAsync_OverSyncBulkLimit_ShouldPass_WhenSyncLimitDisabled()
    {
        // 后台异步任务路径：enforceSyncBulkRowLimit=false 时不再被同步上限拒绝（仍受表行数硬上限保护）。
        using var harness = new AiDatabaseTestHarness(new AiDatabaseQuotaOptions
        {
            MaxBulkInsertRows = 1,
            MaxRowsPerTable = 100
        });
        var db = await harness.SeedDatabaseAsync();

        var request = new AiDatabaseRecordBulkCreateRequest(
        [
            "{\"orderId\":\"A\",\"amount\":1}",
            "{\"orderId\":\"B\",\"amount\":2}"
        ]);

        var result = await harness.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id, request, CancellationToken.None,
            enforceSyncBulkRowLimit: false);
        Assert.Equal(2, result.Succeeded);
    }

    [Fact]
    public async Task SubmitBulkInsertJobAsync_EmptyRows_ShouldThrow()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync();
        var request = new AiDatabaseRecordBulkCreateRequest([]);

        await Assert.ThrowsAsync<BusinessException>(() => harness.Service.SubmitBulkInsertJobAsync(
            AiDatabaseTestHarness.Tenant, db.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitBulkInsertJobAsync_HappyPath_ShouldEnqueueAndPersistTask()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync();
        var request = new AiDatabaseRecordBulkCreateRequest(
        [
            "{\"orderId\":\"NO-9\",\"amount\":9}"
        ]);

        var accepted = await harness.Service.SubmitBulkInsertJobAsync(
            AiDatabaseTestHarness.Tenant, db.Id, request, CancellationToken.None,
            ownerUserId: 12,
            channelId: "agent-1");

        Assert.True(accepted.TaskId > 0);
        Assert.Equal(1, accepted.RowCount);

        var task = await harness.ImportTaskRepository.GetLatestAsync(
            AiDatabaseTestHarness.Tenant, db.Id, CancellationToken.None);
        Assert.NotNull(task);
        Assert.Equal(AiDatabaseImportSource.Inline, task!.Source);
        Assert.Equal(AiDatabaseImportStatus.Pending, task.Status);
        Assert.Equal(12, task.OwnerUserId);
        Assert.Equal("agent-1", task.ChannelId);
        Assert.Single(harness.BackgroundWorkQueue.Items);
    }

    [Fact]
    public async Task ProcessInlineBulkAsync_HappyPath_ShouldMarkCompletedAndInsertRows()
    {
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync();

        var accepted = await harness.Service.SubmitBulkInsertJobAsync(
            AiDatabaseTestHarness.Tenant,
            db.Id,
            new AiDatabaseRecordBulkCreateRequest(
            [
                "{\"orderId\":\"OK-1\",\"amount\":1}",
                "{\"orderId\":\"OK-2\",\"amount\":2}"
            ]),
            CancellationToken.None,
            ownerUserId: 5,
            channelId: "ch-1");

        // 主动驱动后台工作项：模拟 BackgroundWorkQueueProcessor 创建独立 scope 的逻辑。
        await harness.BackgroundWorkQueue.DrainLastAsync(harness.ServiceProvider);

        var task = await harness.ImportTaskRepository.FindByIdAsync(
            AiDatabaseTestHarness.Tenant, accepted.TaskId, CancellationToken.None);
        Assert.NotNull(task);
        Assert.Equal(AiDatabaseImportStatus.Completed, task!.Status);
        Assert.Equal(2, task.TotalRows);
        Assert.Equal(2, task.SucceededRows);
        Assert.Equal(0, task.FailedRows);

        var rows = await harness.RecordRepository.GetPagedByDatabaseAsync(
            AiDatabaseTestHarness.Tenant, db.Id, 1, 50, CancellationToken.None);
        Assert.Equal(2, rows.Items.Count);
        Assert.All(rows.Items, r =>
        {
            Assert.Equal(5, r.OwnerUserId);
            Assert.Equal("ch-1", r.ChannelId);
        });
    }

    [Fact]
    public async Task ProcessInlineBulkAsync_ShouldMarkFailed_WhenPayloadJsonIsCorrupted()
    {
        // 验证 C3：payload 解析失败要走 MarkFailed，而不是静默 return。
        using var harness = new AiDatabaseTestHarness();
        var db = await harness.SeedDatabaseAsync();

        // 1) 先合法 Submit 一次：写入 task + 入队后台 work item（taskId 在 work item 闭包里）。
        var accepted = await harness.Service.SubmitBulkInsertJobAsync(
            AiDatabaseTestHarness.Tenant,
            db.Id,
            new AiDatabaseRecordBulkCreateRequest(["{\"orderId\":\"X\",\"amount\":0}"]),
            CancellationToken.None);

        // 2) 通过反射把 PayloadJson 私有 setter 改为非法 JSON，模拟"任务持久化后被破坏"的边界。
        var task = await harness.ImportTaskRepository.FindByIdAsync(
            AiDatabaseTestHarness.Tenant, accepted.TaskId, CancellationToken.None);
        Assert.NotNull(task);
        var payloadProp = typeof(AiDatabaseImportTask).GetProperty(nameof(AiDatabaseImportTask.PayloadJson))!;
        payloadProp.GetSetMethod(nonPublic: true)!.Invoke(task, new object?[] { "this-is-not-json" });
        await harness.ImportTaskRepository.UpdateAsync(task!, CancellationToken.None);

        // 3) 驱动 work item：进入 ProcessInlineBulkAsync，应在 JSON 解析失败时 MarkFailed。
        await harness.BackgroundWorkQueue.DrainLastAsync(harness.ServiceProvider);

        var refreshed = await harness.ImportTaskRepository.FindByIdAsync(
            AiDatabaseTestHarness.Tenant, accepted.TaskId, CancellationToken.None);
        Assert.NotNull(refreshed);
        Assert.Equal(AiDatabaseImportStatus.Failed, refreshed!.Status);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.ErrorMessage));
        // 关键安全断言：错误信息走 AiDatabasePublicErrors 脱敏文案，不暴露 JsonException 堆栈。
        Assert.DoesNotContain("System.Text.Json", refreshed.ErrorMessage);
        Assert.DoesNotContain("at Atlas.", refreshed.ErrorMessage);
    }
}
