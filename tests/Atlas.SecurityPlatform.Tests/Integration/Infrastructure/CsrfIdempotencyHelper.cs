namespace Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

public static class CsrfIdempotencyHelper
{
    public static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string accessToken,
        string tenantId = IntegrationAuthHelper.DefaultTenantId,
        CancellationToken cancellationToken = default)
    {
        IntegrationAuthHelper.SetAuthorizationHeaders(client, accessToken, tenantId);
        await Task.CompletedTask;
        return "deprecated-csrf-token";
    }

    public static void AddWriteSecurityHeaders(
        HttpRequestMessage request,
        string csrfToken,
        string? idempotencyKey = null)
    {        _ = csrfToken;
        _ = idempotencyKey;
    }
}

