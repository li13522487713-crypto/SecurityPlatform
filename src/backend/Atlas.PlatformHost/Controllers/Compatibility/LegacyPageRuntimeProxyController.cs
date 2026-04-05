using System.Net.Http.Headers;
using Atlas.Presentation.Shared.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Compatibility;

[ApiController]
[Route("api/v1/runtime")]
[Obsolete("Legacy runtime compatibility API will be removed after migration to /app-host/{appKey}/* routes.")]
[DeprecatedApi("legacy runtime proxy endpoints are in compatibility window", "/app-host/{appKey}/api/app/runtime/*", "2026-10-31T00:00:00Z")]
public sealed class LegacyPageRuntimeProxyController : ControllerBase
{
    private static readonly string[] ForwardedHeaders =
    [
        "Authorization",
        "Accept-Language",
        "X-Tenant-Id",
        "Idempotency-Key",
        "X-CSRF-TOKEN",
        "Cookie"
    ];

    private readonly IHttpClientFactory httpClientFactory;

    public LegacyPageRuntimeProxyController(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    [HttpGet("apps/{appKey}/pages/{pageKey}/schema")]
    public Task<IActionResult> GetSchema(string appKey, string pageKey, CancellationToken cancellationToken)
    {
        return ProxyAsync(HttpMethod.Get, BuildTargetPath(pageKey, "schema"), cancellationToken);
    }

    [HttpGet("apps/{appKey}/menu")]
    public Task<IActionResult> GetRuntimeMenu(string appKey, CancellationToken cancellationToken)
    {
        return ProxyAsync(HttpMethod.Get, BuildRuntimeMenuTargetPath(appKey), cancellationToken);
    }

    [HttpGet("apps/{appKey}/pages/{pageKey}/records")]
    public Task<IActionResult> QueryRecords(string appKey, string pageKey, CancellationToken cancellationToken)
    {
        return ProxyAsync(HttpMethod.Get, BuildTargetPath(pageKey, "records"), cancellationToken);
    }

    [HttpGet("apps/{appKey}/pages/{pageKey}/records/{id:long}")]
    public Task<IActionResult> GetRecord(string appKey, string pageKey, long id, CancellationToken cancellationToken)
    {
        return ProxyAsync(HttpMethod.Get, BuildTargetPath(pageKey, $"records/{id}"), cancellationToken);
    }

    [HttpPost("apps/{appKey}/pages/{pageKey}/records")]
    public Task<IActionResult> CreateRecord(string appKey, string pageKey, CancellationToken cancellationToken)
    {
        return ProxyAsync(HttpMethod.Post, BuildTargetPath(pageKey, "records"), cancellationToken);
    }

    [HttpPut("apps/{appKey}/pages/{pageKey}/records/{id:long}")]
    public Task<IActionResult> UpdateRecord(string appKey, string pageKey, long id, CancellationToken cancellationToken)
    {
        return ProxyAsync(HttpMethod.Put, BuildTargetPath(pageKey, $"records/{id}"), cancellationToken);
    }

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string targetPath, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("app-runtime-proxy");
        var request = new HttpRequestMessage(method, $"{targetPath}{Request.QueryString}");

        foreach (var headerName in ForwardedHeaders)
        {
            if (!Request.Headers.TryGetValue(headerName, out var values))
            {
                continue;
            }

            _ = request.Headers.TryAddWithoutValidation(headerName, values.AsEnumerable());
        }

        if (Request.ContentLength > 0)
        {
            var streamContent = new StreamContent(Request.Body);
            if (!string.IsNullOrWhiteSpace(Request.ContentType))
            {
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(Request.ContentType);
            }

            request.Content = streamContent;
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json; charset=utf-8";
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (content.Length == 0)
        {
            return StatusCode((int)response.StatusCode);
        }

        Response.StatusCode = (int)response.StatusCode;
        return File(content, contentType);
    }

    private static string BuildTargetPath(string pageKey, string suffix)
    {
        return $"/api/app/runtime/pages/{Uri.EscapeDataString(pageKey)}/{suffix}";
    }

    private static string BuildRuntimeMenuTargetPath(string appKey)
    {
        return $"/api/v1/runtime/apps/{Uri.EscapeDataString(appKey)}/menu";
    }
}
