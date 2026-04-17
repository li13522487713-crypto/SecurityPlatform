using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// ORM ????????????M6???
///
/// ??????
/// - ?? / ???? new <see cref="SqlSugarClient"/> ???????????? ScopedClient??
/// - ??reflection ?? <see cref="AtlasOrmSchemaCatalog.RuntimeEntities"/>????????Queryable&lt;TEntity&gt;.ToList ??Insertable(rows).ExecuteCommand??
/// - ????? <see cref="DataMigrationStates"/>??
/// - ??????????/ ??????+ DbType ?? SHA256??
/// - ?????M6?????? + ????????ORM ?????M7 ??"?????? + ????appsettings.runtime.json"??
/// </summary>
public sealed class OrmDataMigrationService : IDataMigrationOrmService
{
    private const int DefaultBatchSize = 500;

    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly MigrationSecretProtector _secretProtector;
    private readonly RuntimeConfigPersistor _runtimeConfigPersistor;
    private readonly ILogger<OrmDataMigrationService> _logger;

    public OrmDataMigrationService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        MigrationSecretProtector secretProtector,
        RuntimeConfigPersistor runtimeConfigPersistor,
        ILogger<OrmDataMigrationService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
        _secretProtector = secretProtector;
        _runtimeConfigPersistor = runtimeConfigPersistor;
        _logger = logger;
    }

    public async Task<MigrationTestConnectionResponse> TestConnectionAsync(
        DbConnectionConfig connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            using var scope = OpenScope(connection);
            var ok = await scope.Ado.GetScalarAsync("SELECT 1") is not null;
            var tableCount = ok ? scope.DbMaintenance.GetTableInfoList(false).Count : 0;
            return new MigrationTestConnectionResponse(
                Connected: ok,
                Message: ok ? "connection successful" : "connection failed",
                DetectedDbType: connection.DbType,
                DetectedTableCount: tableCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrmMigration] TestConnection failed for {DbType}", connection.DbType);
            return new MigrationTestConnectionResponse(
                Connected: false,
                Message: ex.Message,
                DetectedDbType: connection.DbType,
                DetectedTableCount: 0);
        }
    }

    public async Task<DataMigrationJobDto> CreateJobAsync(
        DataMigrationJobCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceFingerprint = ComputeFingerprint(request.Source);
        var targetFingerprint = ComputeFingerprint(request.Target);
        var tenantValue = _tenantProvider.GetTenantId().Value;

        var existing = await _db.Queryable<DataMigrationJob>()
            .Where(item => item.TenantIdValue == tenantValue
                           && item.SourceFingerprint == sourceFingerprint
                           && item.TargetFingerprint == targetFingerprint
                           && item.State == DataMigrationStates.CutoverCompleted)
            .FirstAsync()
            .ConfigureAwait(false);

        if (existing is not null && !request.AllowReExecute)
        {
            throw new InvalidOperationException(
                "an identical migration has already completed; set allowReExecute=true to re-run");
        }

        var now = DateTimeOffset.UtcNow;
        // ????????MigrationSecretProtector ???M8/A2 ?? 2.0 ????
        var job = new DataMigrationJob(
            _tenantProvider.GetTenantId(),
            id: _idGen.NextId(),
            mode: request.Mode,
            sourceConnectionString: _secretProtector.Encrypt(request.Source.ConnectionString),
            sourceDbType: request.Source.DbType,
            targetConnectionString: _secretProtector.Encrypt(request.Target.ConnectionString),
            targetDbType: request.Target.DbType,
            sourceFingerprint: sourceFingerprint,
            targetFingerprint: targetFingerprint,
            moduleScopeJson: JsonSerializer.Serialize(request.ModuleScope),
            createdBy: 0,
            now: now);
        await _db.Insertable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "OrmDataMigrationService", $"job created mode={request.Mode}", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> PrecheckJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        job.TransitionTo(DataMigrationStates.Prechecking, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);

        var sourceCfg = BuildSourceConfig(job);
        var targetCfg = BuildTargetConfig(job);

        var sourceTest = await TestConnectionAsync(sourceCfg, cancellationToken).ConfigureAwait(false);
        var targetTest = await TestConnectionAsync(targetCfg, cancellationToken).ConfigureAwait(false);

        if (!sourceTest.Connected || !targetTest.Connected)
        {
            job.TransitionTo(DataMigrationStates.Failed, DateTimeOffset.UtcNow,
                $"connectivity failed: source={sourceTest.Message} / target={targetTest.Message}");
            await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
            await AppendLogAsync(job.Id, "error", "Precheck", "connectivity failed", null).ConfigureAwait(false);
            return MapJob(job);
        }

        job.TransitionTo(DataMigrationStates.Ready, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Precheck", "passed", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> StartJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Ready or DataMigrationStates.Failed or DataMigrationStates.RolledBack))
        {
            throw new InvalidOperationException($"cannot start migration from state {job.State}");
        }

        // M9/C1????????????????Tenant / UserAccount / Role????
        var entityTypes = EntityTopologySorter.Sort(AtlasOrmSchemaCatalog.RuntimeEntities);
        job.MarkRunning(entityTypes.Count, totalRows: 0, now: DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Start", $"running with {entityTypes.Count} entities (topology sorted)", null).ConfigureAwait(false);

        // M9/C2???? checkpoint?? rowsCopied ??????????????
        var existingCheckpoints = await _db.Queryable<DataMigrationCheckpoint>()
            .Where(c => c.TenantIdValue == _tenantProvider.GetTenantId().Value && c.JobId == job.Id)
            .ToListAsync()
            .ConfigureAwait(false);
        var checkpointByEntity = existingCheckpoints.ToDictionary(c => c.EntityName, StringComparer.OrdinalIgnoreCase);

        var sourceCfg = BuildSourceConfig(job);
        var targetCfg = BuildTargetConfig(job);

        long totalCopied = checkpointByEntity.Values.Sum(c => c.RowsCopied);
        var completedCount = 0;
        var failedCount = 0;

        try
        {
            using var sourceScope = OpenScope(sourceCfg);
            using var targetScope = OpenScope(targetCfg);

            if (job.Mode is DataMigrationModes.StructureOnly or DataMigrationModes.StructurePlusData
                or DataMigrationModes.ReExecute)
            {
                AtlasOrmSchemaCatalog.EnsureRuntimeSchema(targetScope);
                await AppendLogAsync(job.Id, "info", "Schema", "target schema ensured", null).ConfigureAwait(false);
            }

            if (job.Mode is DataMigrationModes.StructureOnly)
            {
                completedCount = entityTypes.Count;
            }
            else
            {
                foreach (var entityType in entityTypes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var startBatchNo = 1;
                    long startSkip = 0;
                    if (checkpointByEntity.TryGetValue(entityType.Name, out var cp))
                    {
                        startBatchNo = cp.LastBatchNo + 1;
                        startSkip = cp.LastBatchNo * (long)DefaultBatchSize;
                        await AppendLogAsync(
                            job.Id, "info", "Resume",
                            $"{entityType.Name} resume from batch {startBatchNo} (already {cp.RowsCopied} rows)",
                            entityType.Name).ConfigureAwait(false);
                    }

                    var copied = await CopyEntityAsync(
                        sourceScope, targetScope, job.Id, entityType, startBatchNo, startSkip, cancellationToken)
                        .ConfigureAwait(false);
                    totalCopied += copied;
                    completedCount += 1;

                    job.RecordProgress(
                        currentEntity: entityType.Name,
                        currentBatch: startBatchNo,
                        completedEntities: completedCount,
                        failedEntities: failedCount,
                        copiedRows: totalCopied,
                        now: DateTimeOffset.UtcNow);
                    await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
                }
            }

            job.RecordProgress(
                currentEntity: null!,
                currentBatch: 0,
                completedEntities: completedCount,
                failedEntities: failedCount,
                copiedRows: totalCopied,
                now: DateTimeOffset.UtcNow);
            job.TransitionTo(DataMigrationStates.Validating, DateTimeOffset.UtcNow);
            await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
            await AppendLogAsync(job.Id, "info", "Start", $"copied {totalCopied} rows across {completedCount} entities", null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            job.TransitionTo(DataMigrationStates.Failed, DateTimeOffset.UtcNow, ex.Message);
            await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
            await AppendLogAsync(job.Id, "error", "Start", ex.Message, null).ConfigureAwait(false);
            _logger.LogError(ex, "[OrmMigration] job {JobId} failed", job.Id);
            throw;
        }

        return MapJob(job);
    }

    public async Task<DataMigrationProgressDto> GetProgressAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        var batches = await _db.Queryable<DataMigrationBatch>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == job.Id)
            .OrderByDescending(item => item.BatchNo)
            .Take(5)
            .ToListAsync()
            .ConfigureAwait(false);

        return new DataMigrationProgressDto(
            JobId: job.Id.ToString(),
            State: job.State,
            TotalEntities: job.TotalEntities,
            CompletedEntities: job.CompletedEntities,
            FailedEntities: job.FailedEntities,
            TotalRows: job.TotalRows,
            CopiedRows: job.CopiedRows,
            ProgressPercent: job.ProgressPercent,
            CurrentEntityName: job.CurrentEntityName,
            CurrentBatchNo: job.CurrentBatchNo,
            UpdatedAt: job.UpdatedAt,
            RecentBatches: batches.Select(b => new DataMigrationBatchDto(
                b.BatchNo,
                b.EntityName,
                b.RowsCopied,
                b.State,
                b.StartedAt,
                b.EndedAt,
                b.Checksum)).ToList());
    }

    public async Task<DataMigrationReportDto> ValidateJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Validating or DataMigrationStates.Running or DataMigrationStates.Ready))
        {
            throw new InvalidOperationException($"cannot validate migration from state {job.State}");
        }

        var sourceCfg = BuildSourceConfig(job);
        var targetCfg = BuildTargetConfig(job);
        using var sourceScope = OpenScope(sourceCfg);
        using var targetScope = OpenScope(targetCfg);

        var rowDiff = new List<DataMigrationRowDiffDto>();
        var samplingDiff = new List<DataMigrationSamplingDiffDto>();
        var passed = 0;
        var failed = 0;

        foreach (var entityType in AtlasOrmSchemaCatalog.RuntimeEntities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                long sourceCount;
                long targetCount;
                try
                {
                    sourceCount = (long)CountEntity(sourceScope, entityType);
                    targetCount = (long)CountEntity(targetScope, entityType);
                }
                catch (Exception countEx)
                {
                    // ????/????????????????? 0 ?????????
                    await AppendLogAsync(job.Id, "warn", "Validate",
                        $"{entityType.Name} count skipped: {countEx.Message}", entityType.Name).ConfigureAwait(false);
                    rowDiff.Add(new DataMigrationRowDiffDto(entityType.Name, 0, 0, 0));
                    passed += 1;
                    continue;
                }
                rowDiff.Add(new DataMigrationRowDiffDto(entityType.Name, sourceCount, targetCount, sourceCount - targetCount));

                bool entityPassed = sourceCount == targetCount;

                // M9/C3??????????????????????????????????????
                if (entityPassed && sourceCount > 0)
                {
                    var sampleSize = ComputeSampleSize(sourceCount);
                    var samplingResult = ComputeSamplingDiff(sourceScope, targetScope, entityType, sampleSize);
                    samplingDiff.Add(samplingResult);
                    if (samplingResult.Mismatched > 0)
                    {
                        entityPassed = false;
                    }
                }

                if (entityPassed)
                {
                    passed += 1;
                }
                else
                {
                    failed += 1;
                }
            }
            catch (Exception ex)
            {
                failed += 1;
                await AppendLogAsync(job.Id, "warn", "Validate", $"{entityType.Name}: {ex.Message}", entityType.Name).ConfigureAwait(false);
            }
        }

        var overallPassed = failed == 0;
        var report = new DataMigrationReport(
            _tenantProvider.GetTenantId(),
            id: _idGen.NextId(),
            jobId: job.Id,
            totalEntities: AtlasOrmSchemaCatalog.RuntimeEntities.Count,
            passedEntities: passed,
            failedEntities: failed,
            overallPassed: overallPassed,
            rowDiffJson: JsonSerializer.Serialize(rowDiff),
            samplingDiffJson: JsonSerializer.Serialize(samplingDiff),
            now: DateTimeOffset.UtcNow);
        await _db.Insertable(report).ExecuteCommandAsync().ConfigureAwait(false);

        if (overallPassed)
        {
            job.TransitionTo(DataMigrationStates.CutoverReady, DateTimeOffset.UtcNow);
        }
        else
        {
            job.TransitionTo(DataMigrationStates.Failed, DateTimeOffset.UtcNow, $"{failed} entities failed validation");
        }
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Validate", $"passed={passed}/{AtlasOrmSchemaCatalog.RuntimeEntities.Count}", null).ConfigureAwait(false);

        return new DataMigrationReportDto(
            JobId: job.Id.ToString(),
            TotalEntities: report.TotalEntities,
            PassedEntities: report.PassedEntities,
            FailedEntities: report.FailedEntities,
            RowDiff: rowDiff,
            SamplingDiff: samplingDiff,
            OverallPassed: overallPassed,
            GeneratedAt: report.GeneratedAt);
    }

    /// <summary>5% ??????10 ????100 ???/summary>
    private static int ComputeSampleSize(long sourceCount)
    {
        var raw = (int)Math.Min(sourceCount, Math.Max(10, sourceCount * 5L / 100));
        return Math.Min(100, raw);
    }

    /// <summary>
    /// ?????????M9/C3???? / ???? N ????Id ASC??
    /// ?????? JSON ??SHA256???Id ????
    /// ???? Id ?????5 ????????????
    /// </summary>
    private static DataMigrationSamplingDiffDto ComputeSamplingDiff(
        ISqlSugarClient sourceScope,
        ISqlSugarClient targetScope,
        Type entityType,
        int sampleSize)
    {
        var sourceRows = QueryPage(sourceScope, entityType, skip: 0, take: sampleSize);
        var targetRows = QueryPage(targetScope, entityType, skip: 0, take: sampleSize);
        if (sourceRows is null || targetRows is null)
        {
            return new DataMigrationSamplingDiffDto(entityType.Name, 0, 0, Array.Empty<string>());
        }

        var sourceHashes = HashRowsById(sourceRows);
        var targetHashes = HashRowsById(targetRows);
        var examples = new List<string>();
        var mismatched = 0;

        foreach (var (id, sourceHash) in sourceHashes)
        {
            if (!targetHashes.TryGetValue(id, out var targetHash))
            {
                mismatched += 1;
                if (examples.Count < 5)
                {
                    examples.Add($"{id}: missing in target");
                }
                continue;
            }
            if (!string.Equals(sourceHash, targetHash, StringComparison.Ordinal))
            {
                mismatched += 1;
                if (examples.Count < 5)
                {
                    examples.Add($"{id}: hash mismatch");
                }
            }
        }

        return new DataMigrationSamplingDiffDto(
            EntityName: entityType.Name,
            SampledRows: sourceHashes.Count,
            Mismatched: mismatched,
            MismatchedExamples: examples);
    }

    private static Dictionary<string, string> HashRowsById(System.Collections.IList rows)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var jsonOptions = new JsonSerializerOptions
        {
            // ???????????????????????????
            PropertyNamingPolicy = null
        };

        foreach (var row in rows)
        {
            if (row is null)
            {
                continue;
            }
            var id = ExtractIdAsString(row);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }
            var json = JsonSerializer.Serialize(row, row.GetType(), jsonOptions);
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
            result[id] = Convert.ToHexString(hash);
        }
        return result;
    }

    private static string ExtractIdAsString(object row)
    {
        var idProp = row.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp is null)
        {
            return string.Empty;
        }
        var value = idProp.GetValue(row);
        return value?.ToString() ?? string.Empty;
    }

    public async Task<DataMigrationJobDto> CutoverJobAsync(
        string jobId,
        DataMigrationCutoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.CutoverReady or DataMigrationStates.Validating))
        {
            throw new InvalidOperationException($"cannot cutover from state {job.State}");
        }

        // M9/C5?????? appsettings.runtime.json?PlatformHost / AppHost ????????????
        var targetConnectionString = _secretProtector.Decrypt(job.TargetConnectionString);
        try
        {
            await _runtimeConfigPersistor
                .PersistDatabaseConfigAsync(targetConnectionString, job.TargetDbType, cancellationToken)
                .ConfigureAwait(false);
            await AppendLogAsync(
                job.Id, "info", "Cutover",
                $"runtime database config persisted; restart PlatformHost / AppHost to apply",
                null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // ????????cutover ????????? + ????job ???
            await AppendLogAsync(
                job.Id, "warn", "Cutover",
                $"failed to persist runtime config: {ex.Message}; cutover state still applied",
                null).ConfigureAwait(false);
        }

        job.MarkFinished(DataMigrationStates.CutoverCompleted, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(
            job.Id, "info", "Cutover",
            $"completed; keep source readonly for {request.KeepSourceReadonlyForDays} days",
            null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> RollbackJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State == DataMigrationStates.CutoverCompleted)
        {
            throw new InvalidOperationException("cannot rollback an already cutover-completed migration");
        }
        job.MarkFinished(DataMigrationStates.RolledBack, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "warn", "Rollback", "rolled back by user request", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> RetryJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Failed or DataMigrationStates.RolledBack))
        {
            throw new InvalidOperationException($"cannot retry from state {job.State}");
        }
        job.TransitionTo(DataMigrationStates.Ready, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Retry", "retry triggered; ready to resume", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationReportDto?> GetReportAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var jobLong = ParseJobId(jobId);
        var report = await _db.Queryable<DataMigrationReport>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == jobLong)
            .OrderByDescending(item => item.GeneratedAt)
            .FirstAsync()
            .ConfigureAwait(false);
        if (report is null)
        {
            return null;
        }

        var rowDiff = JsonSerializer.Deserialize<List<DataMigrationRowDiffDto>>(report.RowDiffJson) ?? new List<DataMigrationRowDiffDto>();
        var samplingDiff = JsonSerializer.Deserialize<List<DataMigrationSamplingDiffDto>>(report.SamplingDiffJson) ?? new List<DataMigrationSamplingDiffDto>();

        return new DataMigrationReportDto(
            JobId: jobId,
            TotalEntities: report.TotalEntities,
            PassedEntities: report.PassedEntities,
            FailedEntities: report.FailedEntities,
            RowDiff: rowDiff,
            SamplingDiff: samplingDiff,
            OverallPassed: report.OverallPassed,
            GeneratedAt: report.GeneratedAt);
    }

    public async Task<DataMigrationLogPagedResponse> GetLogsAsync(
        string jobId,
        string? level,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var jobLong = ParseJobId(jobId);
        var tenant = _tenantProvider.GetTenantId().Value;
        var query = _db.Queryable<DataMigrationLog>()
            .Where(item => item.TenantIdValue == tenant && item.JobId == jobLong);
        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(item => item.Level == level);
        }

        var total = await query.CountAsync().ConfigureAwait(false);
        var page = Math.Max(pageIndex, 1);
        var size = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);
        var items = await query
            .OrderByDescending(item => item.OccurredAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync()
            .ConfigureAwait(false);

        return new DataMigrationLogPagedResponse(
            Items: items.Select(item => new DataMigrationLogItemDto(
                item.Id.ToString(),
                jobId,
                item.Level,
                item.Module,
                item.EntityName,
                item.Message,
                item.OccurredAt)).ToList(),
            Total: total,
            PageIndex: page,
            PageSize: size);
    }

    // ---------------------------------------------------------------- internals

    private async Task<DataMigrationJob> LoadJobAsync(string jobId)
    {
        var jobLong = ParseJobId(jobId);
        var tenant = _tenantProvider.GetTenantId().Value;
        var job = await _db.Queryable<DataMigrationJob>()
            .Where(item => item.TenantIdValue == tenant && item.Id == jobLong)
            .FirstAsync()
            .ConfigureAwait(false);
        return job ?? throw new InvalidOperationException($"migration job {jobId} not found");
    }

    private async Task<long> CopyEntityAsync(
        ISqlSugarClient sourceScope,
        ISqlSugarClient targetScope,
        long jobId,
        Type entityType,
        int startBatchNo,
        long startSkip,
        CancellationToken cancellationToken)
    {
        var batchNo = startBatchNo;
        var totalCopied = 0L;
        var skip = startSkip;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // M9??? query ??????????? ctor / ?????????
            // SqlSugar ????? ArgumentNullException ????"??????"???
            // ?????????????? ValidateJobAsync ????????????
            System.Collections.IList? rows;
            try
            {
                rows = QueryPage(sourceScope, entityType, (int)skip, DefaultBatchSize);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await AppendLogAsync(jobId, "warn", "Copy",
                    $"{entityType.Name} skipped (source query failed): {ex.Message}",
                    entityType.Name).ConfigureAwait(false);
                return totalCopied;
            }

            if (rows is null || rows.Count == 0)
            {
                break;
            }

            try
            {
                InsertBatch(targetScope, entityType, rows);
                totalCopied += rows.Count;

                var batch = new DataMigrationBatch(
                    _tenantProvider.GetTenantId(),
                    id: _idGen.NextId(),
                    jobId: jobId,
                    entityName: entityType.Name,
                    batchNo: batchNo,
                    now: DateTimeOffset.UtcNow);
                batch.MarkSucceeded(rows.Count, checksum: null, now: DateTimeOffset.UtcNow);
                await _db.Insertable(batch).ExecuteCommandAsync().ConfigureAwait(false);

                // M9/C2????????checkpoint????retry ????????
                await UpsertCheckpointAsync(jobId, entityType.Name, batchNo, totalCopied).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var batch = new DataMigrationBatch(
                    _tenantProvider.GetTenantId(),
                    id: _idGen.NextId(),
                    jobId: jobId,
                    entityName: entityType.Name,
                    batchNo: batchNo,
                    now: DateTimeOffset.UtcNow);
                batch.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
                await _db.Insertable(batch).ExecuteCommandAsync().ConfigureAwait(false);
                await AppendLogAsync(jobId, "error", "Copy", $"{entityType.Name}#{batchNo}: {ex.Message}", entityType.Name).ConfigureAwait(false);
                throw;
            }

            if (rows.Count < DefaultBatchSize)
            {
                break;
            }
            skip += DefaultBatchSize;
            batchNo += 1;
        }

        return totalCopied;
    }

    /// <summary>
    /// upsert <c>setup_data_migration_checkpoint</c>??jobId, entityName) ?????????
    /// </summary>
    private async Task UpsertCheckpointAsync(long jobId, string entityName, int lastBatchNo, long rowsCopied)
    {
        var existing = await _db.Queryable<DataMigrationCheckpoint>()
            .Where(c => c.TenantIdValue == _tenantProvider.GetTenantId().Value
                        && c.JobId == jobId
                        && c.EntityName == entityName)
            .FirstAsync()
            .ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            var checkpoint = new DataMigrationCheckpoint(
                _tenantProvider.GetTenantId(),
                id: _idGen.NextId(),
                jobId: jobId,
                entityName: entityName,
                now: now);
            checkpoint.Advance(lastBatchNo, lastMaxId: 0, rowsCopied: rowsCopied, now: now);
            await _db.Insertable(checkpoint).ExecuteCommandAsync().ConfigureAwait(false);
            return;
        }

        existing.Advance(lastBatchNo, lastMaxId: 0, rowsCopied: rowsCopied, now: now);
        await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);
    }

    private static System.Collections.IList? QueryPage(ISqlSugarClient scope, Type entityType, int skip, int take)
    {
        // ???? scope.Queryable<TEntity>() ?? ??? scope.GetType() ??? typeof(SqlSugarClient)?
        // ?? SqlSugarScope ? ISqlSugarClient ???????? SqlSugarClient ????????
        var queryableMethod = scope.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Queryable" && m.IsGenericMethod && m.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(scope, null);
        if (queryable is null)
        {
            return null;
        }

        var skipMethod = queryable.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Skip" && m.GetParameters().Length == 1);
        var skipped = skipMethod.Invoke(queryable, new object[] { skip });

        var takeMethod = queryable!.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Take" && m.GetParameters().Length == 1);
        var taken = takeMethod.Invoke(skipped, new object[] { take });

        var toListMethod = taken!.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "ToList" && m.GetParameters().Length == 0);
        return toListMethod.Invoke(taken, null) as System.Collections.IList;
    }

    private static int CountEntity(ISqlSugarClient scope, Type entityType)
    {
        var queryableMethod = scope.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Queryable" && m.IsGenericMethod && m.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(scope, null);
        if (queryable is null)
        {
            return 0;
        }
        var countMethod = queryable.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Count" && m.GetParameters().Length == 0);
        return (int)countMethod.Invoke(queryable, null)!;
    }

    private static void InsertBatch(ISqlSugarClient scope, Type entityType, System.Collections.IList rows)
    {
        if (rows.Count == 0)
        {
            return;
        }
        var typedArray = Array.CreateInstance(entityType, rows.Count);
        for (var i = 0; i < rows.Count; i += 1)
        {
            typedArray.SetValue(rows[i], i);
        }

        var insertableMethod = scope.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Insertable"
                        && m.IsGenericMethod
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType.IsArray);
        var insertable = insertableMethod.MakeGenericMethod(entityType).Invoke(scope, new object[] { typedArray });
        if (insertable is null)
        {
            return;
        }

        var execMethod = insertable.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "ExecuteCommand" && m.GetParameters().Length == 0);
        execMethod.Invoke(insertable, null);
    }

    private static ISqlSugarClient OpenScope(DbConnectionConfig connection)
    {
        var dbType = connection.DbType.Equals("MySql", StringComparison.OrdinalIgnoreCase) ? DbType.MySql
            : connection.DbType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ? DbType.PostgreSQL
            : connection.DbType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ? DbType.SqlServer
            : DbType.Sqlite;

        // M9??? SqlSugarScope???? ServiceCollectionExtensions ????
        // ?? ConfigureExternalServices.EntityService ? CodeFirst.InitTables ??????
        // ??? TenantEntity.TenantId ?? get-only ?????"??? 1 ? get set ? 2 ?"?
        return new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = connection.ConnectionString ?? string.Empty,
            DbType = dbType,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    // 1) ?? TenantEntity.TenantId ???????????
                    if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId)
                        && property.PropertyType == typeof(Atlas.Core.Tenancy.TenantId))
                    {
                        column.IsIgnore = true;
                        return;
                    }

                    // 2) ???? long Id ?????? SqlSugarScope ????????????
                    //    ??????????????? Updateable ? "no primary key"?
                    if (property.Name == "Id" && property.PropertyType == typeof(long))
                    {
                        column.IsPrimarykey = true;
                        column.IsIdentity = false;
                    }

                    // M9/C4?????????
                    // ???? [SugarColumn(ColumnDataType="TEXT")] ? MySQL ?????? TEXT??? 64KB??
                    // ? JSON / ??????????????? LONGTEXT?PostgreSQL ??? text?
                    // SqlServer ? NVARCHAR(MAX)?SqlSugar ? SQLite ??? TEXT affinity ???
                    if (column.DataType is "TEXT" or "text"
                        && property.PropertyType == typeof(string)
                        && property.GetCustomAttributes(typeof(SqlSugar.SugarColumn), inherit: false).Length > 0)
                    {
                        column.DataType = dbType switch
                        {
                            DbType.MySql => "LONGTEXT",
                            DbType.SqlServer => "NVARCHAR(MAX)",
                            DbType.PostgreSQL => "text",
                            _ => column.DataType
                        };
                    }
                }
            }
        });
    }

    private DbConnectionConfig BuildSourceConfig(DataMigrationJob job)
        => new(job.SourceDbType, job.SourceDbType, "raw", _secretProtector.Decrypt(job.SourceConnectionString), null);

    private DbConnectionConfig BuildTargetConfig(DataMigrationJob job)
        => new(job.TargetDbType, job.TargetDbType, "raw", _secretProtector.Decrypt(job.TargetConnectionString), null);

    /// <summary>?? UI ??????????Password=...; / Pwd=...; ????/summary>
    private static DbConnectionConfig BuildSafeConfig(string dbType, string? connectionString)
    {
        var safe = string.IsNullOrEmpty(connectionString)
            ? string.Empty
            : System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"(?i)(password|pwd)\s*=\s*[^;]+",
                "$1=***");
        return new DbConnectionConfig(dbType, dbType, "raw", safe, null);
    }

    private DataMigrationJobDto MapJob(DataMigrationJob job)
    {
        var moduleScope = string.IsNullOrEmpty(job.ModuleScopeJson)
            ? new DataMigrationModuleScopeDto(new[] { "all" }, null)
            : JsonSerializer.Deserialize<DataMigrationModuleScopeDto>(job.ModuleScopeJson)
              ?? new DataMigrationModuleScopeDto(new[] { "all" }, null);

        // ??????DTO ????????????????BuildSourceConfig/BuildTargetConfig ??
        var sourceCleartext = _secretProtector.Decrypt(job.SourceConnectionString);
        var targetCleartext = _secretProtector.Decrypt(job.TargetConnectionString);
        return new DataMigrationJobDto(
            Id: job.Id.ToString(),
            State: job.State,
            Mode: job.Mode,
            Source: BuildSafeConfig(job.SourceDbType, sourceCleartext),
            Target: BuildSafeConfig(job.TargetDbType, targetCleartext),
            SourceFingerprint: job.SourceFingerprint,
            TargetFingerprint: job.TargetFingerprint,
            ModuleScope: moduleScope,
            TotalEntities: job.TotalEntities,
            CompletedEntities: job.CompletedEntities,
            FailedEntities: job.FailedEntities,
            TotalRows: job.TotalRows,
            CopiedRows: job.CopiedRows,
            ProgressPercent: job.ProgressPercent,
            CurrentEntityName: job.CurrentEntityName,
            CurrentBatchNo: job.CurrentBatchNo,
            StartedAt: job.StartedAt,
            FinishedAt: job.FinishedAt,
            ErrorSummary: job.ErrorSummary,
            CreatedAt: job.CreatedAt,
            UpdatedAt: job.UpdatedAt);
    }

    private async Task AppendLogAsync(long jobId, string level, string module, string message, string? entityName)
    {
        var log = new DataMigrationLog(
            _tenantProvider.GetTenantId(),
            id: _idGen.NextId(),
            jobId: jobId,
            level: level,
            module: module,
            message: message,
            entityName: entityName,
            now: DateTimeOffset.UtcNow);
        await _db.Insertable(log).ExecuteCommandAsync().ConfigureAwait(false);
    }

    public static string ComputeFingerprint(DbConnectionConfig connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var canonical = $"{connection.DbType}|{connection.ConnectionString ?? string.Empty}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static long ParseJobId(string jobId)
    {
        if (long.TryParse(jobId, out var id))
        {
            return id;
        }
        throw new InvalidOperationException($"invalid jobId {jobId}");
    }
}
