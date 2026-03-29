using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class AppMigrationService : IAppMigrationService
{
    private static readonly string[] RequiredAppSchemaTables =
    {
        "DynamicTable",
        "DynamicField",
        "DynamicIndex",
        "DynamicRelation",
        "FieldPermission",
        "MigrationRecord",
        "DynamicSchemaMigration",
        "AppMember",
        "AppRole",
        "AppUserRole",
        "AppRolePermission",
        "AppPermission",
        "AppRolePage",
        "AppDepartment",
        "AppPosition",
        "AppProject",
        "RuntimeRoute",
        "AppDatabaseSchemaVersion"
    };

    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly ITenantDbConnectionFactory _tenantDbConnectionFactory;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly AppDatabaseProvisioningService _provisioningService;

    public AppMigrationService(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        ITenantDbConnectionFactory tenantDbConnectionFactory,
        IIdGeneratorAccessor idGeneratorAccessor,
        AppDatabaseProvisioningService provisioningService)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _tenantDbConnectionFactory = tenantDbConnectionFactory;
        _idGeneratorAccessor = idGeneratorAccessor;
        _provisioningService = provisioningService;
    }

    public async Task<long> CreateTaskAsync(
        TenantId tenantId,
        long userId,
        AppMigrationTaskCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var dataSourceId = await ResolveDataSourceIdAsync(tenantId, request.AppInstanceId, cancellationToken);
        if (dataSourceId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "应用实例未绑定主数据源，无法创建迁移任务。");
        }

        var now = DateTimeOffset.UtcNow;
        var task = new AppMigrationTask(
            tenantId,
            request.AppInstanceId,
            dataSourceId,
            request.ReadOnlyWindow,
            request.EnableDualWrite,
            request.EnableRollback,
            userId,
            _idGeneratorAccessor.NextId(),
            now);
        await _mainDb.Insertable(task).ExecuteCommandAsync(cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return task.Id;
    }

    public async Task<PagedResult<AppMigrationTaskListItem>> QueryTasksAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _mainDb.Queryable<AppMigrationTask>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            query = query.Where(x =>
                x.Status.Contains(request.Keyword)
                || x.Phase.Contains(request.Keyword)
                || x.TenantAppInstanceId.ToString().Contains(request.Keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(request.PageIndex, request.PageSize, cancellationToken);

        var items = rows.Select(x => new AppMigrationTaskListItem(
            x.Id.ToString(),
            x.TenantIdValue.ToString("D"),
            x.TenantAppInstanceId.ToString(),
            x.Status,
            x.Phase,
            x.TotalItems,
            x.CompletedItems,
            x.FailedItems,
            x.ProgressPercent,
            x.CreatedAt,
            x.StartedAt,
            x.FinishedAt,
            x.ErrorSummary)).ToArray();
        return new PagedResult<AppMigrationTaskListItem>(items, total, request.PageIndex, request.PageSize);
    }

    public async Task<AppMigrationTaskDetail?> GetTaskAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        return MapDetail(task);
    }

    public async Task<AppMigrationPrecheckResult> PrecheckAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        task.MarkPrechecking(task.UpdatedBy <= 0 ? task.CreatedBy : task.UpdatedBy, now);
        await UpdateTaskAsync(task, cancellationToken);

        var checks = new List<string>();
        var warnings = new List<string>();

        var info = await _tenantDbConnectionFactory.GetConnectionInfoAsync(
            tenantId.Value.ToString("D"),
            task.TenantAppInstanceId,
            cancellationToken);
        if (info is null)
        {
            checks.Add("应用绑定数据源不可解析");
            task.MarkFailed(task.UpdatedBy, now, "应用绑定数据源不可解析");
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return new AppMigrationPrecheckResult(task.Id.ToString(), false, checks, warnings);
        }

        checks.Add("应用绑定数据源可解析");

        var appTables = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        checks.Add($"待迁动态表数量：{appTables.Count}");
        if (appTables.Count == 0)
        {
            warnings.Add("该应用暂无动态表元数据，迁移将主要处理应用权限和运行态数据。");
        }

        task.MarkReady(task.UpdatedBy, now);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return new AppMigrationPrecheckResult(task.Id.ToString(), true, checks, warnings);
    }

    public async Task<AppMigrationActionResult> StartAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        var precheck = await PrecheckAsync(tenantId, taskId, cancellationToken);
        if (!precheck.CanStart)
        {
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, "预检查未通过");
        }

        var now = DateTimeOffset.UtcNow;
        task.MarkRunning(userId, now, 17);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);

        try
        {
            var appDb = await EnsureSchemaBeforeDataSyncAsync(
                tenantId,
                task,
                userId,
                cancellationToken);

            var completed = 0;
            await CopyDynamicTablesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyDynamicFieldsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyDynamicIndexesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyDynamicRelationsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyFieldPermissionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyMigrationRecordsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppMembersAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppRolesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppPermissionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppUserRolesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppRolePermissionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppRolePagesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppDepartmentsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppPositionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyAppProjectsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
            await CopyRuntimeRoutesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);

            task.MarkCutoverReady(userId, DateTimeOffset.UtcNow);
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return new AppMigrationActionResult(true, task.Id.ToString(), task.Status, "迁移完成，等待切换");
        }
        catch (Exception ex)
        {
            task.MarkFailed(userId, DateTimeOffset.UtcNow, ex.Message);
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, ex.Message);
        }
    }

    private async Task<ISqlSugarClient> EnsureSchemaBeforeDataSyncAsync(
        TenantId tenantId,
        AppMigrationTask task,
        long userId,
        CancellationToken cancellationToken)
    {
        task.MarkObjectProgress("SchemaInit", 0, 0, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);

        await _provisioningService.EnsureSchemaAsync(tenantId, task.TenantAppInstanceId, cancellationToken);
        var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, task.TenantAppInstanceId, cancellationToken);
        EnsureRequiredTablesReady(appDb);

        task.MarkObjectProgress("SchemaReady", 0, 0, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return appDb;
    }

    private static void EnsureRequiredTablesReady(ISqlSugarClient appDb)
    {
        var tableNames = appDb.DbMaintenance.GetTableInfoList(false)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredAppSchemaTables
            .Where(table => !tableNames.Contains(table))
            .ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        throw new BusinessException(
            ErrorCodes.ServerError,
            $"应用库结构初始化不完整，缺少表：{string.Join(", ", missing)}");
    }

    public async Task<AppMigrationTaskProgress?> GetProgressAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        return new AppMigrationTaskProgress(
            task.Id.ToString(),
            task.Status,
            task.Phase,
            task.TotalItems,
            task.CompletedItems,
            task.FailedItems,
            task.ProgressPercent,
            task.CurrentObjectName,
            task.CurrentBatchNo,
            task.UpdatedAt,
            task.ErrorSummary);
    }

    public async Task<AppIntegrityCheckSummary> ValidateIntegrityAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        task.MarkValidating(userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);

        var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, task.TenantAppInstanceId, cancellationToken);
        var checks = new List<AppIntegrityCheckItem>();

        var mainDynamicTableCount = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .CountAsync(cancellationToken);
        var appDynamicTableCount = await appDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "TableCount",
            nameof(DynamicTable),
            mainDynamicTableCount == appDynamicTableCount,
            $"main={mainDynamicTableCount}, app={appDynamicTableCount}",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var mainRoleCount = await _mainDb.Queryable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .CountAsync(cancellationToken);
        var appRoleCount = await appDb.Queryable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "RowCount",
            nameof(AppRole),
            mainRoleCount == appRoleCount,
            $"main={mainRoleCount}, app={appRoleCount}",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var passed = checks.Count(x => x.Status == "Passed");
        var failed = checks.Count - passed;
        var report = new AppDataIntegrityReport(
            tenantId,
            task.Id,
            failed == 0,
            checks.Count,
            passed,
            failed,
            userId,
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow);
        await _mainDb.Insertable(report).ExecuteCommandAsync(cancellationToken);

        var reportItems = checks.Select(item => new AppIntegrityCheckItem(
            tenantId,
            report.Id,
            item.CheckType,
            item.ObjectName,
            item.Status == "Passed",
            item.DetailMessage,
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow)).ToArray();
        await _mainDb.Insertable(reportItems).ExecuteCommandAsync(cancellationToken);

        if (failed == 0)
        {
            task.MarkCutoverReady(userId, DateTimeOffset.UtcNow);
        }
        else
        {
            task.MarkFailed(userId, DateTimeOffset.UtcNow, "完整性校验未通过");
        }

        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return new AppIntegrityCheckSummary(task.Id.ToString(), failed == 0, checks.Count, passed, failed, DateTimeOffset.UtcNow);
    }

    public async Task<AppMigrationActionResult> CutoverAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        AppMigrationCutoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        if (!string.Equals(task.Status, AppMigrationTaskStatuses.CutoverReady, StringComparison.Ordinal))
        {
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, "任务尚未达到可切换状态");
        }

        var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == task.TenantAppInstanceId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (policy is null)
        {
            policy = new AppDataRoutePolicy(
                tenantId,
                task.TenantAppInstanceId,
                "AppOnly",
                request.EnableReadOnlyWindow,
                request.EnableDualWrite,
                userId,
                _idGeneratorAccessor.NextId(),
                now);
            await _mainDb.Insertable(policy).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            policy.SetMode("AppOnly", request.EnableReadOnlyWindow, request.EnableDualWrite, userId, now);
            await _mainDb.Updateable(policy).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkCutoverCompleted(userId, now, request.EnableReadOnlyWindow, request.EnableDualWrite);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return new AppMigrationActionResult(true, task.Id.ToString(), task.Status, "切换完成");
    }

    public async Task<AppMigrationActionResult> RollbackCutoverAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == task.TenantAppInstanceId, cancellationToken);
        if (policy is null)
        {
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, "未找到切换策略，无法回切");
        }

        policy.SetMode("MainOnly", true, false, userId, DateTimeOffset.UtcNow);
        await _mainDb.Updateable(policy).ExecuteCommandAsync(cancellationToken);

        task.MarkRolledBack(userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return new AppMigrationActionResult(true, task.Id.ToString(), task.Status, "已回切到主库");
    }

    private async Task<long> ResolveDataSourceIdAsync(TenantId tenantId, long appInstanceId, CancellationToken cancellationToken)
    {
        var binding = await _mainDb.Queryable<TenantAppDataSourceBinding>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.TenantAppInstanceId == appInstanceId
                && x.IsActive
                && x.BindingType == TenantAppDataSourceBindingType.Primary)
            .FirstAsync(cancellationToken);
        return binding?.DataSourceId ?? 0;
    }

    private async Task<AppMigrationTask?> FindTaskAsync(TenantId tenantId, long taskId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<AppMigrationTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == taskId)
            .FirstAsync(cancellationToken);
    }

    private async Task<AppMigrationTask> RequireTaskAsync(TenantId tenantId, long taskId, CancellationToken cancellationToken)
    {
        var task = await FindTaskAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "迁移任务不存在。");
        }

        return task;
    }

    private Task UpdateTaskAsync(AppMigrationTask task, CancellationToken cancellationToken)
    {
        return _mainDb.Updateable(task)
            .Where(x => x.Id == task.Id && x.TenantIdValue == task.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    private Task AddSnapshotAsync(AppMigrationTask task, CancellationToken cancellationToken)
    {
        var snapshot = new AppMigrationProgressSnapshot(
            new TenantId(task.TenantIdValue),
            task.Id,
            task.Status,
            task.Phase,
            task.TotalItems,
            task.CompletedItems,
            task.FailedItems,
            task.ProgressPercent,
            task.CurrentObjectName,
            task.CurrentBatchNo,
            task.ErrorSummary,
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow);
        return _mainDb.Insertable(snapshot).ExecuteCommandAsync(cancellationToken);
    }

    private static AppMigrationTaskDetail MapDetail(AppMigrationTask task)
    {
        return new AppMigrationTaskDetail(
            task.Id.ToString(),
            task.TenantIdValue.ToString("D"),
            task.TenantAppInstanceId.ToString(),
            task.DataSourceId.ToString(),
            task.Status,
            task.Phase,
            task.TotalItems,
            task.CompletedItems,
            task.FailedItems,
            task.ProgressPercent,
            task.CurrentObjectName,
            task.CurrentBatchNo,
            task.ReadOnlyWindow,
            task.EnableDualWrite,
            task.EnableRollback,
            task.CreatedAt,
            task.StartedAt,
            task.FinishedAt,
            task.ErrorSummary);
    }

    private async Task CopyDynamicTablesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<DynamicTable>().Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId).ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(DynamicTable), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyDynamicFieldsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var tableIds = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (tableIds.Count == 0)
        {
            task.MarkObjectProgress(nameof(DynamicField), 1, completed, 0, userId, DateTimeOffset.UtcNow);
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return;
        }

        var rows = await _mainDb.Queryable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableIds, x.TableId))
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableIds, x.TableId))
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(DynamicField), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyDynamicIndexesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var tableIds = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var rows = tableIds.Count == 0
            ? new List<DynamicIndex>()
            : await _mainDb.Queryable<DynamicIndex>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableIds, x.TableId))
                .ToListAsync(cancellationToken);
        if (tableIds.Count > 0)
        {
            await appDb.Deleteable<DynamicIndex>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableIds, x.TableId))
                .ExecuteCommandAsync(cancellationToken);
        }
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(DynamicIndex), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyDynamicRelationsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var tableIds = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var tableKeys = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.TableKey)
            .ToListAsync(cancellationToken);
        var rows = tableIds.Count == 0
            ? new List<DynamicRelation>()
            : await _mainDb.Queryable<DynamicRelation>()
                .Where(x => x.TenantIdValue == tenantId.Value
                    && (SqlFunc.ContainsArray(tableIds, x.TableId) || SqlFunc.ContainsArray(tableKeys, x.RelatedTableKey)))
                .ToListAsync(cancellationToken);
        if (tableIds.Count > 0 || tableKeys.Count > 0)
        {
            await appDb.Deleteable<DynamicRelation>()
                .Where(x => x.TenantIdValue == tenantId.Value
                    && (SqlFunc.ContainsArray(tableIds, x.TableId) || SqlFunc.ContainsArray(tableKeys, x.RelatedTableKey)))
                .ExecuteCommandAsync(cancellationToken);
        }
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(DynamicRelation), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyFieldPermissionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var prefix = $"app:{task.TenantAppInstanceId}:";
        var rows = await _mainDb.Queryable<FieldPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey.StartsWith(prefix))
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<FieldPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey.StartsWith(prefix))
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(FieldPermission), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyMigrationRecordsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var tableKeys = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.TableKey)
            .ToListAsync(cancellationToken);
        var rows = tableKeys.Count == 0
            ? new List<MigrationRecord>()
            : await _mainDb.Queryable<MigrationRecord>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableKeys, x.TableKey))
                .ToListAsync(cancellationToken);
        if (tableKeys.Count > 0)
        {
            await appDb.Deleteable<MigrationRecord>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableKeys, x.TableKey))
                .ExecuteCommandAsync(cancellationToken);
        }
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(MigrationRecord), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppMembersAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppMember), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppRolesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppRole), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppPermissionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppPermission), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppUserRolesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppUserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppUserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppUserRole), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppRolePermissionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppRolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppRolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppRolePermission), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppRolePagesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppRolePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppRolePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppRolePage), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppDepartmentsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppDepartment), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppPositionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppPosition), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppProjectsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppProject), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyRuntimeRoutesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var app = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == task.TenantAppInstanceId)
            .FirstAsync(cancellationToken);
        if (app is null)
        {
            task.MarkObjectProgress(nameof(RuntimeRoute), 1, completed, 0, userId, DateTimeOffset.UtcNow);
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return;
        }

        var rows = await _mainDb.Queryable<RuntimeRoute>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == app.AppKey)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<RuntimeRoute>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == app.AppKey)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(RuntimeRoute), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }
}
