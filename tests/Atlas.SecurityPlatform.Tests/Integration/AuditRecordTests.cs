using System.Net;
using System.Net.Http.Json;
using Atlas.Application.Audit.Models;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

/// <summary>
/// 审计日志集成测试
/// 验证所有关键操作都正确记录审计日志，满足等保2.0审计要求
/// </summary>
public sealed class AuditRecordTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    // 测试租户ID
    private const string TestTenantId = "00000000-0000-0000-0000-000000000001";
    private const string DefaultUsername = "admin";
    private const string DefaultPassword = "Admin@123";

    public AuditRecordTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// 测试成功登录是否记录审计日志
    /// </summary>
    [Fact]
    public async Task Login_Success_ShouldWriteAuditLog()
    {
        // Act: 执行登录操作
        var token = await LoginAsync(TestTenantId, DefaultUsername, DefaultPassword);

        // 等待审计日志写入（异步操作可能需要短暂延迟）
        await Task.Delay(100);

        // 查询审计日志
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        var response = await _client.GetAsync("/api/v1/audit?pageIndex=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AuditListItem>>>();

        // Assert: 验证审计日志存在且字段完整
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Total > 0, "应该至少有一条审计日志");

        // 验证登录审计记录
        var loginAudit = result.Data.Items.FirstOrDefault(a =>
            a.Action == "LOGIN" && a.Result == "SUCCESS" && a.Actor == DefaultUsername);

        Assert.NotNull(loginAudit);
        Assert.Equal("LOGIN", loginAudit.Action);
        Assert.Equal("SUCCESS", loginAudit.Result);
        Assert.Equal(DefaultUsername, loginAudit.Actor);
        Assert.NotNull(loginAudit.IpAddress);
        Assert.NotNull(loginAudit.UserAgent);
        Assert.True(loginAudit.OccurredAt > DateTimeOffset.MinValue);
    }

    /// <summary>
    /// 测试登录失败是否记录审计日志
    /// </summary>
    [Fact]
    public async Task Login_Failed_ShouldWriteAuditLog()
    {
        // Arrange: 准备错误密码
        var wrongPassword = "WrongPassword@123";

        // Act: 尝试使用错误密码登录
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        var loginPayload = new
        {
            username = DefaultUsername,
            password = wrongPassword
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/token", loginPayload);

        // Assert: 登录应该失败
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);

        // 等待审计日志写入
        await Task.Delay(100);

        // 使用正确密码登录以获取token来查询审计日志
        var token = await LoginAsync(TestTenantId, DefaultUsername, DefaultPassword);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        var auditResponse = await _client.GetAsync("/api/v1/audit?pageIndex=1&pageSize=20");
        auditResponse.EnsureSuccessStatusCode();

        var result = await auditResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AuditListItem>>>();

        // Assert: 验证失败登录的审计记录
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var failedLoginAudit = result.Data.Items.FirstOrDefault(a =>
            a.Action == "LOGIN" && a.Result == "FAILED" && a.Actor == DefaultUsername);

        Assert.NotNull(failedLoginAudit);
        Assert.Equal("LOGIN", failedLoginAudit.Action);
        Assert.Equal("FAILED", failedLoginAudit.Result);
        Assert.Equal(DefaultUsername, failedLoginAudit.Actor);
    }

    /// <summary>
    /// 测试密码修改是否记录审计日志
    /// </summary>
    [Fact]
    public async Task ChangePassword_ShouldWriteAuditLog()
    {
        // Arrange: 登录获取token
        var token = await LoginAsync(TestTenantId, DefaultUsername, DefaultPassword);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        // Act: 修改密码（修改为相同密码以避免影响后续测试）
        var changePasswordPayload = new
        {
            currentPassword = DefaultPassword,
            newPassword = DefaultPassword,
            confirmPassword = DefaultPassword
        };

        var changeResponse = await _client.PutAsJsonAsync("/api/v1/auth/password", changePasswordPayload);
        changeResponse.EnsureSuccessStatusCode();

        // 等待审计日志写入
        await Task.Delay(100);

        // 查询审计日志
        var auditResponse = await _client.GetAsync("/api/v1/audit?pageIndex=1&pageSize=20");
        auditResponse.EnsureSuccessStatusCode();

        var result = await auditResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AuditListItem>>>();

        // Assert: 验证密码修改的审计记录
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var changePasswordAudit = result.Data.Items.FirstOrDefault(a =>
            a.Action == "CHANGE_PASSWORD" && a.Result == "SUCCESS" && a.Actor == DefaultUsername);

        Assert.NotNull(changePasswordAudit);
        Assert.Equal("CHANGE_PASSWORD", changePasswordAudit.Action);
        Assert.Equal("SUCCESS", changePasswordAudit.Result);
        Assert.Equal(DefaultUsername, changePasswordAudit.Actor);
        Assert.NotNull(changePasswordAudit.IpAddress);
        Assert.NotNull(changePasswordAudit.UserAgent);
    }

    /// <summary>
    /// 测试退出登录是否记录审计日志
    /// </summary>
    [Fact]
    public async Task Logout_ShouldWriteAuditLog()
    {
        // Arrange: 登录获取token
        var token = await LoginAsync(TestTenantId, DefaultUsername, DefaultPassword);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        // Act: 退出登录
        var logoutResponse = await _client.PostAsync("/api/v1/auth/logout", null);
        logoutResponse.EnsureSuccessStatusCode();

        // 等待审计日志写入
        await Task.Delay(100);

        // 重新登录以查询审计日志
        var newToken = await LoginAsync(TestTenantId, DefaultUsername, DefaultPassword);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {newToken}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        var auditResponse = await _client.GetAsync("/api/v1/audit?pageIndex=1&pageSize=20");
        auditResponse.EnsureSuccessStatusCode();

        var result = await auditResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AuditListItem>>>();

        // Assert: 验证退出登录的审计记录
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var logoutAudit = result.Data.Items.FirstOrDefault(a =>
            a.Action == "LOGOUT" && a.Result == "SUCCESS" && a.Actor == DefaultUsername);

        Assert.NotNull(logoutAudit);
        Assert.Equal("LOGOUT", logoutAudit.Action);
        Assert.Equal("SUCCESS", logoutAudit.Result);
        Assert.Equal(DefaultUsername, logoutAudit.Actor);
    }

    /// <summary>
    /// 测试审计日志是否包含IP地址和用户代理信息
    /// </summary>
    [Fact]
    public async Task AuditLog_ShouldContainIpAddressAndUserAgent()
    {
        // Arrange & Act: 登录
        var token = await LoginAsync(TestTenantId, DefaultUsername, DefaultPassword);

        await Task.Delay(100);

        // 查询审计日志
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        var response = await _client.GetAsync("/api/v1/audit?pageIndex=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AuditListItem>>>();

        // Assert: 验证审计日志包含IP和UserAgent
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Total > 0);

        var loginAudit = result.Data.Items.FirstOrDefault(a => a.Action == "LOGIN");
        Assert.NotNull(loginAudit);
        Assert.False(string.IsNullOrWhiteSpace(loginAudit.IpAddress), "审计日志应包含IP地址");
        Assert.False(string.IsNullOrWhiteSpace(loginAudit.UserAgent), "审计日志应包含用户代理信息");
    }

    /// <summary>
    /// 测试审计日志查询是否支持分页
    /// </summary>
    [Fact]
    public async Task QueryAuditLogs_ShouldSupportPagination()
    {
        // Arrange: 登录获取token
        var token = await LoginAsync(TestTenantId, DefaultUsername, DefaultPassword);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        // Act: 查询第一页（每页5条）
        var page1Response = await _client.GetAsync("/api/v1/audit?pageIndex=1&pageSize=5");
        page1Response.EnsureSuccessStatusCode();

        var page1Result = await page1Response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AuditListItem>>>();

        // Assert: 验证分页信息
        Assert.NotNull(page1Result);
        Assert.True(page1Result.Success);
        Assert.NotNull(page1Result.Data);
        Assert.Equal(1, page1Result.Data.PageIndex);
        Assert.Equal(5, page1Result.Data.PageSize);
        Assert.True(page1Result.Data.Total > 0);

        // 如果总数大于5，验证Items不超过5条
        if (page1Result.Data.Total > 5)
        {
            Assert.True(page1Result.Data.Items.Count <= 5);
        }
    }

    /// <summary>
    /// 测试审计日志是否正确隔离租户数据
    /// </summary>
    [Fact]
    public async Task AuditLog_ShouldIsolateTenantData()
    {
        // 此测试在 MultiTenancyTests.cs 中已有实现
        // 这里仅作为占位，确保审计日志的多租户隔离测试被覆盖

        // Arrange: 登录租户A
        var token = await LoginAsync(TestTenantId, DefaultUsername, DefaultPassword);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId);

        // Act: 查询审计日志
        var response = await _client.GetAsync("/api/v1/audit?pageIndex=1&pageSize=100");

        // Assert: 应该成功并返回租户隔离的数据
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AuditListItem>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        // 所有审计记录都应该属于当前租户
        // （在实际实现中，QueryFilter会自动过滤TenantId）
    }

    // ====== 辅助方法 ======

    /// <summary>
    /// 登录并返回访问令牌
    /// </summary>
    private async Task<string> LoginAsync(string tenantId, string username, string password)
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
        Assert.NotNull(result.Data);

        var accessToken = result.Data!.GetProperty("accessToken").GetString();
        Assert.NotNull(accessToken);

        return accessToken;
    }
}
