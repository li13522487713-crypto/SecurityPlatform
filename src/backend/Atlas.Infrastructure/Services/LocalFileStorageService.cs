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

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 本地磁盘文件存储实现（等保2.0：隔离目录 + 类型白名单 + 软删除审计）
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly FileRecordRepository _fileRecordRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IHostEnvironmentAccessor _hostEnvironmentAccessor;

    public LocalFileStorageService(
        IOptions<FileStorageOptions> options,
        FileRecordRepository fileRecordRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider,
        IHostEnvironmentAccessor hostEnvironmentAccessor)
    {
        _options = options.Value;
        _fileRecordRepository = fileRecordRepository;
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
