using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Actions.Http;

public interface IMicroflowRuntimeHttpClient
{
    Task<MicroflowRuntimeHttpResponse> SendAsync(
        MicroflowRuntimeHttpRequest request,
        MicroflowRuntimeHttpOptions options,
        CancellationToken ct);
}

public sealed class MicroflowRuntimeHttpClient : IMicroflowRuntimeHttpClient
{
    public const string HttpClientName = "microflow-runtime-rest";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MicroflowRestSecurityPolicy _securityPolicy;

    public MicroflowRuntimeHttpClient(
        IHttpClientFactory httpClientFactory,
        MicroflowRestSecurityPolicy securityPolicy)
    {
        _httpClientFactory = httpClientFactory;
        _securityPolicy = securityPolicy;
    }

    public async Task<MicroflowRuntimeHttpResponse> SendAsync(
        MicroflowRuntimeHttpRequest request,
        MicroflowRuntimeHttpOptions options,
        CancellationToken ct)
    {
        if (!options.AllowRealHttp)
        {
            return ErrorResponse(
                RuntimeErrorCode.RuntimeExternalCallBlocked,
                MicroflowRuntimeHttpErrorKind.SecurityBlocked,
                "External REST calls are blocked unless allowRealHttp=true and the runtime security policy allows the target.",
                Stopwatch.StartNew());
        }

        return await SendRealAsync(request, options, redirectCount: 0, ct);
    }

    private async Task<MicroflowRuntimeHttpResponse> SendRealAsync(
        MicroflowRuntimeHttpRequest request,
        MicroflowRuntimeHttpOptions options,
        int redirectCount,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timeoutSeconds = request.TimeoutSeconds.GetValueOrDefault(options.TimeoutSecondsDefault);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 300)));

        try
        {
            using var httpRequest = CreateHttpRequest(request);
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

            if (IsRedirect(response.StatusCode) && options.FollowRedirects)
            {
                if (redirectCount >= Math.Max(0, options.MaxRedirects))
                {
                    return ErrorResponse(
                        RuntimeErrorCode.RuntimeRestUrlBlocked,
                        MicroflowRuntimeHttpErrorKind.SecurityBlocked,
                        "REST redirect 次数超过限制。",
                        stopwatch);
                }

                var location = response.Headers.Location;
                if (location is null)
                {
                    return ErrorResponse(RuntimeErrorCode.RuntimeRestInvalidUrl, MicroflowRuntimeHttpErrorKind.SecurityBlocked, "REST redirect 缺少 Location。", stopwatch);
                }

                var nextUri = location.IsAbsoluteUri ? location : new Uri(new Uri(request.Url), location);
                var nextRequest = request with { Url = nextUri.AbsoluteUri };
                var decision = await _securityPolicy.EvaluateAsync(nextRequest, options, resolveHostAddresses: true, ct);
                if (!decision.Allowed)
                {
                    return ErrorResponse(decision.ReasonCode, MicroflowRuntimeHttpErrorKind.SecurityBlocked, decision.Message, stopwatch);
                }

                return await SendRealAsync(nextRequest, options, redirectCount + 1, ct);
            }

            var headers = CollectHeaders(response);
            var read = await ReadBodyAsync(response, options.MaxResponseBytes, timeoutCts.Token);
            stopwatch.Stop();
            var bodyJson = TryParseJson(read.Text);
            return new MicroflowRuntimeHttpResponse
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                Headers = headers,
                BodyText = read.Text,
                BodyJson = bodyJson,
                BodyPreview = MicroflowVariableStore.TrimPreview(read.Text, 500),
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                Truncated = read.Truncated,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                ContentLength = response.Content.Headers.ContentLength,
                Error = read.Truncated
                    ? new MicroflowRuntimeHttpError
                    {
                        Kind = MicroflowRuntimeHttpErrorKind.ResponseTooLarge,
                        Code = RuntimeErrorCode.RuntimeRestResponseTooLarge,
                        Message = $"REST response body exceeded maxResponseBytes={options.MaxResponseBytes}; body was truncated."
                    }
                    : null
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ErrorResponse(RuntimeErrorCode.RuntimeRestTimeout, MicroflowRuntimeHttpErrorKind.Timeout, $"REST call timed out after {timeoutSeconds} seconds.", stopwatch);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return ErrorResponse(RuntimeErrorCode.RuntimeCancelled, MicroflowRuntimeHttpErrorKind.Cancelled, "REST call cancelled.", stopwatch);
        }
        catch (HttpRequestException ex)
        {
            return ErrorResponse(RuntimeErrorCode.RuntimeRestCallFailed, MicroflowRuntimeHttpErrorKind.Network, ex.Message, stopwatch, ex.GetType().Name);
        }
    }

    private static HttpRequestMessage CreateHttpRequest(MicroflowRuntimeHttpRequest request)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
        foreach (var header in request.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                EnsureContent(message, request);
                message.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (!string.Equals(request.Method, HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.BodyKind, MicroflowRestBodyKind.None, StringComparison.OrdinalIgnoreCase))
        {
            message.Content = CreateContent(request);
        }

        return message;
    }

    private static HttpContent? CreateContent(MicroflowRuntimeHttpRequest request)
        => request.BodyKind switch
        {
            MicroflowRestBodyKind.Json => new StringContent(request.BodyText ?? request.BodyJson?.GetRawText() ?? "null", Encoding.UTF8, "application/json"),
            MicroflowRestBodyKind.Text => new StringContent(request.BodyText ?? string.Empty, Encoding.UTF8, "text/plain"),
            MicroflowRestBodyKind.Form => new FormUrlEncodedContent(request.FormFields),
            MicroflowRestBodyKind.Mapping => new StringContent(request.BodyText ?? string.Empty, Encoding.UTF8, "application/json"),
            _ => null
        };

    private static void EnsureContent(HttpRequestMessage message, MicroflowRuntimeHttpRequest request)
    {
        message.Content ??= CreateContent(request) ?? new StringContent(string.Empty, Encoding.UTF8);
    }

    private static IReadOnlyDictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        return headers;
    }

    private static async Task<(string Text, bool Truncated)> ReadBodyAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        var limit = Math.Clamp(maxBytes, 1, 10 * 1024 * 1024);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var truncated = false;
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), ct);
            if (read == 0)
            {
                break;
            }

            var remaining = limit - (int)buffer.Length;
            if (read > remaining)
            {
                buffer.Write(chunk, 0, Math.Max(0, remaining));
                truncated = true;
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return (Encoding.UTF8.GetString(buffer.ToArray()), truncated);
    }

    private static JsonElement? TryParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.Moved
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static MicroflowRuntimeHttpResponse ErrorResponse(
        string code,
        string kind,
        string message,
        Stopwatch stopwatch,
        string? details = null)
    {
        stopwatch.Stop();
        return new MicroflowRuntimeHttpResponse
        {
            Success = false,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            Error = new MicroflowRuntimeHttpError
            {
                Code = code,
                Kind = kind,
                Message = message,
                Details = details
            }
        };
    }
}
