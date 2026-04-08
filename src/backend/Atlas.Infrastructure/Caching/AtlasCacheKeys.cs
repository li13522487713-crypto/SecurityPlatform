using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Caching;

public static class AtlasCacheKeys
{
    public static class Plans
    {
        public static string ActiveList() => "platform:plans:active-list";

        public static string ById(long id) => $"platform:plans:id:{id}";

        public static string ByCode(string code) => $"platform:plans:code:{code.Trim().ToLowerInvariant()}";
    }

    public static class Subscriptions
    {
        public static string Current(TenantId tenantId) => $"platform:subscriptions:current:{tenantId.Value:N}";
    }

    public static class Identity
    {
        public static string PermissionDecision(TenantId tenantId, long userId) => $"identity:perm:u:{tenantId.Value:N}:{userId}";

        public static string AuthProfile(TenantId tenantId, long userId) => $"identity:profile:u:{tenantId.Value:N}:{userId}";

        public static string MenuTree(TenantId tenantId, long userId) => $"identity:menu:u:{tenantId.Value:N}:{userId}";

        public static string AuthSession(TenantId tenantId, long sessionId) => $"identity:auth:s:{tenantId.Value:N}:{sessionId}";
    }

    public static class AppConfig
    {
        public static string ByAppId(TenantId tenantId, string appId) => $"app-config:{tenantId.Value:N}:{appId.Trim().ToLowerInvariant()}";
    }

    public static class TenantConnection
    {
        public static string TenantInfo(string tenantId) => $"tenant-conn:t:{NormalizeId(tenantId)}";

        public static string AppInfo(string tenantId, long tenantAppInstanceId) => $"tenant-conn:a:{NormalizeId(tenantId)}:{tenantAppInstanceId}";
    }

    public static class RoutePolicy
    {
        public static string AppDataRoute(TenantId tenantId, long appInstanceId) => $"route-policy:{tenantId.Value:N}:{appInstanceId}";
    }

    public static class Captcha
    {
        public static string Key() => $"captcha:{Guid.NewGuid():N}";
    }

    private static string NormalizeId(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}

public static class AtlasCacheTags
{
    public static string Plans() => "tag:platform:plans";

    public static string SubscriptionTenant(TenantId tenantId) => $"tag:platform:subscriptions:{tenantId.Value:N}";

    public static string IdentityTenant(TenantId tenantId) => $"tag:identity:t:{tenantId.Value:N}";

    public static string IdentityUser(TenantId tenantId, long userId) => $"tag:identity:u:{tenantId.Value:N}:{userId}";

    public static string AppConfigTenant(TenantId tenantId) => $"tag:app-config:t:{tenantId.Value:N}";

    public static string TenantConnectionTenant(string tenantId) => $"tag:tenant-conn:t:{NormalizeId(tenantId)}";

    public static string TenantConnectionApp(string tenantId, long appInstanceId) => $"tag:tenant-conn:a:{NormalizeId(tenantId)}:{appInstanceId}";

    public static string RoutePolicy(TenantId tenantId, long appInstanceId) => $"tag:route-policy:{tenantId.Value:N}:{appInstanceId}";

    private static string NormalizeId(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}

