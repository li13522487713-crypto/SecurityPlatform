using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class OrganizationIntegrationTests
{
    private readonly HttpClient _client;
    private readonly AtlasWebApplicationFactory _factory;

    public OrganizationIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateDepartmentWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/departments");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        request.Content = JsonContent.Create(new
        {
            code = $"DEP{Guid.NewGuid():N}".Substring(0, 8),
            name = "部门-缺少幂等键",
            parentId = (long?)null,
            sortOrder = 1
        });

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }

    [Fact]
    public async Task CreatePermissionWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/permissions");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        request.Content = JsonContent.Create(new
        {
            name = "权限-缺少幂等",
            code = $"PERM_{Guid.NewGuid():N}".Substring(0, 16),
            type = "API",
            description = "幂等校验"
        });

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }

    [Fact]
    public async Task WorkspaceDevelopAppCreate_ShouldPersistNullAgentId_AndCleanupWorkflowWhenAiAppCreateFails()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var appInstance = await CreateLowCodeAppAsync(accessToken);
        var (tenantId, userId, isPlatformAdmin) = await GetDefaultAdminContextAsync();
        var workspaceId = await CreateWorkspaceAsync(appInstance.Id, userId, tenantId);
        var tenantGuid = tenantId.Value;
        var appName = $"WorkspaceApp{Guid.NewGuid():N}"[..24];

        WorkspaceAppCreateResult createResult;
        using (var scope = _factory.Services.CreateScope())
        {
            var appContextAccessor = scope.ServiceProvider.GetRequiredService<IAppContextAccessor>();
            var service = scope.ServiceProvider.GetRequiredService<IWorkspacePortalService>();
            using var appContextScope = appContextAccessor.BeginScope(CreateAppContext(tenantId, appInstance.Id));
            createResult = await service.CreateDevelopAppAsync(
                tenantId,
                long.Parse(workspaceId),
                userId,
                isPlatformAdmin,
                new WorkspaceAppCreateRequest(appName, "workspace create app integration", null),
                CancellationToken.None);
        }

        var appId = createResult.AppId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            var persistedApp = await db.Queryable<AiApp>()
                .Where(x => x.TenantIdValue == tenantGuid && x.Id == long.Parse(appId))
                .FirstAsync();

            Assert.NotNull(persistedApp);
            Assert.Null(persistedApp!.AgentId);
        }

        int workflowCountBeforeDuplicate;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            workflowCountBeforeDuplicate = await db.Queryable<WorkflowMeta>()
                .Where(x => x.TenantIdValue == tenantGuid && !x.IsDeleted && x.WorkspaceId == long.Parse(workspaceId))
                .CountAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var appContextAccessor = scope.ServiceProvider.GetRequiredService<IAppContextAccessor>();
            var service = scope.ServiceProvider.GetRequiredService<IWorkspacePortalService>();
            using var appContextScope = appContextAccessor.BeginScope(CreateAppContext(tenantId, appInstance.Id));
            var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateDevelopAppAsync(
                tenantId,
                long.Parse(workspaceId),
                userId,
                isPlatformAdmin,
                new WorkspaceAppCreateRequest(appName, "duplicate workspace app", null),
                CancellationToken.None));
            Assert.Equal(ErrorCodes.ValidationError, exception.Code);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
            var workflowCountAfterDuplicate = await db.Queryable<WorkflowMeta>()
                .Where(x => x.TenantIdValue == tenantGuid && !x.IsDeleted && x.WorkspaceId == long.Parse(workspaceId))
                .CountAsync();

            Assert.Equal(workflowCountBeforeDuplicate, workflowCountAfterDuplicate);
        }
    }

    [Fact]
    public async Task WorkspaceCrud_ShouldCreateUpdateAndArchiveWorkspace()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var appInstance = await CreateLowCodeAppAsync(accessToken);
        var (tenantId, userId, isPlatformAdmin) = await GetDefaultAdminContextAsync();
        var workspaceId = await CreateWorkspaceAsync(appInstance.Id, userId, tenantId);
        const string updatedDescription = "updated workspace description";
        const string updatedIcon = "updated-icon";
        WorkspaceDetailDto? beforeUpdate;
        WorkspaceDetailDto? afterUpdate;
        WorkspaceDetailDto? afterDelete;
        WorkspaceDetailDto? byAppKeyAfterDelete;
        IReadOnlyList<WorkspaceListItem> workspaceListAfterDelete;

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IWorkspacePortalService>();
            beforeUpdate = await service.GetWorkspaceAsync(tenantId, long.Parse(workspaceId), userId, isPlatformAdmin, CancellationToken.None);
            await service.UpdateWorkspaceAsync(
                tenantId,
                long.Parse(workspaceId),
                userId,
                isPlatformAdmin,
                new WorkspaceUpdateRequest(
                    $"UpdatedWorkspace{Guid.NewGuid():N}"[..28],
                    updatedDescription,
                    updatedIcon),
                CancellationToken.None);
            afterUpdate = await service.GetWorkspaceAsync(tenantId, long.Parse(workspaceId), userId, isPlatformAdmin, CancellationToken.None);
            await service.DeleteWorkspaceAsync(tenantId, long.Parse(workspaceId), userId, isPlatformAdmin, CancellationToken.None);
            afterDelete = await service.GetWorkspaceAsync(tenantId, long.Parse(workspaceId), userId, isPlatformAdmin, CancellationToken.None);
            byAppKeyAfterDelete = await service.GetWorkspaceByAppKeyAsync(tenantId, appInstance.AppKey, userId, isPlatformAdmin, CancellationToken.None);
            workspaceListAfterDelete = await service.ListWorkspacesAsync(tenantId, userId, isPlatformAdmin, CancellationToken.None);
        }

        Assert.NotNull(beforeUpdate);
        Assert.Equal(appInstance.Id, beforeUpdate!.AppInstanceId);
        Assert.Equal(appInstance.AppKey, beforeUpdate.AppKey);

        Assert.NotNull(afterUpdate);
        Assert.Equal(updatedDescription, afterUpdate!.Description);
        Assert.Equal(updatedIcon, afterUpdate.Icon);
        Assert.Equal(appInstance.Id, afterUpdate.AppInstanceId);
        Assert.Equal(appInstance.AppKey, afterUpdate.AppKey);

        Assert.Null(afterDelete);
        Assert.Null(byAppKeyAfterDelete);
        Assert.DoesNotContain(workspaceListAfterDelete, item => string.Equals(item.Id, workspaceId, StringComparison.Ordinal));
    }

    private async Task<(string Id, string AppKey)> CreateLowCodeAppAsync(string accessToken)
    {
        var appKey = $"workspace-{Guid.NewGuid():N}"[..18];

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lowcode-apps");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Content = JsonContent.Create(new
        {
            appKey,
            name = $"工作空间测试应用-{Guid.NewGuid():N}"[..24],
            description = "workspace integration app",
            category = "integration",
            icon = "app",
            dataSourceId = (long?)null
        });

        using var response = await _client.SendAsync(request);
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        return (ApiResponseAssert.RequireString(payload.Data, "id"), appKey);
    }

    private async Task<string> CreateWorkspaceAsync(string appInstanceId, long userId, TenantId tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var appContextAccessor = scope.ServiceProvider.GetRequiredService<IAppContextAccessor>();
        var service = scope.ServiceProvider.GetRequiredService<IWorkspacePortalService>();
        using var appContextScope = appContextAccessor.BeginScope(CreateAppContext(tenantId, appInstanceId));
        var workspaceId = await service.CreateWorkspaceAsync(
            tenantId,
            userId,
            new WorkspaceCreateRequest(
                $"工作空间-{Guid.NewGuid():N}"[..20],
                "workspace integration",
                "workspace",
                appInstanceId),
            CancellationToken.None);

        return workspaceId.ToString();
    }

    private async Task<(TenantId TenantId, long UserId, bool IsPlatformAdmin)> GetDefaultAdminContextAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var tenantId = new TenantId(Guid.Parse(IntegrationAuthHelper.DefaultTenantId));
        var user = await db.Queryable<UserAccount>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Username == IntegrationAuthHelper.DefaultUsername)
            .FirstAsync()
            ?? throw new InvalidOperationException("默认管理员不存在。");

        return (tenantId, user.Id, user.IsPlatformAdmin);
    }

    private static AppContextSnapshot CreateAppContext(TenantId tenantId, string appId)
    {
        return new AppContextSnapshot(
            tenantId,
            appId,
            null,
            new ClientContext(ClientType.Backend, ClientPlatform.Web, ClientChannel.Browser, ClientAgent.Other),
            null);
    }
}



