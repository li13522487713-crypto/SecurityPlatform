using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Helpers;
using Atlas.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers.Open;

[ApiController]
[Route("api/v1/open/files")]
[Authorize(AuthenticationSchemes = PatAuthenticationHandler.SchemeName)]
public sealed class OpenFilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ITenantProvider _tenantProvider;

    public OpenFilesController(IFileStorageService fileStorageService, ITenantProvider tenantProvider)
    {
        _fileStorageService = fileStorageService;
        _tenantProvider = tenantProvider;
    }

    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<FileUploadResult>>> Upload(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (!OpenScopeHelper.HasScope(User, "open:files:write"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<FileUploadResult>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:files:write 权限",
                HttpContext.TraceIdentifier));
        }

        if (file is null || file.Length <= 0)
        {
            return BadRequest(ApiResponse<FileUploadResult>.Fail(
                ErrorCodes.ValidationError,
                "未收到有效文件",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var userId = ControllerHelper.GetUserIdSafely(User) ?? 0L;
        var username = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? $"pat-{userId}";

        await using var stream = file.OpenReadStream();
        var result = await _fileStorageService.UploadAsync(
            tenantId,
            userId,
            username,
            file.FileName,
            file.ContentType,
            stream,
            file.Length,
            cancellationToken);
        return Ok(ApiResponse<FileUploadResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/info")]
    public async Task<ActionResult<ApiResponse<FileRecordDto>>> GetInfo(long id, CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:files:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<FileRecordDto>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:files:read 权限",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var info = await _fileStorageService.GetInfoAsync(tenantId, id, cancellationToken);
        if (info is null)
        {
            return NotFound(ApiResponse<FileRecordDto>.Fail(ErrorCodes.NotFound, "文件不存在", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<FileRecordDto>.Ok(info, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:files:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:files:read 权限",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _fileStorageService.DownloadAsync(tenantId, id, cancellationToken);
        return File(result.Stream, result.ContentType, result.FileName);
    }
}
