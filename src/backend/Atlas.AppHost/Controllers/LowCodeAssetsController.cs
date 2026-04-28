using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/lowcode/assets")]
[Authorize]
public sealed class LowCodeAssetsController : ControllerBase
{
    private readonly IRuntimeFileService _runtimeFileService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeAssetsController(
        IRuntimeFileService runtimeFileService,
        IFileStorageService fileStorageService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUser)
    {
        _runtimeFileService = runtimeFileService;
        _fileStorageService = fileStorageService;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpPost("upload-session")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<RuntimeFilePrepareUploadResponse>>> CreateUploadSession(
        [FromBody] RuntimeFilePrepareUploadRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var response = await _runtimeFileService.PrepareAsync(tenantId, user.UserId, user.Username, request, cancellationToken);
        return Ok(ApiResponse<RuntimeFilePrepareUploadResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpPost("complete")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    [RequestSizeLimit(200_000_000)]
    public async Task<ActionResult<ApiResponse<RuntimeFileCompleteUploadResponse>>> Complete(
        [FromForm] string token,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "缺少 file 字段");
        }

        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await using var stream = file.OpenReadStream();
        var response = await _runtimeFileService.CompleteAsync(tenantId, user.UserId, user.Username, token, stream, file.Length, cancellationToken);
        return Ok(ApiResponse<RuntimeFileCompleteUploadResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<LowCodeAssetDescriptorDto>>> Get(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var info = await _fileStorageService.GetInfoAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"资产不存在：{id}");
        var dto = new LowCodeAssetDescriptorDto(
            FileHandle: info.Id.ToString(),
            FileName: info.OriginalName,
            ContentType: info.ContentType,
            SizeBytes: info.SizeBytes,
            Url: $"/api/runtime/files/{info.Id}",
            UploadedAt: info.UploadedAt,
            ETag: null);
        return Ok(ApiResponse<LowCodeAssetDescriptorDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _runtimeFileService.DeleteAsync(tenantId, user.UserId, id.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
