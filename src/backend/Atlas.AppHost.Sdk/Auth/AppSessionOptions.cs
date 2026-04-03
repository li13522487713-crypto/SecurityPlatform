namespace Atlas.AppHost.Sdk.Auth;

public sealed class AppSessionOptions
{
    public const string SectionName = "AppSession";

    public string CookieName { get; set; } = "atlas_app_session";

    public int IdleTimeoutMinutes { get; set; } = 30;
}
