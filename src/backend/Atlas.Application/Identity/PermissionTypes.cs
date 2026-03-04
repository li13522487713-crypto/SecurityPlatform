namespace Atlas.Application.Identity;

public static class PermissionTypes
{
    public const string Api = "Api";
    public const string Menu = "Menu";
    public const string Application = "Application";
    public const string Page = "Page";
    public const string Action = "Action";

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        Api,
        Menu,
        Application,
        Page,
        Action
    };

    public static bool IsSupported(string? type)
    {
        return !string.IsNullOrWhiteSpace(type) && AllowedTypes.Contains(type);
    }
}
