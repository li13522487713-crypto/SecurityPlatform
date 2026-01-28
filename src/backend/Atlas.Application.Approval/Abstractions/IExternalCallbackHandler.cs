namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 外部回调处理器接口（用于扩展不同的回调实现方式，如 HTTP、消息队列等）
/// </summary>
public interface IExternalCallbackHandler
{
    /// <summary>
    /// 发送回调
    /// </summary>
    /// <param name="callbackUrl">回调 URL</param>
    /// <param name="requestBody">请求体（JSON）</param>
    /// <param name="headers">请求头（包含签名等信息）</param>
    /// <param name="timeoutSeconds">超时时间（秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应体（JSON）</returns>
    Task<string> SendCallbackAsync(
        string callbackUrl,
        string requestBody,
        Dictionary<string, string> headers,
        int timeoutSeconds,
        CancellationToken cancellationToken);
}
