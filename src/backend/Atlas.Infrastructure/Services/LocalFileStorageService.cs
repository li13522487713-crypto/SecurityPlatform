using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.FileStorage;
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
    private readonly IOptionsMonitor<FileStorageOptions> _optionsMonitor;
    private readonly IFileStorageSettingsResolver _settingsResolver;
    private readonly FileRecordRepository _fileRecordRepository;
    private readonly FileUploadSessionRepository _fileUploadSessionRepository;
    private readonly FileTusUploadSessionRepository _fileTusUploadSessionRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IFileObjectStore _fileObjectStore;
    private readonly IHostEnvironmentAccessor _hostEnvironmentAccessor;

    public LocalFileStorageService(
        IOptionsMonitor<FileStorageOptions> optionsMonitor,
        IFileStorageSettingsResolver settingsResolver,
        FileRecordRepository fileRecordRepository,
        FileUploadSessionRepository fileUploadSessionRepository,
        FileTusUploadSessionRepository fileTusUploadSessionRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider,
        IFileObjectStore fileObjectStore,
        IHostEnvironmentAccessor hostEnvironmentAccessor)
    {
        _optionsMonitor = optionsMonitor;
        _settingsResolver = settingsResolver;
        _fileRecordRepository = fileRecordRepository;
        _fileUploadSessionRepository = fileUploadSessionRepository;
        _fileTusUploadSessionRepository = fileTusUploadSessionRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
        _fileObjectStore = fileObjectStore;
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
        var options = _optionsMonitor.CurrentValue;
        ValidateExtension(originalName);

        if (fileSizeBytes > options.MaxFileSizeBytes)
        {
            throw new BusinessException(
                $"文件大小不能超过 {options.MaxFileSizeBytes / 1024 / 1024} MB。",
                ErrorCodes.ValidationError);
        }

        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        await _fileObjectStore.SaveAsync(tenantId, storedName, fileStream, contentType, ct);
        await using var uploadedStream = await _fileObjectStore.OpenReadAsync(tenantId, storedName, ct);
        var fileHashSha256 = await ComputeSha256Async(uploadedStream, ct);

        // 秒传去重：相同内容已存在则删除刚上传的物理文件，直接返回已有记录
        var existingByHash = await _fileRecordRepository.FindByHashAsync(tenantId, fileHashSha256, fileSizeBytes, ct);
        if (existingByHash is not null)
        {
            await _fileObjectStore.DeleteAsync(tenantId, storedName, ct);
            return new FileUploadResult(
                existingByHash.Id,
                existingByHash.OriginalName,
                existingByHash.ContentType,
                existingByHash.SizeBytes,
                existingByHash.UploadedAt,
                existingByHash.VersionNumber,
                existingByHash.IsLatestVersion);
        }

        // 版本控制：同名文件上传新内容时递增版本号
        var now = _timeProvider.GetUtcNow();
        var id = _idGeneratorAccessor.NextId();
        int versionNumber = 1;
        long? previousVersionId = null;

        var latestVersion = await _fileRecordRepository.FindLatestByOriginalNameAsync(tenantId, originalName, ct);
        if (latestVersion is not null)
        {
            versionNumber = latestVersion.VersionNumber + 1;
            previousVersionId = latestVersion.Id;
            latestVersion.MarkAsOldVersion();
            await _fileRecordRepository.UpdateAsync(latestVersion, ct);
        }

        var record = new FileRecord(
            tenantId, originalName, storedName, contentType, fileHashSha256,
            fileSizeBytes, uploadedById, uploadedByName, now, id,
            versionNumber, isLatestVersion: true, previousVersionId);

        await _fileRecordRepository.AddAsync(record, ct);

        return new FileUploadResult(id, originalName, contentType, fileSizeBytes, now, versionNumber, IsLatestVersion: true);
    }

    public async Task<FileDownloadResult> DownloadAsync(
        TenantId tenantId, long fileId, CancellationToken ct = default)
    {
        var record = await _fileRecordRepository.FindByIdAsync(tenantId, fileId, ct)
            ?? throw new BusinessException("文件不存在或已删除。", ErrorCodes.NotFound);

        var exists = await _fileObjectStore.ExistsAsync(tenantId, record.StoredName, ct);
        if (!exists)
        {
            throw new BusinessException("文件物理存储不存在。", ErrorCodes.NotFound);
        }

        var stream = await _fileObjectStore.OpenReadAsync(tenantId, record.StoredName, ct);
        var eTag = $"\"{record.Id}-{record.SizeBytes}-{record.UploadedAt.ToUnixTimeSeconds()}\"";
        return new FileDownloadResult(
            stream,
            record.ContentType,
            record.OriginalName,
            record.SizeBytes,
            eTag,
            record.UploadedAt);
    }

    public async Task<FileRecordDto?> GetInfoAsync(
        TenantId tenantId, long fileId, CancellationToken ct = default)
    {
        var record = await _fileRecordRepository.FindByIdAsync(tenantId, fileId, ct);
        if (record is null) return null;

        return new FileRecordDto(
            record.Id, record.OriginalName, record.ContentType, record.SizeBytes,
            record.UploadedById, record.UploadedByName, record.UploadedAt,
            record.VersionNumber, record.IsLatestVersion, record.PreviousVersionId);
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
        var options = _optionsMonitor.CurrentValue;
        ValidateExtension(request.OriginalName);
        if (request.TotalSizeBytes <= 0)
        {
            throw new BusinessException("文件大小无效。", ErrorCodes.ValidationError);
        }

        if (request.TotalSizeBytes > options.MaxFileSizeBytes)
        {
            throw new BusinessException(
                $"文件大小不能超过 {options.MaxFileSizeBytes / 1024 / 1024} MB。",
                ErrorCodes.ValidationError);
        }

        if (request.TotalParts <= 0)
        {
            throw new BusinessException("分片数量必须大于 0。", ErrorCodes.ValidationError);
        }

        var partSize = Math.Max(64 * 1024, options.ChunkPartSizeBytes);
        var ext = Path.GetExtension(request.OriginalName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var sessionId = _idGeneratorAccessor.NextId();
        var tenantDirectory = await GetTenantDirectoryAsync(tenantId, ct);
        var tempDirectory = Path.Combine(tenantDirectory, ".chunks", sessionId.ToString());
        Directory.CreateDirectory(tempDirectory);

        var expiresAt = _timeProvider.GetUtcNow().AddMinutes(options.ChunkSessionExpireMinutes);
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

        var mergedPath = Path.Combine(session.TempDirectory, $"{session.StoredName}.merge");
        await using (var destination = File.Create(mergedPath))
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

        await using (var mergedStream = File.OpenRead(mergedPath))
        {
            await _fileObjectStore.SaveAsync(tenantId, session.StoredName, mergedStream, request.ContentType, ct);
        }

        var now = _timeProvider.GetUtcNow();
        var fileId = _idGeneratorAccessor.NextId();
        var fileRecord = new FileRecord(
            tenantId,
            request.OriginalName,
            session.StoredName,
            request.ContentType,
            await ComputeSha256FromObjectStoreAsync(tenantId, session.StoredName, ct),
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

        var options = _optionsMonitor.CurrentValue;
        var ttl = expiresInSeconds > 0
            ? Math.Min(expiresInSeconds, 24 * 60 * 60)
            : options.SignedUrlDefaultExpireSeconds;
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

    public async Task<FileInstantCheckResult> CheckInstantUploadAsync(
        TenantId tenantId,
        string fileHashSha256,
        long sizeBytes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileHashSha256))
        {
            throw new BusinessException("文件哈希不能为空。", ErrorCodes.ValidationError);
        }

        var normalizedHash = fileHashSha256.Trim().ToLowerInvariant();
        var record = await _fileRecordRepository.FindByHashAsync(tenantId, normalizedHash, sizeBytes, ct);
        if (record is null)
        {
            return new FileInstantCheckResult(false, null, null, null, null);
        }

        return new FileInstantCheckResult(
            true,
            record.Id,
            record.OriginalName,
            record.ContentType,
            record.SizeBytes);
    }

    public async Task<IReadOnlyList<FileVersionHistoryItemDto>> GetVersionHistoryAsync(
        TenantId tenantId,
        long fileId,
        CancellationToken ct = default)
    {
        var versions = await _fileRecordRepository.ListVersionsByFileIdAsync(tenantId, fileId, ct);
        return versions.Select(x => new FileVersionHistoryItemDto(
            x.Id,
            x.VersionNumber,
            x.IsLatestVersion,
            x.OriginalName,
            x.ContentType,
            x.SizeBytes,
            x.UploadedById,
            x.UploadedByName,
            x.UploadedAt,
            x.PreviousVersionId)).ToList();
    }

    public async Task<FileTusUploadCreateResult> CreateTusUploadAsync(
        TenantId tenantId,
        long uploadedById,
        string uploadedByName,
        string originalName,
        string contentType,
        long totalSizeBytes,
        CancellationToken ct = default)
    {
        var options = _optionsMonitor.CurrentValue;
        ValidateExtension(originalName);
        if (totalSizeBytes <= 0)
        {
            throw new BusinessException("上传总大小必须大于 0。", ErrorCodes.ValidationError);
        }

        if (totalSizeBytes > options.MaxFileSizeBytes)
        {
            throw new BusinessException(
                $"文件大小不能超过 {options.MaxFileSizeBytes / 1024 / 1024} MB。",
                ErrorCodes.ValidationError);
        }

        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var sessionId = _idGeneratorAccessor.NextId();
        var tempDirectory = await GetTusTempDirectoryAsync(tenantId, ct);
        Directory.CreateDirectory(tempDirectory);
        var tempFilePath = Path.Combine(tempDirectory, $"{sessionId}.upload");
        await using (var _ = File.Create(tempFilePath))
        {
            // create empty file
        }

        var expiresAt = _timeProvider.GetUtcNow().AddMinutes(options.ChunkSessionExpireMinutes);
        var session = new FileTusUploadSession(
            tenantId,
            originalName,
            storedName,
            contentType,
            totalSizeBytes,
            tempFilePath,
            expiresAt,
            uploadedById,
            uploadedByName,
            sessionId);
        await _fileTusUploadSessionRepository.AddAsync(session, ct);

        return new FileTusUploadCreateResult(
            session.Id,
            $"/api/v1/files/tus/{session.Id}",
            session.UploadedSizeBytes,
            session.TotalSizeBytes,
            session.ExpiresAt);
    }

    public async Task<FileTusUploadStatusDto?> GetTusUploadStatusAsync(
        TenantId tenantId,
        long sessionId,
        CancellationToken ct = default)
    {
        var session = await _fileTusUploadSessionRepository.FindByIdAsync(tenantId, sessionId, ct);
        if (session is null)
        {
            return null;
        }

        if (session.ExpiresAt <= _timeProvider.GetUtcNow() && session.Status != FileTusUploadSessionStatus.Completed)
        {
            session.MarkExpired();
            await _fileTusUploadSessionRepository.UpdateAsync(session, ct);
        }

        return new FileTusUploadStatusDto(
            session.Id,
            session.UploadedSizeBytes,
            session.TotalSizeBytes,
            session.Status == FileTusUploadSessionStatus.Completed,
            session.ExpiresAt);
    }

    public async Task<FileTusUploadPatchResult> AppendTusUploadAsync(
        TenantId tenantId,
        long sessionId,
        long uploadOffset,
        Stream chunkStream,
        long chunkSizeBytes,
        CancellationToken ct = default)
    {
        var session = await _fileTusUploadSessionRepository.FindActiveByIdAsync(tenantId, sessionId, ct)
            ?? throw new BusinessException("Tus 上传会话不存在或已结束。", ErrorCodes.NotFound);
        if (session.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            session.MarkExpired();
            await _fileTusUploadSessionRepository.UpdateAsync(session, ct);
            throw new BusinessException("Tus 上传会话已过期。", ErrorCodes.ValidationError);
        }

        if (uploadOffset != session.UploadedSizeBytes)
        {
            throw new BusinessException("Tus 上传偏移量不匹配。", ErrorCodes.ValidationError);
        }

        var tempDirectory = Path.GetDirectoryName(session.TempFilePath);
        if (!string.IsNullOrWhiteSpace(tempDirectory))
        {
            Directory.CreateDirectory(tempDirectory);
        }

        long bytesWritten;
        await using (var destination = new FileStream(
                         session.TempFilePath,
                         FileMode.OpenOrCreate,
                         FileAccess.Write,
                         FileShare.Read))
        {
            destination.Seek(uploadOffset, SeekOrigin.Begin);
            bytesWritten = await CopyStreamAndCountAsync(chunkStream, destination, ct);
            await destination.FlushAsync(ct);
        }

        if (bytesWritten <= 0)
        {
            throw new BusinessException("Tus 上传分片大小无效。", ErrorCodes.ValidationError);
        }

        if (chunkSizeBytes > 0 && bytesWritten != chunkSizeBytes)
        {
            throw new BusinessException("Tus 上传分片大小与请求头不一致。", ErrorCodes.ValidationError);
        }

        var nextOffset = checked(uploadOffset + bytesWritten);
        if (nextOffset > session.TotalSizeBytes)
        {
            throw new BusinessException("Tus 上传分片超出文件总大小。", ErrorCodes.ValidationError);
        }

        session.UpdateUploadedSize(nextOffset);
        long? fileId = null;
        var completed = nextOffset == session.TotalSizeBytes;
        if (completed)
        {
            await using var mergedStream = File.OpenRead(session.TempFilePath);
            await _fileObjectStore.SaveAsync(tenantId, session.StoredName, mergedStream, session.ContentType, ct);
            var now = _timeProvider.GetUtcNow();
            fileId = _idGeneratorAccessor.NextId();
            var fileRecord = new FileRecord(
                tenantId,
                session.OriginalName,
                session.StoredName,
                session.ContentType,
                await ComputeSha256FromObjectStoreAsync(tenantId, session.StoredName, ct),
                session.TotalSizeBytes,
                session.UploadedByUserId,
                session.UploadedByName,
                now,
                fileId.Value);
            await _fileRecordRepository.AddAsync(fileRecord, ct);
            session.MarkCompleted(fileId.Value);
        }

        await _fileTusUploadSessionRepository.UpdateAsync(session, ct);

        if (completed)
        {
            TryDeleteFile(session.TempFilePath);
        }

        return new FileTusUploadPatchResult(
            session.Id,
            session.UploadedSizeBytes,
            session.TotalSizeBytes,
            completed,
            fileId);
    }

    // ===== Helpers =====

    private void ValidateExtension(string fileName)
    {
        var options = _optionsMonitor.CurrentValue;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        // 明确拒绝危险扩展名
        if (options.BlockedExtensions.Contains(ext))
        {
            throw new BusinessException(
                $"不允许上传 '{ext}' 类型的文件。", ErrorCodes.ValidationError);
        }

        // 若有白名单配置则必须在白名单内
        if (options.AllowedExtensions.Length > 0 && !options.AllowedExtensions.Contains(ext))
        {
            throw new BusinessException(
                $"不支持的文件类型 '{ext}'。", ErrorCodes.ValidationError);
        }
    }

    private async Task<string> GetTenantDirectoryAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var settings = await _settingsResolver.ResolveAsync(tenantId, cancellationToken);
        var contentRoot = _hostEnvironmentAccessor.ContentRootPath;
        return Path.Combine(contentRoot, settings.BasePath, tenantId.Value.ToString());
    }

    private async Task<string> GetTusTempDirectoryAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return Path.Combine(await GetTenantDirectoryAsync(tenantId, cancellationToken), ".tus");
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
        var secret = _optionsMonitor.CurrentValue.SignedUrlSecret;
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

    private async Task<string> ComputeSha256FromObjectStoreAsync(
        TenantId tenantId,
        string objectName,
        CancellationToken cancellationToken)
    {
        await using var stream = await _fileObjectStore.OpenReadAsync(tenantId, objectName, cancellationToken);
        return await ComputeSha256Async(stream, cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
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

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private static async Task<long> CopyStreamAndCountAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 64];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            total += read;
        }

        return total;
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
