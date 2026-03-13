namespace Atlas.Application.Options;

/// <summary>
/// 文件存储配置（等保2.0：限制可上传文件类型，防止恶意文件上传）
/// </summary>
public sealed class FileStorageOptions
{
    public const string UnsafeDefaultSignedUrlSecret = "CHANGE_ME_FILE_SIGNED_URL_SECRET";

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

    /// <summary>默认分片大小（字节），默认 2MB。</summary>
    public int ChunkPartSizeBytes { get; init; } = 2 * 1024 * 1024;

    /// <summary>分片上传会话过期分钟数。</summary>
    public int ChunkSessionExpireMinutes { get; init; } = 120;

    /// <summary>签名下载默认有效期（秒）。</summary>
    public int SignedUrlDefaultExpireSeconds { get; init; } = 600;

    /// <summary>签名下载密钥（生产环境请通过环境变量覆盖）。</summary>
    public string SignedUrlSecret { get; init; } = UnsafeDefaultSignedUrlSecret;
}
