using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

/// <summary>
/// 运行时文件控制器（M10 S10-1，**运行时 runtime 前缀** /api/runtime/files）。
///
/// 端点：
///  - POST :prepare-upload    颁发 token + 直传 URL
///  - POST :complete-upload   把 multipart/form-data 文件落到 IFileStorageService
///  - GET  /{handle}          下载（委托 IFileStorageService.DownloadAsync）
///  - DELETE /{handle}        软删除
///  - POST /sessions/{token}:cancel  取消 pending 会话
/// </summary>
[ApiController]
[Route("api/runtime/files")]
[Authorize]
public sealed class RuntimeFilesController : ControllerBase
{
    private readonly IRuntimeFileService _service;
    private readonly IFileStorageService _storage;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public RuntimeFilesController(IRuntimeFileService service, IFileStorageService storage, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _storage = storage;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost(":prepare-upload")]
    public async Task<ActionResult<ApiResponse<RuntimeFilePrepareUploadResponse>>> Prepare([FromBody] RuntimeFilePrepareUploadRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var r = await _service.PrepareAsync(tenantId, user.UserId, user.Username, request, cancellationToken);
        return Ok(ApiResponse<RuntimeFilePrepareUploadResponse>.Ok(r, HttpContext.TraceIdentifier));
    }

    /// <summary>multipart/form-data 上传：fields=token,file。</summary>
    [HttpPost(":complete-upload")]
    [RequestSizeLimit(200_000_000)]
    public async Task<ActionResult<ApiResponse<RuntimeFileCompleteUploadResponse>>> Complete([FromForm] string token, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) throw new BusinessException(ErrorCodes.ValidationError, "缺少 file 字段");
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await using var stream = file.OpenReadStream();
        var r = await _service.CompleteAsync(tenantId, user.UserId, user.Username, token, stream, file.Length, cancellationToken);
        return Ok(ApiResponse<RuntimeFileCompleteUploadResponse>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("sessions/{token}:cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(string token, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.CancelAsync(tenantId, user.UserId, token, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpGet("{handle}")]
    public async Task<IActionResult> Download(string handle, CancellationToken cancellationToken)
    {
        if (!long.TryParse(handle, out var id)) throw new BusinessException(ErrorCodes.ValidationError, "fileHandle 无效");
        var tenantId = _tenantProvider.GetTenantId();
        var d = await _storage.DownloadAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"文件不存在：{handle}");
        return File(d.Stream, d.ContentType, d.FileName);
    }

    [HttpDelete("{handle}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string handle, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.DeleteAsync(tenantId, user.UserId, handle, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
