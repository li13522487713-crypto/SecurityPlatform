using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 本地磁盘文件存储实现（等保2.0：隔离目录 + 类型白名单 + 软删除审计）
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FileStorageOptions _options;
    private readonly FileRecordRepository _fileRecordRepository;
    private readonly FileUploadSessionRepository _fileUploadSessionRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IHostEnvironmentAccessor _hostEnvironmentAccessor;

    public LocalFileStorageService(
        IOptions<FileStorageOptions> options,
        FileRecordRepository fileRecordRepository,
        FileUploadSessionRepository fileUploadSessionRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider,
        IHostEnvironmentAccessor hostEnvironmentAccessor)
    {
        _options = options.Value;
        _fileRecordRepository = fileRecordRepository;
        _fileUploadSessionRepository = fileUploadSessionRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
        _hostEnvironmentAccessor = hostEnvironmentAccessor;
    }

    public async Task<FileUploadResult> UploadAsync(
        TenantId tenantId,
        long uploadedById,
        string uploadedByName,
        string originalName,
        string contentType,
        Stream fileStream,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        ValidateExtension(originalName);

        if (fileSizeBytes > _options.MaxFileSizeBytes)
        {
            throw new BusinessException(
                $"文件大小不能超过 {_options.MaxFileSizeBytes / 1024 / 1024} MB。",
                ErrorCodes.ValidationError);
        }

        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var dir = GetTenantDirectory(tenantId);
        Directory.CreateDirectory(dir);

        var physicalPath = Path.Combine(dir, storedName);
        await using (var dest = File.Create(physicalPath))
        {
            await fileStream.CopyToAsync(dest, ct);
        }

        var now = _timeProvider.GetUtcNow();
        var id = _idGeneratorAccessor.NextId();
        var record = new FileRecord(
            tenantId, originalName, storedName, contentType,
            fileSizeBytes, uploadedById, uploadedByName, now, id);

        await _fileRecordRepository.AddAsync(record, ct);

        return new FileUploadResult(id, originalName, contentType, fileSizeBytes, now);
    }

    public async Task<FileDownloadResult> DownloadAsync(
        TenantId tenantId, long fileId, CancellationToken ct = default)
    {
        var record = await _fileRecordRepository.FindByIdAsync(tenantId, fileId, ct)
            ?? throw new BusinessException("文件不存在或已删除。", ErrorCodes.NotFound);

        var physicalPath = Path.Combine(GetTenantDirectory(tenantId), record.StoredName);
        if (!File.Exists(physicalPath))
        {
            throw new BusinessException("文件物理存储不存在。", ErrorCodes.NotFound);
        }

        var stream = File.OpenRead(physicalPath);
        return new FileDownloadResult(stream, record.ContentType, record.OriginalName);
    }

    public async Task<FileRecordDto?> GetInfoAsync(
        TenantId tenantId, long fileId, CancellationToken ct = default)
    {
        var record = await _fileRecordRepository.FindByIdAsync(tenantId, fileId, ct);
        if (record is null) return null;

        return new FileRecordDto(
            record.Id, record.OriginalName, record.ContentType, record.SizeBytes,
            record.UploadedById, record.UploadedByName, record.UploadedAt);
    }

    public async Task DeleteAsync(TenantId tenantId, long fileId, CancellationToken ct = default)
    {
        var record = await _fileRecordRepository.FindByIdAsync(tenantId, fileId, ct)
            ?? throw new BusinessException("文件不存在。", ErrorCodes.NotFound);

        record.SoftDelete();
        await _fileRecordRepository.UpdateAsync(record, ct);
    }

    public async Task<FileChunkUploadInitResult> InitChunkUploadAsync(
        TenantId tenantId,
        long uploadedById,
        string uploadedByName,
        FileChunkUploadInitRequest request,
        CancellationToken ct = default)
    {
        ValidateExtension(request.OriginalName);
        if (request.TotalSizeBytes <= 0)
        {
            throw new BusinessException("文件大小无效。", ErrorCodes.ValidationError);
        }

        if (request.TotalSizeBytes > _options.MaxFileSizeBytes)
        {
            throw new BusinessException(
                $"文件大小不能超过 {_options.MaxFileSizeBytes / 1024 / 1024} MB。",
                ErrorCodes.ValidationError);
        }

        if (request.TotalParts <= 0)
        {
            throw new BusinessException("分片数量必须大于 0。", ErrorCodes.ValidationError);
        }

        var partSize = Math.Max(64 * 1024, _options.ChunkPartSizeBytes);
        var ext = Path.GetExtension(request.OriginalName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var sessionId = _idGeneratorAccessor.NextId();
        var tenantDirectory = GetTenantDirectory(tenantId);
        var tempDirectory = Path.Combine(tenantDirectory, ".chunks", sessionId.ToString());
        Directory.CreateDirectory(tempDirectory);

        var expiresAt = _timeProvider.GetUtcNow().AddMinutes(_options.ChunkSessionExpireMinutes);
        var session = new FileUploadSession(
            tenantId,
            request.OriginalName,
            storedName,
            request.ContentType,
            request.TotalSizeBytes,
            request.TotalParts,
            partSize,
            tempDirectory,
            expiresAt,
            uploadedById,
            uploadedByName,
            sessionId);
        await _fileUploadSessionRepository.AddAsync(session, ct);

        return new FileChunkUploadInitResult(sessionId, partSize, expiresAt);
    }

    public async Task UploadChunkPartAsync(
        TenantId tenantId,
        long sessionId,
        int partNumber,
        Stream partStream,
        long partSizeBytes,
        CancellationToken ct = default)
    {
        var session = await GetActiveSessionOrThrowAsync(tenantId, sessionId, ct);
        if (partNumber <= 0 || partNumber > session.TotalParts)
        {
            throw new BusinessException("分片序号超出范围。", ErrorCodes.ValidationError);
        }

        if (partSizeBytes <= 0)
        {
            throw new BusinessException("分片大小必须大于 0。", ErrorCodes.ValidationError);
        }

        if (partSizeBytes > session.PartSizeBytes && partNumber != session.TotalParts)
        {
            throw new BusinessException("分片大小超出限制。", ErrorCodes.ValidationError);
        }

        Directory.CreateDirectory(session.TempDirectory);
        var partFile = Path.Combine(session.TempDirectory, $"part-{partNumber:D6}.chunk");
        await using (var destination = File.Create(partFile))
        {
            await partStream.CopyToAsync(destination, ct);
        }

        var uploadedParts = ParseUploadedParts(session.UploadedPartNumbersJson);
        var uploadedSize = session.UploadedSizeBytes;
        if (uploadedParts.Add(partNumber))
        {
            uploadedSize += partSizeBytes;
        }

        var uploadedPartNumbersJson = JsonSerializer.Serialize(uploadedParts.Order().ToArray(), JsonOptions);
        session.UpdateProgress(uploadedSize, uploadedParts.Count, uploadedPartNumbersJson);
        await _fileUploadSessionRepository.UpdateAsync(session, ct);
    }

    public async Task<FileUploadResult> CompleteChunkUploadAsync(
        TenantId tenantId,
        long sessionId,
        FileChunkUploadCompleteRequest request,
        CancellationToken ct = default)
    {
        var session = await GetActiveSessionOrThrowAsync(tenantId, sessionId, ct);
        ValidateExtension(request.OriginalName);

        var uploadedParts = ParseUploadedParts(session.UploadedPartNumbersJson);
        if (uploadedParts.Count != session.TotalParts)
        {
            throw new BusinessException("分片尚未上传完成。", ErrorCodes.ValidationError);
        }

        var tenantDirectory = GetTenantDirectory(tenantId);
        Directory.CreateDirectory(tenantDirectory);
        var finalPath = Path.Combine(tenantDirectory, session.StoredName);

        await using (var destination = File.Create(finalPath))
        {
            for (var i = 1; i <= session.TotalParts; i++)
            {
                var chunkPath = Path.Combine(session.TempDirectory, $"part-{i:D6}.chunk");
                if (!File.Exists(chunkPath))
                {
                    throw new BusinessException($"缺少分片 {i}。", ErrorCodes.ValidationError);
                }

                await using var source = File.OpenRead(chunkPath);
                await source.CopyToAsync(destination, ct);
            }
        }

        var now = _timeProvider.GetUtcNow();
        var fileId = _idGeneratorAccessor.NextId();
        var fileRecord = new FileRecord(
            tenantId,
            request.OriginalName,
            session.StoredName,
            request.ContentType,
            session.TotalSizeBytes,
            session.UploadedByUserId,
            session.UploadedByName,
            now,
            fileId);

        await _fileRecordRepository.AddAsync(fileRecord, ct);
        session.MarkCompleted(fileId);
        await _fileUploadSessionRepository.UpdateAsync(session, ct);

        TryDeleteDirectory(session.TempDirectory);
        return new FileUploadResult(fileId, fileRecord.OriginalName, fileRecord.ContentType, fileRecord.SizeBytes, fileRecord.UploadedAt);
    }

    public async Task<FileUploadSessionProgressDto?> GetChunkUploadProgressAsync(
        TenantId tenantId,
        long sessionId,
        CancellationToken ct = default)
    {
        var session = await _fileUploadSessionRepository.FindByIdAsync(tenantId, sessionId, ct);
        if (session is null)
        {
            return null;
        }

        var completed = session.Status == FileUploadSessionStatus.Completed;
        return new FileUploadSessionProgressDto(
            session.Id,
            session.OriginalName,
            session.ContentType,
            session.TotalSizeBytes,
            session.UploadedSizeBytes,
            session.TotalParts,
            session.UploadedPartCount,
            completed,
            session.ExpiresAt,
            session.CreatedAt,
            session.UpdatedAt);
    }

    public async Task<FileSignedUrlResult> GenerateSignedUrlAsync(
        TenantId tenantId,
        long fileId,
        int expiresInSeconds,
        CancellationToken ct = default)
    {
        var record = await _fileRecordRepository.FindByIdAsync(tenantId, fileId, ct)
            ?? throw new BusinessException("文件不存在或已删除。", ErrorCodes.NotFound);

        var ttl = expiresInSeconds > 0
            ? Math.Min(expiresInSeconds, 24 * 60 * 60)
            : _options.SignedUrlDefaultExpireSeconds;
        var expiresAt = _timeProvider.GetUtcNow().AddSeconds(ttl);
        var expiresUnixSeconds = expiresAt.ToUnixTimeSeconds();
        var signature = ComputeSignature(tenantId, record.Id, expiresUnixSeconds);
        var url = $"/api/v1/files/signed/{record.Id}?tenantId={tenantId.Value:D}&expires={expiresUnixSeconds}&sig={signature}";
        return new FileSignedUrlResult(url, expiresAt);
    }

    public async Task<FileDownloadResult> DownloadBySignatureAsync(
        TenantId tenantId,
        long fileId,
        long expiresUnixSeconds,
        string signature,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new BusinessException("签名不能为空。", ErrorCodes.ValidationError);
        }

        var nowUnix = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (expiresUnixSeconds <= nowUnix)
        {
            throw new BusinessException("签名地址已过期。", ErrorCodes.Forbidden);
        }

        var expected = ComputeSignature(tenantId, fileId, expiresUnixSeconds);
        if (!FixedTimeEquals(expected, signature))
        {
            throw new BusinessException("签名无效。", ErrorCodes.Forbidden);
        }

        return await DownloadAsync(tenantId, fileId, ct);
    }

    // ===== Helpers =====

    private void ValidateExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        // 明确拒绝危险扩展名
        if (_options.BlockedExtensions.Contains(ext))
        {
            throw new BusinessException(
                $"不允许上传 '{ext}' 类型的文件。", ErrorCodes.ValidationError);
        }

        // 若有白名单配置则必须在白名单内
        if (_options.AllowedExtensions.Length > 0 && !_options.AllowedExtensions.Contains(ext))
        {
            throw new BusinessException(
                $"不支持的文件类型 '{ext}'。", ErrorCodes.ValidationError);
        }
    }

    private string GetTenantDirectory(TenantId tenantId)
    {
        var contentRoot = _hostEnvironmentAccessor.ContentRootPath;
        return Path.Combine(contentRoot, _options.BasePath, tenantId.Value.ToString());
    }

    private async Task<FileUploadSession> GetActiveSessionOrThrowAsync(TenantId tenantId, long sessionId, CancellationToken ct)
    {
        var session = await _fileUploadSessionRepository.FindActiveByIdAsync(tenantId, sessionId, ct)
            ?? throw new BusinessException("上传会话不存在或已结束。", ErrorCodes.NotFound);
        if (session.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            session.MarkExpired();
            await _fileUploadSessionRepository.UpdateAsync(session, ct);
            throw new BusinessException("上传会话已过期。", ErrorCodes.ValidationError);
        }

        return session;
    }

    private static HashSet<int> ParseUploadedParts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parts = JsonSerializer.Deserialize<int[]>(json, JsonOptions);
            return parts is null ? [] : parts.ToHashSet();
        }
        catch
        {
            return [];
        }
    }

    private string ComputeSignature(TenantId tenantId, long fileId, long expiresUnixSeconds)
    {
        var payload = $"{tenantId.Value:D}:{fileId}:{expiresUnixSeconds}";
        var secret = _options.SignedUrlSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("FileStorage:SignedUrlSecret 未配置，无法生成签名下载链接。");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }
}

/// <summary>
/// 提供 ContentRootPath 的抽象，便于测试时 Mock。
/// </summary>
public interface IHostEnvironmentAccessor
{
    string ContentRootPath { get; }
}

public sealed class HostEnvironmentAccessor : IHostEnvironmentAccessor
{
    private readonly IHostEnvironment _env;

    public HostEnvironmentAccessor(IHostEnvironment env)
    {
        _env = env;
    }

    public string ContentRootPath => _env.ContentRootPath;
}
