using System.Text.Json;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

public sealed class DataMigrationRunner : IDataMigrationRunner
{
    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly MigrationSecretProtector _secretProtector;
    private readonly IMigrationConnectionResolver _resolver;
    private readonly IDataMigrationPlanner _planner;
    private readonly IMigrationBulkWriter _bulkWriter;
    private readonly IDataMigrationOrmService? _migrationService;
    private readonly ILogger<DataMigrationRunner> _logger;

    public DataMigrationRunner(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        MigrationSecretProtector secretProtector,
        IMigrationConnectionResolver resolver,
        IDataMigrationPlanner planner,
        IMigrationBulkWriter bulkWriter,
        ILogger<DataMigrationRunner> logger,
        IDataMigrationOrmService? migrationService = null)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
        _secretProtector = secretProtector;
        _resolver = resolver;
        _planner = planner;
        _bulkWriter = bulkWriter;
        _migrationService = migrationService;
        _logger = logger;
    }

    public async Task RunAsync(long jobId, CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        var sourceConfig = DeserializeConfig(job.SourceConfigJson, job.SourceDbType, job.SourceConnectionString);
        var targetConfig = DeserializeConfig(job.TargetConfigJson, job.TargetDbType, job.TargetConnectionString);
        var source = await _resolver.ResolveAsync(sourceConfig, cancellationToken).ConfigureAwait(false);
        var target = await _resolver.ResolveAsync(targetConfig, cancellationToken).ConfigureAwait(false);
        var plan = await _planner.PlanAsync(job, source, target, cancellationToken).ConfigureAwait(false);

        job.MarkRunning(plan.TotalEntities, plan.TotalRows, DateTimeOffset.UtcNow);
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Runner", $"job queued with {plan.Items.Count} tables", null, cancellationToken)
            .ConfigureAwait(false);
        if (job.MigrateFiles)
        {
            await AppendLogAsync(
                    job.Id,
                    "warn",
                    "Runner",
                    "file migration is not implemented in this release and was skipped",
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var completed = 0;
        var failed = 0;
        long copiedRows = 0;

        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var freshJob = await LoadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (freshJob.State == DataMigrationStates.Cancelling)
            {
                await CancelJobAsync(freshJob, cancellationToken).ConfigureAwait(false);
                return;
            }

            var tableProgress = await LoadTableProgressAsync(jobId, item.TableName, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"table progress missing for {item.TableName}");
            try
            {
                tableProgress.MarkRunning(DateTimeOffset.UtcNow);
                await UpdateTableProgressAsync(tableProgress, cancellationToken).ConfigureAwait(false);

                var rowsBefore = await _bulkWriter
                    .PrepareTargetAsync(item, source, target, job.WriteMode, job.CreateSchema, cancellationToken)
                    .ConfigureAwait(false);
                tableProgress.UpdateTargetRowsAfter(rowsBefore, DateTimeOffset.UtcNow);
                await UpdateTableProgressAsync(tableProgress, cancellationToken).ConfigureAwait(false);

                var batchNo = 0;
                var lastMaxId = await LoadCheckpointLastMaxIdAsync(job.Id, item.TableName, cancellationToken).ConfigureAwait(false);
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    freshJob = await LoadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
                    if (freshJob.State == DataMigrationStates.Cancelling)
                    {
                        var cancelledTargetRows = await _bulkWriter.CountTargetRowsAsync(item, target, cancellationToken).ConfigureAwait(false);
                        tableProgress.MarkCancelled(cancelledTargetRows, DateTimeOffset.UtcNow);
                        await UpdateTableProgressAsync(tableProgress, cancellationToken).ConfigureAwait(false);
                        await CancelJobAsync(freshJob, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    var writeResult = await _bulkWriter
                        .WriteNextBatchAsync(item, source, target, job.WriteMode, job.BatchSize, lastMaxId, cancellationToken)
                        .ConfigureAwait(false);
                    if (!writeResult.HasRows)
                    {
                        break;
                    }

                    batchNo += 1;
                    lastMaxId = writeResult.LastMaxId;
                    copiedRows += writeResult.BatchNoRows;

                    tableProgress.RecordBatch(batchNo, tableProgress.CopiedRows + writeResult.BatchNoRows, lastMaxId.ToString(), DateTimeOffset.UtcNow);
                    await UpdateTableProgressAsync(tableProgress, cancellationToken).ConfigureAwait(false);
                    await UpsertCheckpointAsync(job.Id, item.TableName, batchNo, lastMaxId, tableProgress.CopiedRows, cancellationToken)
                        .ConfigureAwait(false);

                    var batch = new DataMigrationBatch(
                        _tenantProvider.GetTenantId(),
                        _idGen.NextId(),
                        job.Id,
                        item.EntityName,
                        item.TableName,
                        batchNo,
                        DateTimeOffset.UtcNow);
                    batch.MarkSucceeded(writeResult.BatchNoRows, checksum: null, now: DateTimeOffset.UtcNow);
                    await _db.Insertable(batch).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(writeResult.WarningMessage))
                    {
                        await AppendLogAsync(job.Id, "warn", "BulkWriter", writeResult.WarningMessage, item.TableName, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    var progressPercent = job.TotalRows <= 0
                        ? (job.TotalEntities <= 0 ? 0m : Math.Round((completed * 100m) / job.TotalEntities, 2, MidpointRounding.AwayFromZero))
                        : Math.Round(copiedRows * 100m / job.TotalRows, 2, MidpointRounding.AwayFromZero);
                    freshJob.RecordProgress(item.EntityName, item.TableName, batchNo, completed, failed, copiedRows, progressPercent, DateTimeOffset.UtcNow);
                    await UpdateJobAsync(freshJob, cancellationToken).ConfigureAwait(false);
                }

                var targetRowsAfter = await _bulkWriter.CountTargetRowsAsync(item, target, cancellationToken).ConfigureAwait(false);
                tableProgress.MarkSucceeded(targetRowsAfter, DateTimeOffset.UtcNow);
                await UpdateTableProgressAsync(tableProgress, cancellationToken).ConfigureAwait(false);
                completed += 1;
                freshJob = await LoadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
                var finishedProgress = freshJob.TotalRows <= 0
                    ? (freshJob.TotalEntities <= 0 ? 100m : Math.Round((completed * 100m) / freshJob.TotalEntities, 2, MidpointRounding.AwayFromZero))
                    : Math.Round(copiedRows * 100m / freshJob.TotalRows, 2, MidpointRounding.AwayFromZero);
                freshJob.RecordProgress(item.EntityName, item.TableName, tableProgress.CurrentBatchNo, completed, failed, copiedRows, finishedProgress, DateTimeOffset.UtcNow);
                await UpdateJobAsync(freshJob, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failed += 1;
                tableProgress.MarkFailed(ex.Message, tableProgress.FailedRows + 1, DateTimeOffset.UtcNow);
                await UpdateTableProgressAsync(tableProgress, cancellationToken).ConfigureAwait(false);
                var failBatch = new DataMigrationBatch(
                    _tenantProvider.GetTenantId(),
                    _idGen.NextId(),
                    job.Id,
                    item.EntityName,
                    item.TableName,
                    tableProgress.CurrentBatchNo + 1,
                    DateTimeOffset.UtcNow);
                failBatch.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
                await _db.Insertable(failBatch).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                await AppendLogAsync(job.Id, "error", "Runner", $"{item.TableName}: {ex.Message}", item.TableName, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogError(ex, "Migration table {TableName} failed in job {JobId}", item.TableName, jobId);
            }
        }

        job = await LoadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (failed > 0)
        {
            job.TransitionTo(DataMigrationStates.Failed, DateTimeOffset.UtcNow, $"{failed} table(s) failed");
            job.MarkFinished(DataMigrationStates.Failed, DateTimeOffset.UtcNow);
        }
        else
        {
            job.MarkFinished(DataMigrationStates.Succeeded, DateTimeOffset.UtcNow);
        }

        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Runner", $"job finished state={job.State}", null, cancellationToken)
            .ConfigureAwait(false);

        if (failed == 0 && job.ValidateAfterCopy && _migrationService is not null)
        {
            await AppendLogAsync(job.Id, "info", "Runner", "auto validation started", null, cancellationToken)
                .ConfigureAwait(false);
            await _migrationService.ValidateJobAsync(job.Id.ToString(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CancelJobAsync(DataMigrationJob job, CancellationToken cancellationToken)
    {
        job.MarkFinished(DataMigrationStates.Cancelled, DateTimeOffset.UtcNow);
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "warn", "Runner", "job cancelled by user", null, cancellationToken).ConfigureAwait(false);
    }

    private DbConnectionConfig DeserializeConfig(string json, string dbType, string encryptedConnectionString)
    {
        var config = string.IsNullOrWhiteSpace(json)
            ? new DbConnectionConfig(dbType, dbType, DataMigrationConnectionModes.ConnectionString, null, null, dbType)
            : JsonSerializer.Deserialize<DbConnectionConfig>(json)
              ?? new DbConnectionConfig(dbType, dbType, DataMigrationConnectionModes.ConnectionString, null, null, dbType);
        return config with
        {
            DriverCode = string.IsNullOrWhiteSpace(config.DriverCode) ? dbType : config.DriverCode,
            DbType = string.IsNullOrWhiteSpace(config.DbType) ? dbType : config.DbType,
            ConnectionString = string.IsNullOrWhiteSpace(config.ConnectionString)
                ? _secretProtector.Decrypt(encryptedConnectionString)
                : config.ConnectionString
        };
    }

    private async Task<DataMigrationJob> LoadJobAsync(long jobId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId().Value;
        var job = await _db.Queryable<DataMigrationJob>()
            .Where(x => x.TenantIdValue == tenantId && x.Id == jobId)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        return job ?? throw new InvalidOperationException($"migration job {jobId} not found");
    }

    private async Task<DataMigrationTableProgress?> LoadTableProgressAsync(
        long jobId,
        string tableName,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId().Value;
        return await _db.Queryable<DataMigrationTableProgress>()
            .Where(x => x.TenantIdValue == tenantId && x.JobId == jobId && x.TableName == tableName)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<long> LoadCheckpointLastMaxIdAsync(long jobId, string entityName, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId().Value;
        var checkpoint = await _db.Queryable<DataMigrationCheckpoint>()
            .Where(x => x.TenantIdValue == tenantId && x.JobId == jobId && x.EntityName == entityName)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        return checkpoint?.LastMaxId ?? 0;
    }

    private async Task UpsertCheckpointAsync(
        long jobId,
        string entityName,
        int batchNo,
        long lastMaxId,
        long copiedRows,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId().Value;
        var checkpoint = await _db.Queryable<DataMigrationCheckpoint>()
            .Where(x => x.TenantIdValue == tenantId && x.JobId == jobId && x.EntityName == entityName)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        if (checkpoint is null)
        {
            checkpoint = new DataMigrationCheckpoint(
                _tenantProvider.GetTenantId(),
                _idGen.NextId(),
                jobId,
                entityName,
                DateTimeOffset.UtcNow);
            checkpoint.Advance(batchNo, lastMaxId, copiedRows, DateTimeOffset.UtcNow);
            await _db.Insertable(checkpoint).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        checkpoint.Advance(batchNo, lastMaxId, copiedRows, DateTimeOffset.UtcNow);
        await _db.Updateable(checkpoint)
            .Where(row => row.TenantIdValue == checkpoint.TenantIdValue && row.Id == checkpoint.Id)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<int> UpdateJobAsync(DataMigrationJob job, CancellationToken cancellationToken)
        => _db.Updateable(job)
            .Where(row => row.TenantIdValue == job.TenantIdValue && row.Id == job.Id)
            .ExecuteCommandAsync(cancellationToken);

    private Task<int> UpdateTableProgressAsync(DataMigrationTableProgress tableProgress, CancellationToken cancellationToken)
        => _db.Updateable(tableProgress)
            .Where(row => row.TenantIdValue == tableProgress.TenantIdValue && row.Id == tableProgress.Id)
            .ExecuteCommandAsync(cancellationToken);

    private async Task AppendLogAsync(
        long jobId,
        string level,
        string module,
        string message,
        string? entityName,
        CancellationToken cancellationToken)
    {
        var log = new DataMigrationLog(
            _tenantProvider.GetTenantId(),
            _idGen.NextId(),
            jobId,
            level,
            module,
            message,
            entityName,
            DateTimeOffset.UtcNow);
        await _db.Insertable(log).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
}
