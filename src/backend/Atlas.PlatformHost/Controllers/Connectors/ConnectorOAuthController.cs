using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Connectors;

/// <summary>
/// OAuth start / callback：
/// - start 需要登录态（在工作空间中点击"绑定我的企微账号"），用于生成跳转链接；
/// - callback 由外部平台跳回，匿名访问；state 一次性消费 + 跨租户校验在服务层完成。
/// </summary>
[ApiController]
[Route("api/v1/connectors/oauth")]
public sealed class ConnectorOAuthController : ControllerBase
{
    private readonly IConnectorOAuthFlowService _flowService;
    private readonly IValidator<OAuthInitiationRequest> _initiationValidator;
    private readonly IValidator<OAuthCallbackRequest> _callbackValidator;

    public ConnectorOAuthController(
        IConnectorOAuthFlowService flowService,
        IValidator<OAuthInitiationRequest> initiationValidator,
        IValidator<OAuthCallbackRequest> callbackValidator)
    {
        _flowService = flowService;
        _initiationValidator = initiationValidator;
        _callbackValidator = callbackValidator;
    }

    [HttpPost("start")]
    [Authorize]
    public async Task<ActionResult<OAuthInitiationResponse>> StartAsync([FromBody] OAuthInitiationRequest request, CancellationToken cancellationToken)
    {
        await _initiationValidator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
        var response = await _flowService.InitiateAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(response);
    }

    /// <summary>
    /// 由外部 OAuth 提供方跳回的回调端点。允许匿名调用：身份由 state 票据 + 一次性消费保证。
    /// </summary>
    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<ActionResult<OAuthCallbackResult>> CallbackAsync([FromBody] OAuthCallbackRequest request, CancellationToken cancellationToken)
    {
        await _callbackValidator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
        var result = await _flowService.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
