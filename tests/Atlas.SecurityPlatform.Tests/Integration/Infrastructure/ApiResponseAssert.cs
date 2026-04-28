using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

public static class ApiResponseAssert
{
    public static async Task<ApiResponse<JsonElement>> ReadSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(cancellationToken);
        Assert.NotNull(payload);
        Assert.True(payload.Success, payload.Message);
        return payload;
    }

    public static async Task<ApiResponse<JsonElement>> ReadFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(cancellationToken);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        return payload;
    }

    public static string RequireString(JsonElement element, string propertyName)
    {
        var value = IntegrationAuthHelper.GetJsonString(element, propertyName);
        Assert.False(string.IsNullOrWhiteSpace(value), $"缺少属性: {propertyName}");
        return value!;
    }

    public static async Task<JsonElement> ReadCozeSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var root = document.RootElement.Clone();
        Assert.Equal(0, root.GetProperty("code").GetInt32());
        return root.GetProperty("data").Clone();
    }
}
