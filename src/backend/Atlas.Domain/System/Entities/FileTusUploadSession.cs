using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

public sealed class FileTusUploadSession : TenantEntity
{
    public FileTusUploadSession()
        : base(TenantId.Empty)
    {
        OriginalName = string.Empty;
        StoredName = string.Empty;
        ContentType = string.Empty;
        TempFilePath = string.Empty;
        UploadedByName = string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public FileTusUploadSession(
        TenantId tenantId,
        string originalName,
        string storedName,
        string contentType,
        long totalSizeBytes,
        string tempFilePath,
        DateTimeOffset expiresAt,
        long uploadedByUserId,
        string uploadedByName,
        long id)
        : base(tenantId)
    {
        Id = id;
        OriginalName = originalName;
        StoredName = storedName;
        ContentType = contentType;
        TotalSizeBytes = totalSizeBytes;
        UploadedSizeBytes = 0;
        TempFilePath = tempFilePath;
        Status = FileTusUploadSessionStatus.Pending;
        ExpiresAt = expiresAt;
        UploadedByUserId = uploadedByUserId;
        UploadedByName = uploadedByName;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string OriginalName { get; private set; }
    public string StoredName { get; private set; }
    public string ContentType { get; private set; }
    public long TotalSizeBytes { get; private set; }
    public long UploadedSizeBytes { get; private set; }
    public string TempFilePath { get; private set; }
    public FileTusUploadSessionStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public long UploadedByUserId { get; private set; }
    public string UploadedByName { get; private set; }
    public long FileId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void UpdateUploadedSize(long uploadedSizeBytes)
    {
        UploadedSizeBytes = uploadedSizeBytes;
        if (Status == FileTusUploadSessionStatus.Pending)
        {
            Status = FileTusUploadSessionStatus.Uploading;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(long fileId)
    {
        FileId = fileId;
        Status = FileTusUploadSessionStatus.Completed;
        UploadedSizeBytes = TotalSizeBytes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkExpired()
    {
        Status = FileTusUploadSessionStatus.Expired;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum FileTusUploadSessionStatus
{
    Pending = 0,
    Uploading = 1,
    Completed = 2,
    Expired = 3
}
