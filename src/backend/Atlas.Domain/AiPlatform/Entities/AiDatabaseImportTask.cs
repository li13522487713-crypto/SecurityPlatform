using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiDatabaseImportTask : TenantEntity
{
    public AiDatabaseImportTask()
        : base(TenantId.Empty)
    {
        ErrorMessage = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiDatabaseImportTask(
        TenantId tenantId,
        long databaseId,
        long fileId,
        long id)
        : this(
            tenantId,
            databaseId,
            fileId,
            id,
            AiDatabaseImportSource.File,
            payloadJson: null,
            ownerUserId: null,
            creatorUserId: null,
            channelId: null,
            environment: AiDatabaseRecordEnvironment.Draft)
    {
    }

    /// <summary>D5：通用构造，区分文件 / 内联 JSON 来源；行级元数据 owner/creator/channel 透传给 records。</summary>
    public AiDatabaseImportTask(
        TenantId tenantId,
        long databaseId,
        long fileId,
        long id,
        AiDatabaseImportSource source,
        string? payloadJson,
        long? ownerUserId,
        long? creatorUserId,
        string? channelId,
        AiDatabaseRecordEnvironment environment)
        : base(tenantId)
    {
        Id = id;
        DatabaseId = databaseId;
        FileId = fileId;
        Source = source;
        PayloadJson = payloadJson;
        OwnerUserId = ownerUserId;
        CreatorUserId = creatorUserId;
        ChannelId = string.IsNullOrWhiteSpace(channelId) ? null : channelId.Trim();
        Environment = environment;
        Status = AiDatabaseImportStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long DatabaseId { get; private set; }
    public long FileId { get; private set; }
    /// <summary>D5：来源——File（CSV）或 Inline（前端直接 POST 行 JSON 数组）。</summary>
    public AiDatabaseImportSource Source { get; private set; }
    /// <summary>D5：当 Source=Inline 时承载行 JSON 数组；File 模式下为 null。</summary>
    [SqlSugar.SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? PayloadJson { get; private set; }
    /// <summary>D5/D9：行级元数据透传给 records（与 AiDatabaseRecord 一致语义）。</summary>
    public long? OwnerUserId { get; private set; }
    public long? CreatorUserId { get; private set; }
    [SqlSugar.SugarColumn(Length = 64, IsNullable = true)]
    public string? ChannelId { get; private set; }
    public AiDatabaseRecordEnvironment Environment { get; private set; }
    public AiDatabaseImportStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int SucceededRows { get; private set; }
    public int FailedRows { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void MarkRunning()
    {
        Status = AiDatabaseImportStatus.Running;
        UpdatedAt = DateTime.UtcNow;
        ErrorMessage = string.Empty;
    }

    public void MarkCompleted(int totalRows, int succeededRows, int failedRows)
    {
        Status = AiDatabaseImportStatus.Completed;
        TotalRows = Math.Max(0, totalRows);
        SucceededRows = Math.Max(0, succeededRows);
        FailedRows = Math.Max(0, failedRows);
        ErrorMessage = string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? errorMessage)
    {
        Status = AiDatabaseImportStatus.Failed;
        ErrorMessage = errorMessage ?? "导入失败";
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiDatabaseImportStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

public enum AiDatabaseImportSource
{
    /// <summary>来源 = 上传文件（默认 CSV）。</summary>
    File = 0,
    /// <summary>D5：来源 = 内联 JSON 行数组（异步批量插入）。</summary>
    Inline = 1
}
