namespace Atlas.Core.Setup;

/// <summary>
/// Setup 初始化结构化回执，用于前端展示初始化结果。
/// </summary>
public sealed class BootstrapReport
{
    public bool SchemaInitialized { get; set; }
    public int TablesCreated { get; set; }
    public bool MigrationsApplied { get; set; }
    public int MigrationCount { get; set; }
    public bool SeedCompleted { get; set; }
    public string SeedSummary { get; set; } = string.Empty;
    public bool AdminCreated { get; set; }
    public string? AdminUsername { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool Success => Errors.Count == 0;
}
