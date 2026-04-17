using Atlas.Application.SetupConsole.Models;

namespace Atlas.Application.SetupConsole.Abstractions;

/// <summary>
/// ORM 优先的跨库数据迁移服务（M6）。
///
/// 设计原则：
/// - 源库 / 目标库各起一个 SqlSugarScope，用同一份 EntityType[] 双向 IO；
/// - 类型差异（DateTimeOffset / bool / long 等）由 SqlSugar 自动适配；
/// - 拓扑排序保证插入顺序（Tenant → UserAccount → Role → Workspace → Agent ...）；
/// - 防重复指纹：SourceFingerprint + TargetFingerprint 相同的"已 cutover-completed"任务必须 allowReExecute=true 才能重建；
/// - 断点续跑：每实体每批次写 DataMigrationCheckpoint(JobId, EntityName)。
/// </summary>
public interface IDataMigrationOrmService
{
    Task<MigrationTestConnectionResponse> TestConnectionAsync(
        DbConnectionConfig connection,
        CancellationToken cancellationToken = default);

    Task<DataMigrationJobDto> CreateJobAsync(
        DataMigrationJobCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<DataMigrationJobDto> PrecheckJobAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task<DataMigrationJobDto> StartJobAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task<DataMigrationProgressDto> GetProgressAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task<DataMigrationReportDto> ValidateJobAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task<DataMigrationJobDto> CutoverJobAsync(
        string jobId,
        DataMigrationCutoverRequest request,
        CancellationToken cancellationToken = default);

    Task<DataMigrationJobDto> RollbackJobAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task<DataMigrationJobDto> RetryJobAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task<DataMigrationReportDto?> GetReportAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task<DataMigrationLogPagedResponse> GetLogsAsync(
        string jobId,
        string? level,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
}
