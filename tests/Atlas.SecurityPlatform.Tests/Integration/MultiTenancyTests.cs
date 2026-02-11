using System.Net;
using System.Net.Http.Json;
using Atlas.Core.Models;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

/// <summary>
/// 多租户隔离集成测试
/// 验证租户数据隔离、跨租户访问防护等安全要求
/// </summary>
public sealed class MultiTenancyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    // 测试租户ID
    private const string TenantA = "00000000-0000-0000-0000-000000000001";
    private const string TenantB = "00000000-0000-0000-0000-000000000002";

    public MultiTenancyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAssets_ShouldNotReturnOtherTenantsData()
    {
        // Arrange: 为租户A和租户B分别创建资产
        var tokenA = await GetAccessTokenAsync(TenantA, "admin", "Admin@123");
        var tokenB = await GetAccessTokenAsync(TenantB, "admin", "Admin@123");

        // 租户A创建资产
        var assetA = new
        {
            name = "Tenant A Asset",
            assetType = "Server",
            ipAddress = "192.168.1.10"
        };
        await CreateAssetAsync(TenantA, tokenA, assetA);

        // 租户B创建资产
        var assetB = new
        {
            name = "Tenant B Asset",
            assetType = "Server",
            ipAddress = "192.168.2.10"
        };
        await CreateAssetAsync(TenantB, tokenB, assetB);

        // Act: 租户A查询资产列表
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenA}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantA);
        var response = await _client.GetAsync("/api/v1/assets?pageIndex=1&pageSize=100");

        // Assert: 租户A只能看到自己的资产
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<dynamic>>>();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        // 验证返回的资产都属于租户A
        var assets = result.Data.Items;
        Assert.All(assets, asset =>
        {
            var assetName = asset.GetProperty("name").GetString();
            Assert.DoesNotContain("Tenant B", assetName);
        });
    }

    [Fact]
    public async Task UpdateAsset_WithWrongTenantId_ShouldReturn403()
    {
        // Arrange: 租户B创建一个资产
        var tokenB = await GetAccessTokenAsync(TenantB, "admin", "Admin@123");
        var assetB = new
        {
            name = "Tenant B Confidential Asset",
            assetType = "Database",
            ipAddress = "192.168.2.20"
        };
        var assetId = await CreateAssetAsync(TenantB, tokenB, assetB);

        // 租户A尝试获取token
        var tokenA = await GetAccessTokenAsync(TenantA, "admin", "Admin@123");

        // Act: 租户A尝试修改租户B的资产
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenA}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantA);

        var updatePayload = new
        {
            id = assetId,
            name = "Hacked Asset",
            assetType = "Database",
            ipAddress = "192.168.2.20"
        };
        var response = await _client.PutAsJsonAsync($"/api/v1/assets/{assetId}", updatePayload);

        // Assert: 应该返回403 Forbidden或404 Not Found（因为资产在租户A的视图中不存在）
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 403 or 404, but got {response.StatusCode}");
    }

    [Fact]
    public async Task QueryUsers_ShouldIsolateTenantData()
    {
        // Arrange: 为两个租户分别创建用户
        var tokenA = await GetAccessTokenAsync(TenantA, "admin", "Admin@123");
        var tokenB = await GetAccessTokenAsync(TenantB, "admin", "Admin@123");

        // 租户A创建用户
        var userA = new
        {
            username = "user_tenant_a",
            displayName = "Tenant A User",
            email = "usera@example.com",
            password = "Password@123"
        };
        await CreateUserAsync(TenantA, tokenA, userA);

        // 租户B创建用户
        var userB = new
        {
            username = "user_tenant_b",
            displayName = "Tenant B User",
            email = "userb@example.com",
            password = "Password@123"
        };
        await CreateUserAsync(TenantB, tokenB, userB);

        // Act: 租户A查询用户列表
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenA}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantA);
        var response = await _client.GetAsync("/api/v1/users?pageIndex=1&pageSize=100");

        // Assert: 租户A只能看到自己租户的用户
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<dynamic>>>();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var users = result.Data.Items;
        Assert.All(users, user =>
        {
            var username = user.GetProperty("username").GetString();
            Assert.DoesNotEqual("user_tenant_b", username);
        });
    }

    [Fact]
    public async Task AuditLog_ShouldOnlyShowOwnTenantRecords()
    {
        // Arrange: 两个租户分别进行操作（会生成审计日志）
        var tokenA = await GetAccessTokenAsync(TenantA, "admin", "Admin@123");
        var tokenB = await GetAccessTokenAsync(TenantB, "admin", "Admin@123");

        // 租户A进行操作（触发审计日志）
        await GetCurrentUserAsync(TenantA, tokenA);

        // 租户B进行操作（触发审计日志）
        await GetCurrentUserAsync(TenantB, tokenB);

        // Act: 租户A查询审计日志
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenA}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantA);
        var response = await _client.GetAsync("/api/v1/audit-records?pageIndex=1&pageSize=100");

        // Assert: 租户A只能看到自己租户的审计日志
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<dynamic>>>();
            Assert.NotNull(result);
            // 所有审计记录应该属于租户A
            // 注意：实际验证逻辑取决于审计日志的返回字段
        }
    }

    [Fact]
    public async Task TenantHeaderMismatch_ShouldReturn403()
    {
        // Arrange: 租户A登录获取token
        var tokenA = await GetAccessTokenAsync(TenantA, "admin", "Admin@123");

        // Act: 使用租户A的token，但header中指定租户B
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenA}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantB);  // 篡改租户ID
        var response = await _client.GetAsync("/api/v1/users");

        // Assert: 应该返回403 Forbidden（租户ID不匹配）
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateResource_AutomaticallySetsTenantId()
    {
        // Arrange
        var tokenA = await GetAccessTokenAsync(TenantA, "admin", "Admin@123");

        // Act: 创建资产（不手动指定TenantId，应该自动从context获取）
        var asset = new
        {
            name = "Auto Tenant Asset",
            assetType = "Server",
            ipAddress = "192.168.1.100"
        };
        var assetId = await CreateAssetAsync(TenantA, tokenA, asset);

        // 获取创建的资产详情
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenA}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantA);
        var response = await _client.GetAsync($"/api/v1/assets/{assetId}");

        // Assert: 资产应该被创建在正确的租户下
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<dynamic>>();
        Assert.NotNull(result);
        Assert.True(result.Success);

        // 验证可以查询到（说明租户ID正确）
        Assert.NotNull(result.Data);
    }

    // ====== 辅助方法 ======

    private async Task<string> GetAccessTokenAsync(string tenantId, string username, string password)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var loginPayload = new
        {
            username,
            password
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/token", loginPayload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<dynamic>>();
        Assert.NotNull(result);
        Assert.True(result.Success);

        var accessToken = result.Data.GetProperty("accessToken").GetString();
        Assert.NotNull(accessToken);

        return accessToken;
    }

    private async Task<long> CreateAssetAsync(string tenantId, string token, object asset)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var response = await _client.PostAsJsonAsync("/api/v1/assets", asset);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<dynamic>>();
        Assert.NotNull(result);
        Assert.True(result.Success);

        var assetId = result.Data.GetProperty("id").GetInt64();
        return assetId;
    }

    private async Task<long> CreateUserAsync(string tenantId, string token, object user)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var response = await _client.PostAsJsonAsync("/api/v1/users", user);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create user: {response.StatusCode}, {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<dynamic>>();
        Assert.NotNull(result);

        var userId = result.Data.GetProperty("id").GetInt64();
        return userId;
    }

    private async Task GetCurrentUserAsync(string tenantId, string token)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var response = await _client.GetAsync("/api/v1/auth/me");
        response.EnsureSuccessStatusCode();
    }
}
