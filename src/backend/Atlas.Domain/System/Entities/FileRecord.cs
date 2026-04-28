using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 文件记录实体（支持多版本控制与软删除）
/// </summary>
public sealed class FileRecord : TenantEntity
{
    public FileRecord()
        : base(TenantId.Empty)
    {
        OriginalName = string.Empty;
        StoredName = string.Empty;
        ContentType = string.Empty;
        FileHashSha256 = string.Empty;
        UploadedByName = string.Empty;
        VersionNumber = 1;
        IsLatestVersion = true;
        PreviousVersionId = 0;
    }

    public FileRecord(
        TenantId tenantId,
        string originalName,
        string storedName,
        string contentType,
        string fileHashSha256,
        long sizeBytes,
        long uploadedById,
        string uploadedByName,
        DateTimeOffset uploadedAt,
        long id,
        int versionNumber = 1,
        bool isLatestVersion = true,
        long? previousVersionId = null)
        : base(tenantId)
    {
        Id = id;
        OriginalName = originalName;
        StoredName = storedName;
        ContentType = contentType;
        FileHashSha256 = fileHashSha256;
        SizeBytes = sizeBytes;
        UploadedById = uploadedById;
        UploadedByName = uploadedByName;
        UploadedAt = uploadedAt;
        IsDeleted = false;
        VersionNumber = versionNumber;
        IsLatestVersion = isLatestVersion;
        PreviousVersionId = previousVersionId ?? 0;
    }

    public string OriginalName { get; private set; }
    public string StoredName { get; private set; }
    public string ContentType { get; private set; }
    public string FileHashSha256 { get; private set; }
    public long SizeBytes { get; private set; }
    public long UploadedById { get; private set; }
    public string UploadedByName { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    /// <summary>文件版本号，从 1 开始递增。同名文件每次更新内容后 +1。</summary>
    public int VersionNumber { get; private set; }

    /// <summary>是否为当前最新版本。每次新版本上传后旧版本置为 false。</summary>
    public bool IsLatestVersion { get; private set; }

    /// <summary>指向上一版本 FileRecord 的 ID，首版本为 null。</summary>
    public long PreviousVersionId { get; private set; }

    public void SoftDelete() => IsDeleted = true;

    /// <summary>新版本上传时将当前版本标记为历史版本。</summary>
    public void MarkAsOldVersion() => IsLatestVersion = false;
}
