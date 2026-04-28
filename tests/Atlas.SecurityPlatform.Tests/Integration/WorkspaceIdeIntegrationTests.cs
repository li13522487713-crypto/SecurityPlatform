using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class WorkspaceIdeIntegrationTests
{
    private readonly HttpClient _client;

    public WorkspaceIdeIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetWorkspaceIdeSummary_ShouldReturnSuccess()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/workspace-ide/summary");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.SendAsync(request);
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.True(payload.Data.TryGetProperty("appCount", out _));
        Assert.True(payload.Data.TryGetProperty("agentCount", out _));
    }

    [Fact]
    public async Task FavoriteWorkflowResource_ThenList_ShouldReturnCreatedResource()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"WorkspaceFav{Guid.NewGuid():N}"[..28]);

        using var favoriteRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/workspace-ide/favorites/workflow/{workflowId}");
        favoriteRequest.Headers.Authorization = new("Bearer", accessToken);
        favoriteRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(favoriteRequest, csrfToken);
        favoriteRequest.Content = JsonContent.Create(new { isFavorite = true });

        using var favoriteResponse = await _client.SendAsync(favoriteRequest);
        await ApiResponseAssert.ReadSuccessAsync(favoriteResponse);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/workspace-ide/resources?resourceType=workflow&favoriteOnly=true&pageIndex=1&pageSize=20");
        listRequest.Headers.Authorization = new("Bearer", accessToken);
        listRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var listResponse = await _client.SendAsync(listRequest);
        var listPayload = await ApiResponseAssert.ReadSuccessAsync(listResponse);
        var items = listPayload.Data.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items, item => string.Equals(item.GetProperty("resourceId").GetString(), workflowId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetOrganizationOverview_ShouldReturnSuccess()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var appId = await GetTenantAppInstanceIdAsync(accessToken);
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/tenant-app-instances/{appId}/organization/overview");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.True(payload.Success);
        Assert.True(payload.Data.TryGetProperty("memberCount", out _));
        Assert.True(payload.Data.TryGetProperty("recentMembers", out _));
    }

    [Fact]
    public async Task CreateWorkspaceApp_WithWorkspaceId_ShouldReturnSuccess()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workspaceId = await GetOrCreateWorkspaceIdAsync(accessToken, csrfToken);
        Assert.False(string.IsNullOrWhiteSpace(workspaceId));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/workspace-ide/apps");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(request, csrfToken);
        request.Content = JsonContent.Create(new
        {
            name = $"WorkspaceApp{Guid.NewGuid():N}"[..24],
            description = "workspace ide integration create app",
            workspaceId
        });

        using var response = await _client.SendAsync(request);
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        var appId = payload.Data.GetProperty("appId").ToString();
        var workflowId = payload.Data.GetProperty("workflowId").ToString();
        Assert.False(string.IsNullOrWhiteSpace(appId));
        Assert.False(string.IsNullOrWhiteSpace(workflowId));
    }

    private async Task<string?> GetTenantAppInstanceIdAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/tenant-app-instances?pageIndex=1&pageSize=20");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.SendAsync(request);
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        if (!payload.Data.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = itemsElement.EnumerateArray().ToArray();
        if (items.Length == 0)
        {
            return null;
        }

        var appId = items[0].GetProperty("id").GetString();
        return string.IsNullOrWhiteSpace(appId) ? null : appId;
    }

    private async Task<string?> GetOrCreateWorkspaceIdAsync(string accessToken, string csrfToken)
    {
        var existingWorkspaceId = await GetFirstWorkspaceIdAsync(accessToken);
        if (!string.IsNullOrWhiteSpace(existingWorkspaceId))
        {
            return existingWorkspaceId;
        }

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/workspaces");
        createRequest.Headers.Authorization = new("Bearer", accessToken);
        createRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(createRequest, csrfToken);
        createRequest.Content = JsonContent.Create(new
        {
            name = $"Workspace{Guid.NewGuid():N}"[..24],
            description = "workspace ide integration seed workspace"
        });

        using var createResponse = await _client.SendAsync(createRequest);
        var createPayload = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.IsSuccessStatusCode, createPayload);

        using var createDocument = JsonDocument.Parse(createPayload);
        if (TryReadId(createDocument.RootElement, out var createdWorkspaceId))
        {
            return createdWorkspaceId;
        }

        return null;
    }

    private async Task<string?> GetFirstWorkspaceIdAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/workspaces?page_num=1&page_size=20");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var dataElement = root;
        if (root.TryGetProperty("data", out var nestedData) && nestedData.ValueKind == JsonValueKind.Object)
        {
            dataElement = nestedData;
        }

        if (!dataElement.TryGetProperty("workspaces", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = itemsElement.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryReadId(first, out var workspaceId))
        {
            return workspaceId;
        }

        return null;
    }

    private static bool TryReadId(JsonElement element, out string? id)
    {
        if (element.TryGetProperty("id", out var idElement))
        {
            id = idElement.ToString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return true;
            }
        }

        if (element.TryGetProperty("Id", out var idUpperElement))
        {
            id = idUpperElement.ToString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return true;
            }
        }

        if (element.TryGetProperty("workspace_id", out var workspaceIdElement))
        {
            id = workspaceIdElement.ToString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return true;
            }
        }

        if (element.TryGetProperty("data", out var nestedData) && nestedData.ValueKind == JsonValueKind.Object)
        {
            return TryReadId(nestedData, out id);
        }

        id = null;
        return false;
    }

    private async Task<string> CreateWorkflowAsync(string accessToken, string csrfToken, string name)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows");
        createRequest.Headers.Authorization = new("Bearer", accessToken);
        createRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        createRequest.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(createRequest, csrfToken);
        createRequest.Content = JsonContent.Create(new
        {
            name,
            description = "workspace ide integration",
            mode = 0
        });

        using var createResponse = await _client.SendAsync(createRequest);
        var payload = await ApiResponseAssert.ReadSuccessAsync(createResponse);
        var workflowId = payload.Data.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(workflowId));
        return workflowId!;
    }
}
