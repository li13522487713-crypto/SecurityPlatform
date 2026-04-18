using System.IO;
using Atlas.Application.ExternalConnectors.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Connectors;

/// <summary>
/// 统一入站 webhook 端点：/api/v1/connectors/providers/{providerId}/callbacks/{topic}。
/// 匿名访问；身份依赖 verifier 的签名 + 解密；幂等由 ConnectorCallbackInboxService 保证。
/// </summary>
[ApiController]
[Route("api/v1/connectors/providers/{providerId:long}/callbacks")]
public sealed class ConnectorCallbacksController : ControllerBase
{
    private readonly IConnectorCallbackInboxService _inbox;

    public ConnectorCallbacksController(IConnectorCallbackInboxService inbox)
    {
        _inbox = inbox;
    }

    [HttpPost("{topic}")]
    [AllowAnonymous]
    public async Task<ActionResult<ConnectorCallbackInboxResult>> ReceiveAsync(long providerId, string topic, CancellationToken cancellationToken)
    {
        // 读取 raw body，便于 verifier 做 SHA1/AES 校验
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var body = ms.ToArray();

        var query = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var headers = Request.Headers.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = await _inbox.AcceptAsync(providerId, topic, query, headers, body, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
