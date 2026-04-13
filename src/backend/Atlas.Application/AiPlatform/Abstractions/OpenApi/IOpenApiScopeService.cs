using System.Security.Claims;

namespace Atlas.Application.AiPlatform.Abstractions.OpenApi;

public interface IOpenApiScopeService
{
    bool HasScope(ClaimsPrincipal principal, string scope);

    IReadOnlyCollection<string> GetScopes(ClaimsPrincipal principal);
}
