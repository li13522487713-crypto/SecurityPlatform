using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 文件记录实体（元数据，物理文件存储在本地磁盘）
/// </summary>
public sealed class FileRecord : TenantEntity
{
    public FileRecord()
        : base(TenantId.Empty)
    {
        OriginalName = string.Empty;
        StoredName = string.Empty;
        ContentType = string.Empty;
        UploadedByName = string.Empty;
    }

    public FileRecord(
        TenantId tenantId,
        string originalName,
        string storedName,
        string contentType,
        long sizeBytes,
        long uploadedById,
        string uploadedByName,
        DateTimeOffset uploadedAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        OriginalName = originalName;
        StoredName = storedName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        UploadedById = uploadedById;
        UploadedByName = uploadedByName;
        UploadedAt = uploadedAt;
        IsDeleted = false;
    }

    public string OriginalName { get; private set; }
    public string StoredName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public long UploadedById { get; private set; }
    public string UploadedByName { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public void SoftDelete() => IsDeleted = true;
}
