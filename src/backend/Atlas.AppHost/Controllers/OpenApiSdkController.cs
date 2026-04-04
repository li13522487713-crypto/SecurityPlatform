using System.IO.Compression;
using System.Text;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/open-api-sdk")]
[Authorize]
public sealed class OpenApiSdkController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenApiSdkController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("openapi.json")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenView)]
    public async Task<IActionResult> DownloadOpenApi(CancellationToken cancellationToken)
    {
        var openApiJson = await FetchOpenApiJsonAsync(cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(openApiJson);
        return File(bytes, "application/json", "openapi.json");
    }

    [HttpGet("download")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenView)]
    public async Task<IActionResult> DownloadSdk(
        [FromQuery] string language = "typescript",
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = language.Trim().ToLowerInvariant();
        if (normalizedLanguage is not ("typescript" or "csharp"))
        {
            return BadRequest(ApiResponse<object>.Fail(
                ErrorCodes.ValidationError,
                "language 仅支持 typescript 或 csharp。",
                HttpContext.TraceIdentifier));
        }

        var openApiJson = await FetchOpenApiJsonAsync(cancellationToken);
        var readme = BuildReadme(normalizedLanguage);
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var openApiEntry = zip.CreateEntry("openapi.json", CompressionLevel.Fastest);
            await using (var stream = openApiEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(openApiJson);
            }

            var readmeEntry = zip.CreateEntry("README.md", CompressionLevel.Fastest);
            await using (var stream = readmeEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(readme);
            }
        }

        memory.Position = 0;
        var fileName = normalizedLanguage == "typescript" ? "atlas-sdk-typescript.zip" : "atlas-sdk-csharp.zip";
        return File(memory.ToArray(), "application/zip", fileName);
    }

    private async Task<string> FetchOpenApiJsonAsync(CancellationToken cancellationToken)
    {
        var origin = $"{Request.Scheme}://{Request.Host}";
        var openApiUrl = $"{origin}/swagger/v1/swagger.json";
        var client = _httpClientFactory.CreateClient("AiPlatform");
        using var response = await client.GetAsync(openApiUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("无法获取 OpenAPI 文档，请确认当前环境已启用 Swagger 文档。");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string BuildReadme(string language)
    {
        if (language == "typescript")
        {
            return """
            # Atlas Open API SDK (TypeScript)

            ## 生成命令

            ```bash
            npm install --save-dev openapi-typescript
            npx openapi-typescript ./openapi.json -o ./atlas-openapi.d.ts
            ```

            ## 调用示例

            使用 `POST /api/v1/open-api-projects/token` 交换访问令牌，然后携带：

            - `Authorization: Bearer <accessToken>`
            - `X-Tenant-Id: <tenant-guid>`
            """;
        }

        return """
        # Atlas Open API SDK (C#)

        ## 生成命令

        ```bash
        dotnet tool install --global NSwag.ConsoleCore
        nswag openapi2csclient /input:openapi.json /classname:AtlasOpenApiClient /namespace:Atlas.OpenApi /output:AtlasOpenApiClient.cs
        ```

        ## 调用示例

        使用 `POST /api/v1/open-api-projects/token` 交换访问令牌，然后携带：

        - `Authorization: Bearer <accessToken>`
        - `X-Tenant-Id: <tenant-guid>`
        """;
    }
}
