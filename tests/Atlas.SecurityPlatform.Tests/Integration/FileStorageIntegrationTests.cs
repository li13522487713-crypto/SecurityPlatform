using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class FileStorageIntegrationTests
{
    private readonly HttpClient _client;

    public FileStorageIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadThenRangeDownload_ShouldReturn206AndExpectedBytes()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var fileId = await UploadTextFileAsync(accessToken, csrfToken, "range-demo.txt", "hello atlas range");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{fileId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Range = new RangeHeaderValue(0, 4);

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Accept-Ranges", out var acceptRanges));
        Assert.Equal("bytes", acceptRanges!.Single());
        Assert.NotNull(response.Content.Headers.ContentRange);
        Assert.Equal(0, response.Content.Headers.ContentRange!.From);
        Assert.Equal(4, response.Content.Headers.ContentRange.To);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("hello", content);
    }

    [Fact]
    public async Task TusUpload_ShouldSupportCreateHeadAndPatch()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/tus");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        createRequest.Headers.Add("X-CSRF-TOKEN", csrfToken);
        createRequest.Headers.Add("Tus-Resumable", "1.0.0");
        createRequest.Headers.Add("Upload-Length", "14");
        createRequest.Headers.Add("Upload-Metadata", "filename dHVzLWRlbW8udHh0,contentType dGV4dC9wbGFpbg==");
        using var createResponse = await _client.SendAsync(createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.True(createResponse.Headers.TryGetValues("Location", out var locations));
        var location = locations!.Single();
        Assert.StartsWith("/api/v1/files/tus/", location, StringComparison.OrdinalIgnoreCase);
        var sessionId = long.Parse(location[(location.LastIndexOf('/') + 1)..]);

        using var headRequest = new HttpRequestMessage(HttpMethod.Head, $"/api/v1/files/tus/{sessionId}");
        headRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        headRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        headRequest.Headers.Add("Tus-Resumable", "1.0.0");
        using var headResponse = await _client.SendAsync(headRequest);
        Assert.Equal(HttpStatusCode.NoContent, headResponse.StatusCode);
        Assert.True(headResponse.Headers.TryGetValues("Upload-Offset", out var headOffsets));
        Assert.Equal("0", headOffsets!.Single());

        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/files/tus/{sessionId}");
        patchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        patchRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        patchRequest.Headers.Add("X-CSRF-TOKEN", csrfToken);
        patchRequest.Headers.Add("Tus-Resumable", "1.0.0");
        patchRequest.Headers.Add("Upload-Offset", "0");
        patchRequest.Content = new StringContent("tus demo data!", Encoding.UTF8, "application/offset+octet-stream");
        using var patchResponse = await _client.SendAsync(patchRequest);
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);
        Assert.True(patchResponse.Headers.TryGetValues("Upload-Offset", out var patchOffsets));
        Assert.Equal("14", patchOffsets!.Single());
    }

    [Fact]
    public async Task SignedUrlDownload_ShouldAllowAnonymousAccess()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var fileId = await UploadTextFileAsync(accessToken, csrfToken, "signed-url-demo.txt", "signed-url-body");

        using var signedUrlRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/files/{fileId}/signed-url?expiresInSeconds=120");
        signedUrlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        signedUrlRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var signedUrlResponse = await _client.SendAsync(signedUrlRequest);
        var payload = await ApiResponseAssert.ReadSuccessAsync(signedUrlResponse);
        var path = IntegrationAuthHelper.GetJsonString(payload.Data, "url");
        Assert.False(string.IsNullOrWhiteSpace(path));

        _client.DefaultRequestHeaders.Clear();
        using var anonymousDownloadResponse = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, anonymousDownloadResponse.StatusCode);
        var body = await anonymousDownloadResponse.Content.ReadAsStringAsync();
        Assert.Equal("signed-url-body", body);
    }

    [Fact]
    public async Task InstantCheck_ShouldReturnExistingFile()
    {
        const string body = "instant-check-body";
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var fileId = await UploadTextFileAsync(accessToken, csrfToken, "instant-check.txt", body);
        var hash = Sha256Hex(body);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/files/instant-check?sha256={hash}&sizeBytes={Encoding.UTF8.GetByteCount(body)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        using var response = await _client.SendAsync(request);

        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.Equal("true", IntegrationAuthHelper.GetJsonString(payload.Data, "exists"), ignoreCase: true);
        Assert.Equal(fileId.ToString(), IntegrationAuthHelper.GetJsonString(payload.Data, "fileId"));
    }

    private async Task<long> UploadTextFileAsync(
        string accessToken,
        string csrfToken,
        string fileName,
        string content)
    {
        using var contentStream = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        contentStream.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var form = new MultipartFormDataContent();
        form.Add(contentStream, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(request, csrfToken);

        using var response = await _client.SendAsync(request);
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        var idRaw = IntegrationAuthHelper.GetJsonString(payload.Data, "id");
        Assert.False(string.IsNullOrWhiteSpace(idRaw));
        return long.Parse(idRaw!);
    }

    private static string Sha256Hex(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
