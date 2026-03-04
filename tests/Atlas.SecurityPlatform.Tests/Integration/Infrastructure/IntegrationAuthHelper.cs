using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

public static class IntegrationAuthHelper
{
    public sealed record AuthTokens(string AccessToken, string RefreshToken);

    public const string DefaultTenantId = "00000000-0000-0000-0000-000000000001";
    public const string DefaultUsername = "admin";
    public const string DefaultPassword = "P@ssw0rd!";

    public static async Task<AuthTokens> LoginAsync(
        HttpClient client,
        string tenantId = DefaultTenantId,
        string username = DefaultUsername,
        string password = DefaultPassword,
        string? totpCode = null,
        CancellationToken cancellationToken = default)
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var payload = new
        {
            username,
            password,
            totpCode
        };

        using var response = await client.PostAsJsonAsync("/api/v1/auth/token", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(cancellationToken);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEqual(JsonValueKind.Undefined, result.Data.ValueKind);
        Assert.NotEqual(JsonValueKind.Null, result.Data.ValueKind);

        var accessToken = GetJsonString(result.Data, "accessToken");
        var refreshToken = GetJsonString(result.Data, "refreshToken");

        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        return new AuthTokens(accessToken!, refreshToken!);
    }

    public static async Task<string> LoginAndGetAccessTokenAsync(
        HttpClient client,
        string tenantId = DefaultTenantId,
        string username = DefaultUsername,
        string password = DefaultPassword,
        string? totpCode = null,
        CancellationToken cancellationToken = default)
    {
        var tokens = await LoginAsync(client, tenantId, username, password, totpCode, cancellationToken);
        return tokens.AccessToken;
    }

    public static void SetAuthorizationHeaders(HttpClient client, string accessToken, string tenantId = DefaultTenantId)
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
    }

    internal static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value))
        {
            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.ToString();
        }

        var pascal = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (element.TryGetProperty(pascal, out var pascalValue))
        {
            return pascalValue.ValueKind == JsonValueKind.String
                ? pascalValue.GetString()
                : pascalValue.ToString();
        }

        return null;
    }
}
