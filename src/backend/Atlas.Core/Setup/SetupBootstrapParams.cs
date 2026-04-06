namespace Atlas.Core.Setup;

/// <summary>
/// Setup 向导传递给初始化器的显式参数，不依赖启动期冻结的 IOptions。
/// </summary>
public sealed class SetupBootstrapParams
{
    public required string ConnectionString { get; init; }
    public required string DbType { get; init; }
    public required string TenantId { get; init; }
    public required string AdminUsername { get; init; }
    public required string AdminPassword { get; init; }
    public string AdminRoles { get; init; } = "Admin";
    public bool IsPlatformAdmin { get; init; } = true;
    public bool SkipSchemaInit { get; init; }
    public bool SkipSeedData { get; init; }
    public bool SkipSchemaMigrations { get; init; }
}
