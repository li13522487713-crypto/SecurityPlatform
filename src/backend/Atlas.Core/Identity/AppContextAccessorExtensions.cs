namespace Atlas.Core.Identity;

public static class AppContextAccessorExtensions
{
    public static long? ResolveAppId(this IAppContextAccessor appContextAccessor)
    {
        if (appContextAccessor is null)
        {
            throw new ArgumentNullException(nameof(appContextAccessor));
        }

        var appIdText = appContextAccessor.GetAppId();
        return long.TryParse(appIdText, out var appId) ? appId : null;
    }
}
