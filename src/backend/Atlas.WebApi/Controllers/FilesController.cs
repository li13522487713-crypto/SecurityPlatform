using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
namespace Atlas.WebApi.Controllers;

/// <summary>
/// 文件上传/下载管理（等保2.0：文件类型限制 + 访问鉴权 + 操作审计）
/// </summary>
[ApiController]
[Route("api/v1/files")]
[Authorize]
public sealed class FilesController : ControllerBase
{
    private const string TusResumable = "1.0.0";
    private readonly IFileStorageService _fileStorageService;
    private readonly IAttachmentService _attachmentService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;

    public FilesController(
        IFileStorageService fileStorageService,
        IAttachmentService attachmentService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder)
    {
        _fileStorageService = fileStorageService;
        _attachmentService = attachmentService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
    }

    /// <summary>Tus 协议探测</summary>
    [HttpOptions("tus")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    [SkipIdempotency]
    public IActionResult TusOptions()
    {
        Response.Headers.Append("Tus-Resumable", TusResumable);
        Response.Headers.Append("Tus-Version", TusResumable);
        Response.Headers.Append("Tus-Extension", "creation,creation-defer-length");
        return NoContent();
    }

    [HttpOptions("tus/{sessionId:long}")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    [SkipIdempotency]
    public IActionResult TusUploadOptions(long sessionId)
    {
        Response.Headers.Append("Tus-Resumable", TusResumable);
        Response.Headers.Append("Tus-Version", TusResumable);
        Response.Headers.Append("Tus-Extension", "creation,creation-defer-length");
        return NoContent();
    }

    /// <summary>Tus 创建上传会话</summary>
    [HttpPost("tus")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    [SkipIdempotency]
    public async Task<IActionResult> CreateTusUpload(CancellationToken cancellationToken = default)
    {
        if (!Request.Headers.TryGetValue("Upload-Length", out var uploadLengthValues)
            || !long.TryParse(uploadLengthValues.ToString(), out var uploadLength))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                "Tus Upload-Length 无效",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();
        var metadata = ParseTusUploadMetadata(
            Request.Headers["Upload-Metadata"].ToString(),
            $"upload-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bin",
            "application/octet-stream");
        var result = await _fileStorageService.CreateTusUploadAsync(
            tenantId,
            currentUser.UserId,
            currentUser.Username ?? currentUser.UserId.ToString(),
            metadata.OriginalName,
            metadata.ContentType,
            uploadLength,
            cancellationToken);

        Response.Headers.Append("Tus-Resumable", TusResumable);
        Response.Headers.Append("Location", $"/api/v1/files/tus/{result.SessionId}");
        Response.Headers.Append("Upload-Offset", result.UploadOffset.ToString());
        Response.Headers.Append("Upload-Length", result.UploadLength.ToString());
        Response.Headers.Append("Upload-Expires", result.ExpiresAt.ToString("R"));
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>Tus 查询上传状态</summary>
    [HttpHead("tus/{sessionId:long}")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    [SkipIdempotency]
    public async Task<IActionResult> GetTusUploadStatus(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var status = await _fileStorageService.GetTusUploadStatusAsync(tenantId, sessionId, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        Response.Headers.Append("Tus-Resumable", TusResumable);
        Response.Headers.Append("Upload-Offset", status.UploadOffset.ToString());
        Response.Headers.Append("Upload-Length", status.UploadLength.ToString());
        Response.Headers.Append("Upload-Expires", status.ExpiresAt.ToString("R"));
        Response.Headers.Append("Cache-Control", "no-store");
        return NoContent();
    }

    /// <summary>Tus 上传分片追加</summary>
    [HttpPatch("tus/{sessionId:long}")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    [SkipIdempotency]
    public async Task<IActionResult> AppendTusUpload(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Request.Headers["Tus-Resumable"], TusResumable, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status412PreconditionFailed, ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                "Tus-Resumable 请求头无效",
                HttpContext.TraceIdentifier));
        }

        if (string.IsNullOrWhiteSpace(Request.ContentType)
            || !Request.ContentType.StartsWith("application/offset+octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                "Tus PATCH Content-Type 必须为 application/offset+octet-stream",
                HttpContext.TraceIdentifier));
        }

        if (!Request.Headers.TryGetValue("Upload-Offset", out var uploadOffsetValues)
            || !long.TryParse(uploadOffsetValues.ToString(), out var uploadOffset))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                "Tus Upload-Offset 无效",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.AppendTusUploadAsync(
            tenantId,
            sessionId,
            uploadOffset,
            Request.Body,
            Request.ContentLength ?? 0,
            cancellationToken);

        Response.Headers.Append("Tus-Resumable", TusResumable);
        Response.Headers.Append("Upload-Offset", result.UploadOffset.ToString());
        if (result.FileId.HasValue)
        {
            Response.Headers.Append("Upload-File-Id", result.FileId.Value.ToString());
        }
        return NoContent();
    }

    /// <summary>上传文件</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB hard cap at HTTP level
    public async Task<ActionResult<ApiResponse<FileUploadResult>>> Upload(
        IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<FileUploadResult>.Fail(
                ErrorCodes.ValidationError, "未收到文件", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();

        await using var stream = file.OpenReadStream();
        var result = await _fileStorageService.UploadAsync(
            tenantId,
            currentUser.UserId,
            currentUser.Username ?? currentUser.UserId.ToString(),
            file.FileName,
            file.ContentType,
            stream,
            file.Length,
            cancellationToken);

        await RecordAuditAsync("UPLOAD_FILE", file.FileName, cancellationToken);
        return Ok(ApiResponse<FileUploadResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>获取文件元数据</summary>
    [HttpGet("{id:long}/info")]
    public async Task<ActionResult<ApiResponse<FileRecordDto>>> GetInfo(
        long id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var info = await _fileStorageService.GetInfoAsync(tenantId, id, cancellationToken);

        if (info is null)
        {
            return NotFound(ApiResponse<FileRecordDto>.Fail(
                ErrorCodes.NotFound, "文件不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<FileRecordDto>.Ok(info, HttpContext.TraceIdentifier));
    }

    /// <summary>秒传校验（按 SHA-256 哈希查重）</summary>
    [HttpGet("instant-check")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    public async Task<ActionResult<ApiResponse<FileInstantCheckResult>>> InstantCheck(
        [FromQuery] string sha256,
        [FromQuery] long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.CheckInstantUploadAsync(
            tenantId,
            sha256,
            sizeBytes,
            cancellationToken);
        return Ok(ApiResponse<FileInstantCheckResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>下载文件</summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.FileDownload)]
    public async Task<IActionResult> Download(
        long id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.DownloadAsync(tenantId, id, cancellationToken);
        return await BuildFileDownloadResultAsync(result, cancellationToken);
    }

    /// <summary>删除文件（软删除 + 审计）</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.FileDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _fileStorageService.DeleteAsync(tenantId, id, cancellationToken);

        await RecordAuditAsync("DELETE_FILE", id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>初始化分片上传会话</summary>
    [HttpPost("upload/init")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    public async Task<ActionResult<ApiResponse<FileChunkUploadInitResult>>> InitChunkUpload(
        [FromBody] FileChunkUploadInitRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();

        var result = await _fileStorageService.InitChunkUploadAsync(
            tenantId,
            currentUser.UserId,
            currentUser.Username ?? currentUser.UserId.ToString(),
            request,
            cancellationToken);
        await RecordAuditAsync("INIT_CHUNK_UPLOAD", request.OriginalName, cancellationToken);
        return Ok(ApiResponse<FileChunkUploadInitResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>上传分片</summary>
    [HttpPost("upload/{sessionId:long}/part/{partNumber:int}")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    public async Task<ActionResult<ApiResponse<object>>> UploadChunkPart(
        long sessionId,
        int partNumber,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError, "未收到分片文件", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await using var stream = file.OpenReadStream();
        await _fileStorageService.UploadChunkPartAsync(
            tenantId,
            sessionId,
            partNumber,
            stream,
            file.Length,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { SessionId = sessionId.ToString(), PartNumber = partNumber }, HttpContext.TraceIdentifier));
    }

    /// <summary>完成分片上传</summary>
    [HttpPost("upload/{sessionId:long}/complete")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    public async Task<ActionResult<ApiResponse<FileUploadResult>>> CompleteChunkUpload(
        long sessionId,
        [FromBody] FileChunkUploadCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.CompleteChunkUploadAsync(
            tenantId,
            sessionId,
            request,
            cancellationToken);
        await RecordAuditAsync("COMPLETE_CHUNK_UPLOAD", request.OriginalName, cancellationToken);
        return Ok(ApiResponse<FileUploadResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>分片上传进度</summary>
    [HttpGet("upload/{sessionId:long}/progress")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    public async Task<ActionResult<ApiResponse<FileUploadSessionProgressDto>>> GetChunkUploadProgress(
        long sessionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.GetChunkUploadProgressAsync(tenantId, sessionId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<FileUploadSessionProgressDto>.Fail(
                ErrorCodes.NotFound,
                "上传会话不存在",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<FileUploadSessionProgressDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>获取签名下载 URL</summary>
    [HttpGet("{id:long}/signed-url")]
    [Authorize(Policy = PermissionPolicies.FileDownload)]
    public async Task<ActionResult<ApiResponse<FileSignedUrlResult>>> GetSignedUrl(
        long id,
        [FromQuery] int expiresInSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.GenerateSignedUrlAsync(tenantId, id, expiresInSeconds, cancellationToken);
        return Ok(ApiResponse<FileSignedUrlResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>通过签名 URL 下载（无需登录）</summary>
    [HttpGet("signed/{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadBySignedUrl(
        long id,
        [FromQuery] Guid tenantId,
        [FromQuery] long expires,
        [FromQuery] string sig,
        CancellationToken cancellationToken = default)
    {
        var tenant = new TenantId(tenantId);
        var result = await _fileStorageService.DownloadBySignatureAsync(tenant, id, expires, sig, cancellationToken);
        return await BuildFileDownloadResultAsync(result, cancellationToken);
    }

    /// <summary>图片上传申请（返回分片会话）</summary>
    [HttpPost("images/apply")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    public async Task<ActionResult<ApiResponse<FileChunkUploadInitResult>>> ApplyImageUpload(
        [FromBody] FileImageUploadApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<FileChunkUploadInitResult>.Fail(
                ErrorCodes.ValidationError,
                "仅支持图片类型",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser()
            ?? throw new UnauthorizedAccessException();
        var initResult = await _fileStorageService.InitChunkUploadAsync(
            tenantId,
            currentUser.UserId,
            currentUser.Username ?? currentUser.UserId.ToString(),
            new FileChunkUploadInitRequest(
                request.OriginalName,
                request.ContentType,
                request.TotalSizeBytes,
                request.TotalParts),
            cancellationToken);
        return Ok(ApiResponse<FileChunkUploadInitResult>.Ok(initResult, HttpContext.TraceIdentifier));
    }

    /// <summary>图片上传提交</summary>
    [HttpPost("images/commit")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    public async Task<ActionResult<ApiResponse<FileUploadResult>>> CommitImageUpload(
        [FromBody] FileImageUploadCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.CompleteChunkUploadAsync(
            tenantId,
            request.SessionId,
            new FileChunkUploadCompleteRequest(request.OriginalName, request.ContentType),
            cancellationToken);
        await RecordAuditAsync("COMMIT_IMAGE_UPLOAD", request.OriginalName, cancellationToken);
        return Ok(ApiResponse<FileUploadResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>获取文件版本历史列表</summary>
    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = PermissionPolicies.FileDownload)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FileVersionHistoryItemDto>>>> GetVersionHistory(
        long id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var versions = await _fileStorageService.GetVersionHistoryAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FileVersionHistoryItemDto>>.Ok(versions, HttpContext.TraceIdentifier));
    }

    /// <summary>获取指定业务实体关联的所有附件（可按 fieldKey 过滤）</summary>
    [HttpGet("attachments/{entityType}/{entityId:long}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AttachmentBindingDto>>>> GetAttachments(
        string entityType,
        long entityId,
        [FromQuery] string? fieldKey,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _attachmentService.GetAttachmentsAsync(tenantId, entityType, entityId, fieldKey, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AttachmentBindingDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>绑定附件到业务实体</summary>
    [HttpPost("bind")]
    [Authorize(Policy = PermissionPolicies.FileUpload)]
    public async Task<ActionResult<ApiResponse<AttachmentBindingDto>>> BindAttachment(
        [FromBody] AttachmentBindRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _attachmentService.BindAsync(tenantId, request, cancellationToken);
        await RecordAuditAsync("BIND_ATTACHMENT", $"{request.EntityType}/{request.EntityId}", cancellationToken);
        return Ok(ApiResponse<AttachmentBindingDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>解绑附件与业务实体的关联</summary>
    [HttpDelete("unbind")]
    [Authorize(Policy = PermissionPolicies.FileDelete)]
    public async Task<ActionResult<ApiResponse<object>>> UnbindAttachment(
        [FromBody] AttachmentUnbindRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _attachmentService.UnbindAsync(tenantId, request, cancellationToken);
        await RecordAuditAsync("UNBIND_ATTACHMENT", $"{request.EntityType}/{request.EntityId}", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, HttpContext.TraceIdentifier));
    }

    private async Task RecordAuditAsync(string action, string target, CancellationToken ct)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null) return;

        var actor = string.IsNullOrWhiteSpace(currentUser.Username)
            ? currentUser.UserId.ToString()
            : currentUser.Username;

        var auditContext = new AuditContext(
            currentUser.TenantId,
            actor,
            action,
            "SUCCESS",
            target,
            ControllerHelper.GetIpAddress(HttpContext),
            ControllerHelper.GetUserAgent(HttpContext),
            _clientContextAccessor.GetCurrent());
        await _auditRecorder.RecordAsync(auditContext, ct);
    }

    private async Task<IActionResult> BuildFileDownloadResultAsync(
        FileDownloadResult result,
        CancellationToken cancellationToken)
    {
        Response.Headers["Accept-Ranges"] = "bytes";
        Response.Headers["ETag"] = result.ETag;
        Response.Headers["Last-Modified"] = result.LastModifiedAt.ToString("R");

        if (TryParseRange(Request.Headers.Range.ToString(), result.SizeBytes, out var rangeStart, out var rangeEnd)
            && IsIfRangeMatched(Request.Headers.IfRange.ToString(), result.ETag, result.LastModifiedAt))
        {
            var length = rangeEnd - rangeStart + 1;
            var partialStream = await CreatePartialStreamAsync(result.Stream, rangeStart, length, cancellationToken);
            Response.StatusCode = StatusCodes.Status206PartialContent;
            Response.Headers["Content-Range"] = $"bytes {rangeStart}-{rangeEnd}/{result.SizeBytes}";
            Response.Headers["Content-Length"] = length.ToString();
            return File(partialStream, result.ContentType, result.FileName);
        }

        Response.Headers["Content-Length"] = result.SizeBytes.ToString();
        return File(result.Stream, result.ContentType, result.FileName);
    }

    private static bool TryParseRange(string rangeHeader, long totalLength, out long start, out long end)
    {
        start = 0;
        end = totalLength - 1;
        if (string.IsNullOrWhiteSpace(rangeHeader)
            || !rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var range = rangeHeader["bytes=".Length..].Trim();
        var dashIndex = range.IndexOf('-');
        if (dashIndex <= 0 || dashIndex == range.Length - 1)
        {
            return false;
        }

        var startPart = range[..dashIndex];
        var endPart = range[(dashIndex + 1)..];
        if (!long.TryParse(startPart, out start)
            || !long.TryParse(endPart, out end))
        {
            return false;
        }

        if (start < 0 || end < start || end >= totalLength)
        {
            return false;
        }

        return true;
    }

    private static bool IsIfRangeMatched(string ifRange, string eTag, DateTimeOffset lastModifiedAt)
    {
        if (string.IsNullOrWhiteSpace(ifRange))
        {
            return true;
        }

        if (ifRange.StartsWith("\"", StringComparison.Ordinal)
            || ifRange.StartsWith("W/\"", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(ifRange, eTag, StringComparison.Ordinal);
        }

        if (DateTimeOffset.TryParse(ifRange, out var parsedDate))
        {
            return parsedDate >= lastModifiedAt;
        }

        return false;
    }

    private static async Task<Stream> CreatePartialStreamAsync(
        Stream source,
        long start,
        long length,
        CancellationToken cancellationToken)
    {
        if (source.CanSeek)
        {
            source.Seek(start, SeekOrigin.Begin);
            return new PartialReadStream(source, length);
        }

        await SkipBytesAsync(source, start, cancellationToken);
        return new PartialReadStream(source, length);
    }

    private static async Task SkipBytesAsync(Stream stream, long bytesToSkip, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        long skipped = 0;
        while (skipped < bytesToSkip)
        {
            var toRead = (int)Math.Min(buffer.Length, bytesToSkip - skipped);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            skipped += read;
        }
    }

    private static (string OriginalName, string ContentType) ParseTusUploadMetadata(
        string metadataHeader,
        string defaultName,
        string defaultContentType)
    {
        if (string.IsNullOrWhiteSpace(metadataHeader))
        {
            return (defaultName, defaultContentType);
        }

        string originalName = defaultName;
        string contentType = defaultContentType;
        var pairs = metadataHeader.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var tokens = pair.Split(' ', 2, StringSplitOptions.TrimEntries);
            if (tokens.Length != 2)
            {
                continue;
            }

            var key = tokens[0];
            var value = DecodeBase64Value(tokens[1]);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.Equals(key, "filename", StringComparison.OrdinalIgnoreCase))
            {
                originalName = value;
            }
            else if (string.Equals(key, "contentType", StringComparison.OrdinalIgnoreCase))
            {
                contentType = value;
            }
        }

        return (originalName, contentType);
    }

    private static string DecodeBase64Value(string encoded)
    {
        try
        {
            var bytes = Convert.FromBase64String(encoded);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class PartialReadStream : Stream
    {
        private readonly Stream _inner;
        private long _remaining;

        public PartialReadStream(Stream inner, long length)
        {
            _inner = inner;
            _remaining = length;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _remaining;
        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0)
            {
                return 0;
            }

            var allowed = (int)Math.Min(count, _remaining);
            var read = _inner.Read(buffer, offset, allowed);
            _remaining -= read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining <= 0)
            {
                return 0;
            }

            var allowed = (int)Math.Min(buffer.Length, _remaining);
            var read = await _inner.ReadAsync(buffer[..allowed], cancellationToken);
            _remaining -= read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
