using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

public static class CsrfIdempotencyHelper
{
    public static string NewIdempotencyKey() => Guid.NewGuid().ToString("N");

    public static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string accessToken,
        string tenantId = IntegrationAuthHelper.DefaultTenantId,
        CancellationToken cancellationToken = default)
    {
        IntegrationAuthHelper.SetAuthorizationHeaders(client, accessToken, tenantId);

        using var response = await client.GetAsync("/api/v1/secure/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(cancellationToken);
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.NotEqual(JsonValueKind.Undefined, payload.Data.ValueKind);
        Assert.NotEqual(JsonValueKind.Null, payload.Data.ValueKind);

        var token = IntegrationAuthHelper.GetJsonString(payload.Data, "token");
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    public static void AddWriteSecurityHeaders(
        HttpRequestMessage request,
        string csrfToken,
        string? idempotencyKey = null)
    {
        request.Headers.Remove("Idempotency-Key");
        request.Headers.Remove("X-CSRF-TOKEN");
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? NewIdempotencyKey());
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
    }
}
