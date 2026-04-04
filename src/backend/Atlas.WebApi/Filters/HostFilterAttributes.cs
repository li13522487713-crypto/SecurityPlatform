namespace Atlas.WebApi.Filters;

[AttributeUsage(AttributeTargets.Class)]
public sealed class PlatformOnlyAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AppRuntimeOnlyAttribute : Attribute;
