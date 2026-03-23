using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/multimodal")]
[Authorize]
public sealed class MultimodalController : ControllerBase
{
    private readonly IMultimodalService _multimodalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<MultimodalAssetCreateRequest> _createAssetValidator;
    private readonly IValidator<VisionAnalyzeRequest> _visionValidator;
    private readonly IValidator<AsrTranscribeRequest> _asrValidator;
    private readonly IValidator<TtsSynthesizeRequest> _ttsValidator;

    public MultimodalController(
        IMultimodalService multimodalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<MultimodalAssetCreateRequest> createAssetValidator,
        IValidator<VisionAnalyzeRequest> visionValidator,
        IValidator<AsrTranscribeRequest> asrValidator,
        IValidator<TtsSynthesizeRequest> ttsValidator)
    {
        _multimodalService = multimodalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createAssetValidator = createAssetValidator;
        _visionValidator = visionValidator;
        _asrValidator = asrValidator;
        _ttsValidator = ttsValidator;
    }

    [HttpPost("assets")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<object>>> CreateAsset(
        [FromBody] MultimodalAssetCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createAssetValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _multimodalService.CreateAssetAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("assets/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<MultimodalAssetDto>>> GetAsset(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _multimodalService.GetAssetAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<MultimodalAssetDto>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "RecordNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<MultimodalAssetDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("vision/analyze")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<VisionAnalyzeResult>>> AnalyzeVision(
        [FromBody] VisionAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        _visionValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _multimodalService.AnalyzeVisionAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<VisionAnalyzeResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("asr/transcribe")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<AsrTranscribeResult>>> Transcribe(
        [FromBody] AsrTranscribeRequest request,
        CancellationToken cancellationToken)
    {
        _asrValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _multimodalService.TranscribeAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<AsrTranscribeResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("tts/synthesize")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<TtsSynthesizeResult>>> Synthesize(
        [FromBody] TtsSynthesizeRequest request,
        CancellationToken cancellationToken)
    {
        _ttsValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _multimodalService.SynthesizeAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<TtsSynthesizeResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
