using System.IO.Compression;
using System.Text.Json;
using Atlas.Application.Plugins.Abstractions;
using Atlas.Application.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Plugins;

/// <summary>
/// .atpkg 插件包服务：解压校验并安装到插件目录。
/// 包结构: manifest.json + lib/*.dll + assets/ + config-schema.json(optional)
/// </summary>
public sealed class PluginPackageService
{
    private readonly IPluginCatalogService _catalogService;
    private readonly ILogger<PluginPackageService> _logger;

    public PluginPackageService(
        IPluginCatalogService catalogService,
        ILogger<PluginPackageService> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    /// <summary>
    /// 从上传的 .atpkg（ZIP）流中安装插件。
    /// 校验 manifest.json，解压 lib/*.dll 到目标目录，然后重载目录。
    /// </summary>
    public async Task<PluginManifest> InstallAsync(
        Stream packageStream,
        string pluginRootPath,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);

        // 1. 读取并校验 manifest
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("插件包缺少 manifest.json");

        PluginManifest manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<PluginManifest>(manifestStream, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("manifest.json 解析失败");
        }

        ValidateManifest(manifest);

        // 2. 创建插件目录（以 code 命名）
        var pluginDir = Path.Combine(pluginRootPath, manifest.Code);
        Directory.CreateDirectory(pluginDir);

        // 3. 解压所有文件（防御 Zip Slip：确保目标路径在 pluginDir 内）
        var canonicalPluginDir = Path.GetFullPath(pluginDir) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue; // 跳过目录条目
            }

            var destFile = Path.GetFullPath(
                Path.Combine(pluginDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

            if (!destFile.StartsWith(canonicalPluginDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"插件包包含非法路径条目，可能存在 Zip Slip 攻击：{entry.FullName}");
            }

            var destDir = Path.GetDirectoryName(destFile)!;
            Directory.CreateDirectory(destDir);

            await using var entryStream = entry.Open();
            await using var fileStream = File.Create(destFile);
            await entryStream.CopyToAsync(fileStream, cancellationToken);
        }

        // 4. 安装主 DLL（从 lib/ 目录复制到根）
        var libDir = Path.Combine(pluginDir, "lib");
        if (Directory.Exists(libDir))
        {
            foreach (var dll in Directory.GetFiles(libDir, "*.dll"))
            {
                var destDll = Path.Combine(pluginRootPath, Path.GetFileName(dll));
                File.Copy(dll, destDll, overwrite: true);
            }
        }

        _logger.LogInformation(
            "Plugin package {Code} v{Version} installed successfully",
            manifest.Code, manifest.Version);

        // 5. 触发重载
        await _catalogService.ReloadAsync(cancellationToken);

        return manifest;
    }

    private static void ValidateManifest(PluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Code))
        {
            throw new InvalidOperationException("manifest.json: Code 不能为空");
        }

        // Code 仅允许字母、数字、连字符、下划线，防止路径注入
        if (!System.Text.RegularExpressions.Regex.IsMatch(manifest.Code, @"^[A-Za-z0-9_\-]+$"))
        {
            throw new InvalidOperationException("manifest.json: Code 包含非法字符，只允许字母、数字、连字符和下划线");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("manifest.json: Version 不能为空");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidOperationException("manifest.json: Name 不能为空");
        }
    }
}
