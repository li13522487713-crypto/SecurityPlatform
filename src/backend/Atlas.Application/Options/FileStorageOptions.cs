namespace Atlas.Application.Options;

/// <summary>
/// 文件存储配置（等保2.0：限制可上传文件类型，防止恶意文件上传）
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>文件存储根目录（相对于应用根目录）</summary>
    public string BasePath { get; init; } = "uploads";

    /// <summary>单文件最大字节数，默认 10 MB</summary>
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>允许的文件扩展名（含点号，小写），空数组表示不限制</summary>
    public string[] AllowedExtensions { get; init; } =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".pdf", ".xlsx", ".xls", ".docx", ".doc",
        ".txt", ".csv", ".zip", ".rar"
    ];

    /// <summary>明确拒绝的危险扩展名（优先级高于 AllowedExtensions）</summary>
    public string[] BlockedExtensions { get; init; } =
    [
        ".exe", ".sh", ".bat", ".cmd", ".ps1",
        ".vbs", ".js", ".msi", ".dll", ".so"
    ];
}
