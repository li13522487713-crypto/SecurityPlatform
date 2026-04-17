using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 低代码资产上传会话（M10 S10-1）。
///
/// prepare-upload 颁发短期 token；complete-upload 用 token 换 fileHandle。
/// 未在窗口期完成的会话由 LowCodeAssetGcJob（M10 S10-3）回收，避免占用文件存储。
/// </summary>
public sealed class LowCodeAssetUploadSession : TenantEntity
{
#pragma warning disable CS8618
    public LowCodeAssetUploadSession()
        : base(TenantId.Empty)
    {
        Token = string.Empty;
        FileName = string.Empty;
        ContentType = string.Empty;
        Status = "pending";
    }
#pragma warning restore CS8618

    public LowCodeAssetUploadSession(TenantId tenantId, long id, string token, string fileName, string contentType, long sizeBytes, string? sha256, long uploadedByUserId)
        : base(tenantId)
    {
        Id = id;
        Token = token;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        UploadedByUserId = uploadedByUserId;
        Status = "pending";
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = CreatedAt.AddMinutes(30);
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Token { get; private set; }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string FileName { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? Sha256 { get; private set; }

    public long UploadedByUserId { get; private set; }

    /// <summary>状态：pending / completed / cancelled / expired。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; private set; }

    /// <summary>完成后绑定的 fileHandle（FileRecord.Id 字符串）。</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? FileHandle { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(string fileHandle)
    {
        FileHandle = fileHandle;
        Status = "completed";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = "cancelled";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkExpired()
    {
        Status = "expired";
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
