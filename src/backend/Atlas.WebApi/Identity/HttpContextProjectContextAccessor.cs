using Atlas.Core.Identity;

namespace Atlas.WebApi.Identity;

public sealed class HttpContextProjectContextAccessor : IProjectContextAccessor
{
    public const string ProjectContextItemKey = "ProjectContext";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextProjectContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ProjectContext GetCurrent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return new ProjectContext(false, null);
        }

        if (httpContext.Items.TryGetValue(ProjectContextItemKey, out var value)
            && value is ProjectContext cached)
        {
            return cached;
        }

        var fallback = new ProjectContext(false, null);
        httpContext.Items[ProjectContextItemKey] = fallback;
        return fallback;
    }
}
