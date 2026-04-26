using System.Reflection;
using System.Text.Json;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

public sealed class OrmDataMigrationService : IDataMigrationOrmService
{
    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly MigrationSecretProtector _secretProtector;
    private readonly RuntimeConfigPersistor _runtimeConfigPersistor;
    private readonly IMigrationConnectionResolver _resolver;
    private readonly IDataMigrationPlanner _planner;
    private readonly IBackgroundWorkQueue _backgroundWorkQueue;
    private readonly IDataMigrationRunner? _legacyInlineRunner;
    private readonly ILogger<OrmDataMigrationService> _logger;

    public OrmDataMigrationService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        MigrationSecretProtector secretProtector,
        RuntimeConfigPersistor runtimeConfigPersistor,
        IMigrationConnectionResolver resolver,
        IDataMigrationPlanner planner,
        IBackgroundWorkQueue backgroundWorkQueue,
        ILogger<OrmDataMigrationService> logger) : this(
        db,
        tenantProvider,
        idGen,
        secretProtector,
        runtimeConfigPersistor,
        resolver,
        planner,
        backgroundWorkQueue,
        legacyInlineRunner: null,
        logger)
    {
    }

    private OrmDataMigrationService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        MigrationSecretProtector secretProtector,
        RuntimeConfigPersistor runtimeConfigPersistor,
        IMigrationConnectionResolver resolver,
        IDataMigrationPlanner planner,
        IBackgroundWorkQueue backgroundWorkQueue,
        IDataMigrationRunner? legacyInlineRunner,
        ILogger<OrmDataMigrationService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
        _secretProtector = secretProtector;
        _runtimeConfigPersistor = runtimeConfigPersistor;
        _resolver = resolver;
        _planner = planner;
        _backgroundWorkQueue = backgroundWorkQueue;
        _legacyInlineRunner = legacyInlineRunner;
        _logger = logger;
    }

    public OrmDataMigrationService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        MigrationSecretProtector secretProtector,
        RuntimeConfigPersistor runtimeConfigPersistor,
        ILogger<OrmDataMigrationService> logger) : this(
        db,
        tenantProvider,
        idGen,
        secretProtector,
        runtimeConfigPersistor,
        CreateLegacyResolver(db, tenantProvider),
        new DataMigrationPlanner(db, idGen, tenantProvider, NullLogger<DataMigrationPlanner>.Instance),
        new LegacyBackgroundWorkQueue(),
        CreateLegacyRunner(db, tenantProvider, idGen, secretProtector),
        logger)
    {
    }

    public async Task<MigrationTestConnectionResponse> TestConnectionAsync(
        DbConnectionConfig connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolved = await _resolver.ResolveAsync(connection, cancellationToken).ConfigureAwait(false);
            using var scope = MigrationSqlSugarScopeFactory.Create(resolved.ConnectionString, resolved.DbType);
            var ok = await scope.Ado.GetScalarAsync("SELECT 1").ConfigureAwait(false) is not null;
            var tableCount = resolved.Tables.Count > 0 ? resolved.Tables.Count : scope.DbMaintenance.GetTableInfoList(false).Count;
            return new MigrationTestConnectionResponse(ok, ok ? "connection successful" : "connection failed", resolved.DbType, tableCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrmMigration] TestConnection failed for {DbType}", connection.DbType);
            return new MigrationTestConnectionResponse(false, ex.Message, connection.DbType, 0);
        }
    }

    public async Task<DataMigrationJobDto> CreateJobAsync(
        DataMigrationJobCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = await _resolver.ResolveAsync(request.Source, cancellationToken).ConfigureAwait(false);
        var target = await _resolver.ResolveAsync(request.Target, cancellationToken).ConfigureAwait(false);
        if (string.Equals(source.Fingerprint, target.Fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("source and target resolve to the same connection.");
        }

        var tenantValue = _tenantProvider.GetTenantId().Value;
        var existing = await _db.Queryable<DataMigrationJob>()
            .Where(item => item.TenantIdValue == tenantValue
                           && item.SourceFingerprint == source.Fingerprint
                           && item.TargetFingerprint == target.Fingerprint
                           && (item.State == DataMigrationStates.CutoverCompleted
                               || item.State == DataMigrationStates.Validated
                               || item.State == DataMigrationStates.Succeeded))
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null && !request.AllowReExecute)
        {
            throw new InvalidOperationException("an identical migration has already completed; set allowReExecute=true to re-run");
        }

        var now = DateTimeOffset.UtcNow;
        var job = new DataMigrationJob(
            _tenantProvider.GetTenantId(),
            _idGen.NextId(),
            request.Mode,
            _secretProtector.Encrypt(source.ConnectionString),
            source.DbType,
            _secretProtector.Encrypt(target.ConnectionString),
            target.DbType,
            source.Fingerprint,
            target.Fingerprint,
            JsonSerializer.Serialize(request.ModuleScope),
            JsonSerializer.Serialize(request.Source),
            JsonSerializer.Serialize(request.Target),
            JsonSerializer.Serialize(request.SelectedEntities ?? Array.Empty<string>()),
            JsonSerializer.Serialize(request.SelectedTables ?? Array.Empty<string>()),
            JsonSerializer.Serialize(request.ExcludedEntities ?? Array.Empty<string>()),
            JsonSerializer.Serialize(request.ExcludedTables ?? Array.Empty<string>()),
            request.BatchSize <= 0 ? 10000 : request.BatchSize,
            string.IsNullOrWhiteSpace(request.WriteMode) ? DataMigrationWriteModes.InsertOnly : request.WriteMode,
            request.CreateSchema,
            request.MigrateSystemTables,
            request.MigrateFiles,
            request.ValidateAfterCopy,
            createdBy: 0,
            now);
        await _db.Insertable(job).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Create", $"job created mode={request.Mode}", null, cancellationToken).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> GetJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationPrecheckResultDto> PrecheckJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        job.TransitionTo(DataMigrationStates.Prechecking, DateTimeOffset.UtcNow);
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);

        var source = await _resolver.ResolveAsync(BuildConfig(job.SourceConfigJson, job.SourceDbType, job.SourceConnectionString), cancellationToken)
            .ConfigureAwait(false);
        var target = await _resolver.ResolveAsync(BuildConfig(job.TargetConfigJson, job.TargetDbType, job.TargetConnectionString), cancellationToken)
            .ConfigureAwait(false);
        var plan = await _planner.PlanAsync(job, source, target, cancellationToken).ConfigureAwait(false);

        job.TransitionTo(DataMigrationStates.Ready, DateTimeOffset.UtcNow);
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Precheck", $"passed tables={plan.Items.Count} totalRows={plan.TotalRows}", null, cancellationToken)
            .ConfigureAwait(false);

        return new DataMigrationPrecheckResultDto(
            MapJob(job),
            plan.Items.Count,
            plan.TotalRows,
            plan.EstimatedBatches,
            plan.UnsupportedTables,
            plan.TargetNonEmptyTables,
            plan.MissingTargetTables,
            plan.Warnings,
            await GetTableProgressDtosAsync(job.Id, cancellationToken).ConfigureAwait(false));
    }

    public async Task<DataMigrationJobDto> StartJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Ready or DataMigrationStates.Failed or DataMigrationStates.Cancelled or DataMigrationStates.RolledBack or DataMigrationStates.Created))
        {
            throw new InvalidOperationException($"cannot start migration from state {job.State}");
        }

        job.MarkQueued(DateTimeOffset.UtcNow);
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Start", "job started", null, cancellationToken).ConfigureAwait(false);
        var inlineRunner = _legacyInlineRunner ?? new DataMigrationRunner(
            _db,
            _tenantProvider,
            _idGen,
            _secretProtector,
            _resolver,
            _planner,
            new SqlSugarMigrationBulkWriter(NullLogger<SqlSugarMigrationBulkWriter>.Instance),
            NullLogger<DataMigrationRunner>.Instance);
        await inlineRunner.RunAsync(job.Id, cancellationToken).ConfigureAwait(false);
        job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State == DataMigrationStates.Succeeded && job.ValidateAfterCopy)
        {
            await ValidateJobAsync(job.Id.ToString(), cancellationToken).ConfigureAwait(false);
            job = await LoadJobAsync(jobId).ConfigureAwait(false);
        }

        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> CancelJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is DataMigrationStates.Created or DataMigrationStates.Prechecking or DataMigrationStates.Ready or DataMigrationStates.Queued)
        {
            job.MarkFinished(DataMigrationStates.Cancelled, DateTimeOffset.UtcNow);
        }
        else if (job.State is DataMigrationStates.Running or DataMigrationStates.Validating)
        {
            job.TransitionTo(DataMigrationStates.Cancelling, DateTimeOffset.UtcNow);
        }
        else
        {
            throw new InvalidOperationException($"cannot cancel migration from state {job.State}");
        }

        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "warn", "Cancel", $"cancel requested from state {job.State}", null, cancellationToken).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationProgressDto> GetProgressAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        var recentLogs = await _db.Queryable<DataMigrationLog>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == job.Id)
            .OrderByDescending(item => item.OccurredAt)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var batches = await _db.Queryable<DataMigrationBatch>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == job.Id)
            .OrderByDescending(item => item.BatchNo)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var elapsedSeconds = job.StartedAt.HasValue
            ? Convert.ToInt64(((job.FinishedAt ?? DateTimeOffset.UtcNow) - job.StartedAt.Value).TotalSeconds)
            : 0;

        return new DataMigrationProgressDto(
            job.Id.ToString(),
            job.State,
            job.TotalEntities,
            job.CompletedEntities,
            job.FailedEntities,
            job.TotalRows,
            job.CopiedRows,
            job.ProgressPercent,
            job.CurrentEntityName,
            job.CurrentTableName,
            job.CurrentBatchNo,
            job.StartedAt,
            job.FinishedAt,
            elapsedSeconds,
            job.UpdatedAt,
            await GetTableProgressDtosAsync(job.Id, cancellationToken).ConfigureAwait(false),
            recentLogs.Select(MapLog).ToList(),
            batches.Select(batch => new DataMigrationBatchDto(
                batch.BatchNo,
                batch.EntityName,
                batch.RowsCopied,
                batch.State,
                batch.StartedAt,
                batch.EndedAt,
                batch.Checksum)).ToList());
    }

    public async Task<DataMigrationReportDto> ValidateJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Validating or DataMigrationStates.Running or DataMigrationStates.Ready or DataMigrationStates.Succeeded))
        {
            throw new InvalidOperationException($"cannot validate migration from state {job.State}");
        }

        job.TransitionTo(DataMigrationStates.Validating, DateTimeOffset.UtcNow);
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);

        var source = await _resolver.ResolveAsync(BuildConfig(job.SourceConfigJson, job.SourceDbType, job.SourceConnectionString), cancellationToken)
            .ConfigureAwait(false);
        var target = await _resolver.ResolveAsync(BuildConfig(job.TargetConfigJson, job.TargetDbType, job.TargetConnectionString), cancellationToken)
            .ConfigureAwait(false);
        using var sourceScope = MigrationSqlSugarScopeFactory.Create(source.ConnectionString, source.DbType);
        using var targetScope = MigrationSqlSugarScopeFactory.Create(target.ConnectionString, target.DbType);

        var tableProgress = await _db.Queryable<DataMigrationTableProgress>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == job.Id)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var rowDiff = new List<DataMigrationRowDiffDto>();
        var samplingDiff = new List<DataMigrationSamplingDiffDto>();
        var passed = 0;
        var failed = 0;

        foreach (var item in tableProgress)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entityType = AtlasOrmSchemaCatalog.RuntimeEntities.FirstOrDefault(type =>
                    string.Equals(type.Name, item.EntityName, StringComparison.OrdinalIgnoreCase));
                long sourceCount;
                long targetCount;
                if (entityType is not null)
                {
                    sourceCount = CountEntity(sourceScope, entityType);
                    targetCount = CountEntity(targetScope, entityType);
                }
                else
                {
                    sourceCount = await CountRawTableAsync(sourceScope, source.DbType, item.TableName).ConfigureAwait(false);
                    if (!targetScope.DbMaintenance.IsAnyTable(item.TableName, false))
                    {
                        rowDiff.Add(new DataMigrationRowDiffDto(item.EntityName, item.TableName, sourceCount, 0, sourceCount, "missing", "target table missing"));
                        failed += 1;
                        continue;
                    }

                    targetCount = await CountRawTableAsync(targetScope, target.DbType, item.TableName).ConfigureAwait(false);
                }

                var diff = sourceCount - targetCount;
                var tablePassed = diff == 0;
                rowDiff.Add(new DataMigrationRowDiffDto(item.EntityName, item.TableName, sourceCount, targetCount, diff, tablePassed ? "passed" : "failed"));

                if (tablePassed && entityType is not null && sourceCount > 0)
                {
                    var sampleSize = ComputeSampleSize(sourceCount);
                    var sampling = ComputeSamplingDiff(sourceScope, targetScope, entityType, sampleSize, item.TableName);
                    samplingDiff.Add(sampling);
                    if (sampling.Mismatched > 0)
                    {
                        tablePassed = false;
                    }
                }

                if (tablePassed)
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
                rowDiff.Add(new DataMigrationRowDiffDto(item.EntityName, item.TableName, 0, 0, 0, "failed", ex.Message));
                await AppendLogAsync(job.Id, "warn", "Validate", $"{item.TableName}: {ex.Message}", item.TableName, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var overallPassed = failed == 0;
        var report = new DataMigrationReport(
            _tenantProvider.GetTenantId(),
            _idGen.NextId(),
            job.Id,
            tableProgress.Count,
            passed,
            failed,
            overallPassed,
            JsonSerializer.Serialize(rowDiff),
            JsonSerializer.Serialize(samplingDiff),
            DateTimeOffset.UtcNow);
        await _db.Insertable(report).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        job.TransitionTo(overallPassed ? DataMigrationStates.Validated : DataMigrationStates.ValidationFailed, DateTimeOffset.UtcNow,
            overallPassed ? null : $"{failed} table(s) failed validation");
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Validate", $"passed={passed}/{tableProgress.Count}", null, cancellationToken).ConfigureAwait(false);

        return new DataMigrationReportDto(
            job.Id.ToString(),
            report.TotalEntities,
            report.PassedEntities,
            report.FailedEntities,
            rowDiff,
            samplingDiff,
            overallPassed,
            report.GeneratedAt);
    }

    public async Task<DataMigrationJobDto> CutoverJobAsync(
        string jobId,
        DataMigrationCutoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Validated or DataMigrationStates.CutoverReady))
        {
            throw new InvalidOperationException($"cannot cutover from state {job.State}");
        }
        if (!request.ConfirmBackup || !request.ConfirmRestartRequired)
        {
            throw new InvalidOperationException("cutover requires confirmBackup=true and confirmRestartRequired=true.");
        }

        try
        {
            await _runtimeConfigPersistor
                .PersistDatabaseConfigAsync(_secretProtector.Decrypt(job.TargetConnectionString), job.TargetDbType, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            job.TransitionTo(DataMigrationStates.CutoverFailed, DateTimeOffset.UtcNow, ex.Message);
            await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
            await AppendLogAsync(job.Id, "error", "Cutover", ex.Message, null, cancellationToken).ConfigureAwait(false);
            throw;
        }

        job.MarkFinished(DataMigrationStates.CutoverCompleted, DateTimeOffset.UtcNow);
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Cutover", $"completed; keep source readonly for {request.KeepSourceReadonlyForDays} days", null, cancellationToken)
            .ConfigureAwait(false);
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
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "warn", "Rollback", "rolled back by user request", null, cancellationToken).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> RetryJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Failed or DataMigrationStates.RolledBack or DataMigrationStates.Cancelled or DataMigrationStates.ValidationFailed))
        {
            throw new InvalidOperationException($"cannot retry from state {job.State}");
        }

        job.TransitionTo(DataMigrationStates.Ready, DateTimeOffset.UtcNow);
        await UpdateJobAsync(job, cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Retry", "retry triggered; ready to resume", null, cancellationToken).ConfigureAwait(false);
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
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
        {
            return null;
        }

        return new DataMigrationReportDto(
            jobId,
            report.TotalEntities,
            report.PassedEntities,
            report.FailedEntities,
            JsonSerializer.Deserialize<List<DataMigrationRowDiffDto>>(report.RowDiffJson) ?? [],
            JsonSerializer.Deserialize<List<DataMigrationSamplingDiffDto>>(report.SamplingDiffJson) ?? [],
            report.OverallPassed,
            report.GeneratedAt);
    }

    public async Task<DataMigrationLogPagedResponse> GetLogsAsync(
        string jobId,
        string? level,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var jobLong = ParseJobId(jobId);
        var query = _db.Queryable<DataMigrationLog>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == jobLong);
        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(item => item.Level == level);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var logs = await query
            .OrderByDescending(item => item.OccurredAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return new DataMigrationLogPagedResponse(logs.Select(MapLog).ToList(), total, pageIndex, pageSize);
    }

    public static string ComputeFingerprint(DbConnectionConfig connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var canonical = $"{connection.DbType}|{connection.ConnectionString ?? string.Empty}";
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<DataMigrationJob> LoadJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var parsedId = ParseJobId(jobId);
        var tenant = _tenantProvider.GetTenantId().Value;
        var job = await _db.Queryable<DataMigrationJob>()
            .Where(item => item.TenantIdValue == tenant && item.Id == parsedId)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        return job ?? throw new InvalidOperationException($"migration job {jobId} not found");
    }

    private Task<int> UpdateJobAsync(DataMigrationJob job, CancellationToken cancellationToken = default)
        => _db.Updateable(job)
            .Where(item => item.TenantIdValue == job.TenantIdValue && item.Id == job.Id)
            .ExecuteCommandAsync(cancellationToken);

    private DbConnectionConfig BuildConfig(string? configJson, string dbType, string encryptedConnectionString)
    {
        var config = string.IsNullOrWhiteSpace(configJson)
            ? new DbConnectionConfig(dbType, dbType, DataMigrationConnectionModes.ConnectionString, null, null, dbType)
            : JsonSerializer.Deserialize<DbConnectionConfig>(configJson)
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

    private static DbConnectionConfig BuildSafeConfig(DbConnectionConfig config)
    {
        var safeConnection = string.IsNullOrEmpty(config.ConnectionString)
            ? string.Empty
            : System.Text.RegularExpressions.Regex.Replace(config.ConnectionString, @"(?i)(password|pwd)\s*=\s*[^;]+", "$1=***");
        return config with { ConnectionString = safeConnection };
    }

    private DataMigrationJobDto MapJob(DataMigrationJob job)
    {
        var moduleScope = string.IsNullOrEmpty(job.ModuleScopeJson)
            ? new DataMigrationModuleScopeDto(new[] { "all" }, null)
            : JsonSerializer.Deserialize<DataMigrationModuleScopeDto>(job.ModuleScopeJson)
              ?? new DataMigrationModuleScopeDto(new[] { "all" }, null);

        return new DataMigrationJobDto(
            job.Id.ToString(),
            job.State,
            job.Mode,
            BuildSafeConfig(BuildConfig(job.SourceConfigJson, job.SourceDbType, job.SourceConnectionString)),
            BuildSafeConfig(BuildConfig(job.TargetConfigJson, job.TargetDbType, job.TargetConnectionString)),
            job.SourceFingerprint,
            job.TargetFingerprint,
            moduleScope,
            job.TotalEntities,
            job.CompletedEntities,
            job.FailedEntities,
            job.TotalRows,
            job.CopiedRows,
            job.ProgressPercent,
            job.CurrentEntityName,
            job.CurrentTableName,
            job.CurrentBatchNo,
            job.StartedAt,
            job.FinishedAt,
            job.ErrorSummary,
            job.CreatedAt,
            job.UpdatedAt);
    }

    private async Task<IReadOnlyList<DataMigrationTableProgressDto>> GetTableProgressDtosAsync(long jobId, CancellationToken cancellationToken)
    {
        var rows = await _db.Queryable<DataMigrationTableProgress>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == jobId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(item => new DataMigrationTableProgressDto(
            item.EntityName,
            item.TableName,
            item.State,
            item.SourceRows,
            item.TargetRowsBefore,
            item.TargetRowsAfter,
            item.CopiedRows,
            item.FailedRows,
            item.BatchSize,
            item.CurrentBatchNo,
            item.TotalBatchCount,
            item.ProgressPercent,
            item.StartedAt,
            item.FinishedAt,
            item.ErrorMessage)).ToList();
    }

    private static DataMigrationLogItemDto MapLog(DataMigrationLog item)
        => new(item.Id.ToString(), item.JobId.ToString(), item.Level, item.Module, item.EntityName, item.Message, item.OccurredAt);

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

    private static int ComputeSampleSize(long sourceCount)
    {
        var raw = (int)Math.Min(sourceCount, Math.Max(10, sourceCount * 5L / 100));
        return Math.Min(100, raw);
    }

    private static DataMigrationSamplingDiffDto ComputeSamplingDiff(
        ISqlSugarClient sourceScope,
        ISqlSugarClient targetScope,
        Type entityType,
        int sampleSize,
        string tableName)
    {
        var sourceRows = QuerySample(sourceScope, entityType, sampleSize);
        var targetRows = QuerySample(targetScope, entityType, sampleSize);
        if (sourceRows is null || targetRows is null)
        {
            return new DataMigrationSamplingDiffDto(entityType.Name, tableName, 0, 0, Array.Empty<string>());
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

        return new DataMigrationSamplingDiffDto(entityType.Name, tableName, sourceHashes.Count, mismatched, examples);
    }

    private static Dictionary<string, string> HashRowsById(System.Collections.IList rows)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
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

            var json = JsonSerializer.Serialize(row, row.GetType());
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
            result[id] = Convert.ToHexString(hash);
        }

        return result;
    }

    private static string ExtractIdAsString(object row)
    {
        var idProp = row.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        return idProp?.GetValue(row)?.ToString() ?? string.Empty;
    }

    private static System.Collections.IList? QuerySample(ISqlSugarClient scope, Type entityType, int take)
    {
        var queryableMethod = scope.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(method => method.Name == "Queryable" && method.IsGenericMethod && method.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(scope, null);
        if (queryable is null)
        {
            return null;
        }

        var ordered = TryOrderById(queryable);
        var takeMethod = ordered.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(method => method.Name == "Take" && method.GetParameters().Length == 1);
        var taken = takeMethod.Invoke(ordered, new object[] { take });
        var toListMethod = taken!.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(method => method.Name == "ToList" && method.GetParameters().Length == 0);
        return toListMethod.Invoke(taken, null) as System.Collections.IList;
    }

    private static object TryOrderById(object queryable)
    {
        var orderByMethod = queryable.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(method => method.Name == "OrderBy"
                                      && method.GetParameters().Length == 1
                                      && method.GetParameters()[0].ParameterType == typeof(string));
        return orderByMethod?.Invoke(queryable, new object[] { "Id asc" }) ?? queryable;
    }

    private static long CountEntity(ISqlSugarClient scope, Type entityType)
    {
        var queryableMethod = scope.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(method => method.Name == "Queryable" && method.IsGenericMethod && method.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(scope, null);
        if (queryable is null)
        {
            return 0;
        }

        var countMethod = queryable.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(method => method.Name == "Count" && method.GetParameters().Length == 0);
        return Convert.ToInt64(countMethod.Invoke(queryable, null) ?? 0);
    }

    private static async Task<long> CountRawTableAsync(ISqlSugarClient scope, string dbType, string tableName)
    {
        if (!scope.DbMaintenance.IsAnyTable(tableName, false))
        {
            return 0;
        }

        var result = await scope.Ado.GetScalarAsync(MigrationSqlSugarScopeFactory.BuildCountSql(dbType, tableName))
            .ConfigureAwait(false);
        return Convert.ToInt64(result ?? 0);
    }

    private static long ParseJobId(string jobId)
    {
        if (long.TryParse(jobId, out var id))
        {
            return id;
        }

        throw new InvalidOperationException($"invalid jobId {jobId}");
    }

    private static IMigrationConnectionResolver CreateLegacyResolver(ISqlSugarClient db, ITenantProvider tenantProvider)
    {
        var repo = new Repositories.TenantDataSourceRepository(db);
        var instanceRepo = new Repositories.AiDatabasePhysicalInstanceRepository(db);
        var options = Microsoft.Extensions.Options.Options.Create(new Infrastructure.Options.DatabaseEncryptionOptions());
        var aiSvc = new AiPlatform.AiDatabasePhysicalTableService(db, NullLogger<AiPlatform.AiDatabasePhysicalTableService>.Instance);
        var aiSecretProtector = new AiPlatform.AiDatabaseSecretProtector(options);
        return new MigrationConnectionResolver(db, tenantProvider, repo, options, aiSvc, instanceRepo, aiSecretProtector);
    }

    private static IDataMigrationRunner CreateLegacyRunner(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        MigrationSecretProtector secretProtector)
    {
        var resolver = CreateLegacyResolver(db, tenantProvider);
        var planner = new DataMigrationPlanner(db, idGen, tenantProvider, NullLogger<DataMigrationPlanner>.Instance);
        var bulkWriter = new SqlSugarMigrationBulkWriter(NullLogger<SqlSugarMigrationBulkWriter>.Instance);
        return new DataMigrationRunner(
            db,
            tenantProvider,
            idGen,
            secretProtector,
            resolver,
            planner,
            bulkWriter,
            NullLogger<DataMigrationRunner>.Instance);
    }

    private sealed class LegacyBackgroundWorkQueue : IBackgroundWorkQueue
    {
        public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            throw new NotSupportedException("Legacy inline migration service does not support background queue execution.");
        }
    }
}
