using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

public sealed class FileUploadSession : TenantEntity
{
    public FileUploadSession()
        : base(TenantId.Empty)
    {
        OriginalName = string.Empty;
        StoredName = string.Empty;
        ContentType = string.Empty;
        UploadedPartNumbersJson = "[]";
        TempDirectory = string.Empty;
        UploadedByName = string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public FileUploadSession(
        TenantId tenantId,
        string originalName,
        string storedName,
        string contentType,
        long totalSizeBytes,
        int totalParts,
        int partSizeBytes,
        string tempDirectory,
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
        TotalParts = totalParts;
        PartSizeBytes = partSizeBytes;
        TempDirectory = tempDirectory;
        UploadedPartNumbersJson = "[]";
        Status = FileUploadSessionStatus.Pending;
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
    public int TotalParts { get; private set; }
    public int UploadedPartCount { get; private set; }
    public int PartSizeBytes { get; private set; }
    public string UploadedPartNumbersJson { get; private set; }
    public string TempDirectory { get; private set; }
    public FileUploadSessionStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public long UploadedByUserId { get; private set; }
    public string UploadedByName { get; private set; }
    public long? FileId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void UpdateProgress(
        long uploadedSizeBytes,
        int uploadedPartCount,
        string uploadedPartNumbersJson)
    {
        UploadedSizeBytes = uploadedSizeBytes;
        UploadedPartCount = uploadedPartCount;
        UploadedPartNumbersJson = uploadedPartNumbersJson;
        if (Status == FileUploadSessionStatus.Pending)
        {
            Status = FileUploadSessionStatus.Uploading;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(long fileId)
    {
        FileId = fileId;
        Status = FileUploadSessionStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkExpired()
    {
        Status = FileUploadSessionStatus.Expired;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum FileUploadSessionStatus
{
    Pending = 0,
    Uploading = 1,
    Completed = 2,
    Expired = 3
}
