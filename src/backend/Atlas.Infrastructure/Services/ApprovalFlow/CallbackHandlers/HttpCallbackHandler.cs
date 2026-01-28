using Atlas.Application.Approval.Abstractions;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.ApprovalFlow.CallbackHandlers;

/// <summary>
/// HTTP 回调处理器实现
/// </summary>
public sealed class HttpCallbackHandler : IExternalCallbackHandler
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpCallbackHandler>? _logger;

    public HttpCallbackHandler(HttpClient httpClient, ILogger<HttpCallbackHandler>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> SendCallbackAsync(
        string callbackUrl,
        string requestBody,
        Dictionary<string, string> headers,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl);
        request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        // 添加自定义请求头
        foreach (var header in headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue; // Content-Type 已在上面设置
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("回调失败：URL={CallbackUrl}, HTTP={StatusCode}, 响应={ResponseBody}", callbackUrl, (int)response.StatusCode, responseBody);
                throw new HttpRequestException($"回调失败：HTTP {(int)response.StatusCode} {response.StatusCode}，响应：{responseBody}");
            }

            _logger?.LogInformation("回调成功：URL={CallbackUrl}, HTTP={StatusCode}", callbackUrl, (int)response.StatusCode);
            return responseBody;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger?.LogError(ex, "回调超时：URL={CallbackUrl}, 超时={TimeoutSeconds}秒", callbackUrl, timeoutSeconds);
            throw new TimeoutException($"回调超时：{timeoutSeconds}秒", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "回调异常：URL={CallbackUrl}", callbackUrl);
            throw;
        }
    }
}
