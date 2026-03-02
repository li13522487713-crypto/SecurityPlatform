using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 文件上传/下载管理（等保2.0：文件类型限制 + 访问鉴权 + 操作审计）
/// </summary>
[ApiController]
[Route("api/v1/files")]
[Authorize]
public sealed class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IClientContextAccessor _clientContextAccessor;
    private readonly IAuditRecorder _auditRecorder;

    public FilesController(
        IFileStorageService fileStorageService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IClientContextAccessor clientContextAccessor,
        IAuditRecorder auditRecorder)
    {
        _fileStorageService = fileStorageService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _clientContextAccessor = clientContextAccessor;
        _auditRecorder = auditRecorder;
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

    /// <summary>下载文件</summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.FileDownload)]
    public async Task<IActionResult> Download(
        long id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.DownloadAsync(tenantId, id, cancellationToken);

        var encodedName = Uri.EscapeDataString(result.FileName);
        Response.Headers.Append("Content-Disposition", $"attachment; filename*=UTF-8''{encodedName}");
        return File(result.Stream, result.ContentType);
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
}
