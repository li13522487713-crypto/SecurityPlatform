using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// 系统初始化与迁移控制台 8 张元数据表的领域逻辑测试（M5）。
///
/// 验证：状态机方法、幂等性辅助逻辑、PBKDF2 hash 钩子。
/// 不依赖 SqlSugar / DI；纯领域行为。
/// </summary>
public sealed class SetupConsoleEntitiesTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset Now = new(2026, 4, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SystemSetupState_DefaultsToNotStartedV1()
    {
        var state = new SystemSetupState(TestTenant, id: 1, version: "v1", now: Now);
        Assert.Equal(SystemSetupStates.NotStarted, state.State);
        Assert.Equal("v1", state.Version);
        Assert.False(state.IsRecoveryKeyConfigured());
        Assert.Null(state.FailureMessage);
    }

    [Fact]
    public void SystemSetupState_TransitionTo_UpdatesStateAndTimestamp()
    {
        var state = new SystemSetupState(TestTenant, id: 1, version: "v1", now: Now);
        var later = Now.AddMinutes(5);

        state.TransitionTo(SystemSetupStates.PrecheckPassed, later);

        Assert.Equal(SystemSetupStates.PrecheckPassed, state.State);
        Assert.Equal(later, state.LastUpdatedAt);
        Assert.Null(state.FailureMessage);
    }

    [Fact]
    public void SystemSetupState_TransitionTo_FailedCarriesMessage()
    {
        var state = new SystemSetupState(TestTenant, id: 1, version: "v1", now: Now);
        state.TransitionTo(SystemSetupStates.Failed, Now.AddMinutes(1), "schema build error");

        Assert.Equal(SystemSetupStates.Failed, state.State);
        Assert.Equal("schema build error", state.FailureMessage);
    }

    [Fact]
    public void SystemSetupState_SetRecoveryKeyHash_UpdatesConfiguredFlag()
    {
        var state = new SystemSetupState(TestTenant, id: 1, version: "v1", now: Now);
        Assert.False(state.IsRecoveryKeyConfigured());

        state.SetRecoveryKeyHash("PBKDF2$1000$abc$def", Now.AddMinutes(1));

        Assert.True(state.IsRecoveryKeyConfigured());
    }

    [Fact]
    public void WorkspaceSetupState_TransitionsAndSeedBundleVersion()
    {
        var ws = new WorkspaceSetupState(TestTenant, id: 1, "default", "Default workspace", "v0", Now);
        Assert.Equal(WorkspaceSetupStates.Pending, ws.State);
        Assert.Equal("v0", ws.SeedBundleVersion);

        ws.TransitionTo(WorkspaceSetupStates.Running, Now.AddMinutes(1));
        Assert.Equal(WorkspaceSetupStates.Running, ws.State);

        ws.SetSeedBundleVersion("v1", Now.AddMinutes(2));
        Assert.Equal("v1", ws.SeedBundleVersion);

        ws.TransitionTo(WorkspaceSetupStates.Completed, Now.AddMinutes(3));
        Assert.Equal(WorkspaceSetupStates.Completed, ws.State);
    }

    [Fact]
    public void SetupStepRecord_RestartIncrementsAttemptCount()
    {
        var record = new SetupStepRecord(TestTenant, id: 1, "system", SetupConsoleSteps.Schema, SetupStepStates.Failed, Now);
        Assert.Equal(1, record.AttemptCount);

        record.Restart(Now.AddMinutes(1));
        Assert.Equal(2, record.AttemptCount);
        Assert.Equal(SetupStepStates.Running, record.State);
        Assert.Null(record.EndedAt);
        Assert.Null(record.ErrorMessage);
    }

    [Fact]
    public void SetupStepRecord_MarkSucceededClearsError()
    {
        var record = new SetupStepRecord(TestTenant, id: 1, "system", SetupConsoleSteps.Schema, SetupStepStates.Running, Now);
        record.MarkFailed(Now.AddMinutes(1), "boom");
        Assert.Equal(SetupStepStates.Failed, record.State);
        Assert.Equal("boom", record.ErrorMessage);

        record.MarkSucceeded(Now.AddMinutes(2), payloadJson: "{\"tablesCreated\": 211}");
        Assert.Equal(SetupStepStates.Succeeded, record.State);
        Assert.Null(record.ErrorMessage);
        Assert.Contains("tablesCreated", record.PayloadJson);
    }

    [Fact]
    public void DataMigrationJob_MarkRunningResetsCounters()
    {
        var job = new DataMigrationJob(
            TestTenant,
            id: 100,
            mode: DataMigrationModes.StructurePlusData,
            sourceConnectionString: "Data Source=src.db",
            sourceDbType: "SQLite",
            targetConnectionString: "Server=localhost;",
            targetDbType: "MySql",
            sourceFingerprint: "src-fp",
            targetFingerprint: "tgt-fp",
            moduleScopeJson: "{}",
            createdBy: 1,
            now: Now);
        Assert.Equal(DataMigrationStates.Pending, job.State);

        job.MarkRunning(totalEntities: 211, totalRows: 1000, now: Now.AddMinutes(1));
        Assert.Equal(DataMigrationStates.Running, job.State);
        Assert.Equal(211, job.TotalEntities);
        Assert.Equal(1000, job.TotalRows);
        Assert.Equal(0, job.CompletedEntities);
        Assert.Equal(0, job.ProgressPercent);
    }

    [Fact]
    public void DataMigrationJob_RecordProgressComputesPercentRounded()
    {
        var job = new DataMigrationJob(
            TestTenant,
            id: 100,
            mode: DataMigrationModes.StructurePlusData,
            sourceConnectionString: "x",
            sourceDbType: "SQLite",
            targetConnectionString: "y",
            targetDbType: "SQLite",
            sourceFingerprint: "a",
            targetFingerprint: "b",
            moduleScopeJson: "{}",
            createdBy: 1,
            now: Now);

        job.MarkRunning(100, 10000, Now);
        job.RecordProgress("Tenant", 1, completedEntities: 33, failedEntities: 1, copiedRows: 3300, now: Now);
        Assert.Equal(33m, job.ProgressPercent);
        Assert.Equal("Tenant", job.CurrentEntityName);
        Assert.Equal(1, job.CurrentBatchNo);
    }

    [Fact]
    public void DataMigrationJob_RecordProgressZeroTotalReturnsZero()
    {
        var job = new DataMigrationJob(
            TestTenant,
            id: 100,
            mode: DataMigrationModes.StructurePlusData,
            sourceConnectionString: "x",
            sourceDbType: "SQLite",
            targetConnectionString: "y",
            targetDbType: "SQLite",
            sourceFingerprint: "a",
            targetFingerprint: "b",
            moduleScopeJson: "{}",
            createdBy: 1,
            now: Now);
        job.RecordProgress("X", 0, 0, 0, 0, Now);
        Assert.Equal(0m, job.ProgressPercent);
    }

    [Fact]
    public void DataMigrationCheckpoint_AdvanceTracksLatest()
    {
        var checkpoint = new DataMigrationCheckpoint(TestTenant, id: 1, jobId: 100, entityName: "UserAccount", now: Now);
        checkpoint.Advance(lastBatchNo: 12, lastMaxId: 9999, rowsCopied: 6000, now: Now.AddSeconds(30));
        Assert.Equal(12, checkpoint.LastBatchNo);
        Assert.Equal(9999, checkpoint.LastMaxId);
        Assert.Equal(6000, checkpoint.RowsCopied);
    }

    [Fact]
    public void DataMigrationBatch_MarkSucceededAndFailedAreSeparate()
    {
        var batch = new DataMigrationBatch(TestTenant, id: 1, jobId: 100, entityName: "Tenant", batchNo: 1, now: Now);
        Assert.Equal(DataMigrationStates.Running, batch.State);

        batch.MarkSucceeded(rowsCopied: 1, checksum: "abc", now: Now.AddSeconds(5));
        Assert.Equal("succeeded", batch.State);
        Assert.Equal(1, batch.RowsCopied);
        Assert.Equal("abc", batch.Checksum);

        var failed = new DataMigrationBatch(TestTenant, id: 2, jobId: 100, entityName: "UserAccount", batchNo: 1, now: Now);
        failed.MarkFailed("type mismatch", Now.AddSeconds(10));
        Assert.Equal("failed", failed.State);
        Assert.Equal("type mismatch", failed.ErrorMessage);
    }
}
