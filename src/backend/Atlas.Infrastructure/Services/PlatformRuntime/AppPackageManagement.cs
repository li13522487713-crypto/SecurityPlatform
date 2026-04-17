using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using Atlas.Shared.Contracts.Package;
using Microsoft.Extensions.Configuration;
using SqlSugar;

namespace Atlas.Infrastructure.Services.PlatformRuntime;

public sealed class FileSystemAppPackageBuilder : IAppPackageBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ISqlSugarClient db;
    private readonly IConfiguration configuration;

    public FileSystemAppPackageBuilder(
        ISqlSugarClient db,
        IConfiguration configuration)
    {
        this.db = db;
        this.configuration = configuration;
    }

    public async Task<AppPackageBuildResult> BuildAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default)
    {
        var release = await db.Queryable<AppRelease>()
            .FirstAsync(row => row.TenantIdValue == tenantId.Value && row.Id == releaseId, cancellationToken);
        if (release is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "Release not found.");
        }

        var manifest = await db.Queryable<AppManifest>()
            .FirstAsync(row => row.TenantIdValue == tenantId.Value && row.Id == release.ManifestId, cancellationToken);
        if (manifest is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "Manifest not found.");
        }

        var builtAt = DateTimeOffset.UtcNow;
        var artifactId = $"{manifest.AppKey}-r{releaseId}-{builtAt:yyyyMMddHHmmss}";
        var artifactRoot = ResolveArtifactRoot(configuration);
        var stagingRoot = Path.Combine(artifactRoot, "staging", artifactId);
        var packageRoot = Path.Combine(stagingRoot, "app-package");
        var zipRoot = Path.Combine(artifactRoot, "packages");
        Directory.CreateDirectory(packageRoot);
        Directory.CreateDirectory(zipRoot);

        var frontendRuntime = Path.Combine(packageRoot, "frontend", "runtime");
        var frontendLogin = Path.Combine(packageRoot, "frontend", "login");
        var backendAppHost = Path.Combine(packageRoot, "backend", "Atlas.AppHost");
        var contracts = Path.Combine(packageRoot, "contracts");
        var configRoot = Path.Combine(packageRoot, "config");
        var migrations = Path.Combine(packageRoot, "migrations");
        var health = Path.Combine(packageRoot, "health");
        var metadata = Path.Combine(packageRoot, "metadata");
        foreach (var directory in new[] { frontendRuntime, frontendLogin, backendAppHost, contracts, configRoot, migrations, health, metadata })
        {
            Directory.CreateDirectory(directory);
        }

        var manifestPath = Path.Combine(packageRoot, "manifest.json");
        var canonicalManifest = new
        {
            packageType = "atlas-app-package",
            manifestVersion = "1.0.0",
            appKey = manifest.AppKey,
            applicationCatalogId = manifest.Id.ToString(),
            releaseId = release.Id.ToString(),
            releaseVersion = release.Version.ToString(),
            artifactId,
            frontend = new { runtimePath = "frontend/runtime", loginPath = "frontend/login" },
            backend = new { entryAssembly = "backend/Atlas.AppHost/Atlas.AppHost.dll", defaultUrls = new[] { "http://127.0.0.1:0" } },
            compatibility = new { minPlatformVersion = "1.0.0", runtimeContractVersion = "1.0.0" }
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(canonicalManifest, SerializerOptions), cancellationToken);

        var buildInfo = new AppPackageBuildInfo
        {
            BuildNumber = builtAt.ToString("yyyyMMddHHmmss"),
            CommitSha = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "local",
            BuiltBy = Environment.UserName,
            BuiltAt = builtAt
        };
        await File.WriteAllTextAsync(
            Path.Combine(metadata, "build-info.json"),
            JsonSerializer.Serialize(buildInfo, SerializerOptions),
            cancellationToken);

        var packageManifest = new AppPackageManifest
        {
            AppKey = manifest.AppKey,
            Version = release.Version.ToString(),
            ArtifactId = artifactId,
            BuiltAt = builtAt
        };
        await File.WriteAllTextAsync(
            Path.Combine(metadata, "package-metadata.json"),
            JsonSerializer.Serialize(packageManifest, SerializerOptions),
            cancellationToken);

        await File.WriteAllTextAsync(Path.Combine(frontendRuntime, "runtime-manifest.json"), "{\"status\":\"placeholder\"}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(frontendLogin, "login-manifest.json"), "{\"status\":\"placeholder\"}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(contracts, "api-contracts.json"), "{}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(contracts, "runtime-events.json"), "{}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(contracts, "health-contracts.json"), "{}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(configRoot, "env.template"), "ASPNETCORE_ENVIRONMENT=Development", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(configRoot, "appsettings.instance.template.json"), "{}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(migrations, "manifest.json"), "{\"steps\":[]}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(health, "endpoints.json"), "{\"live\":\"/internal/health/live\",\"ready\":\"/internal/health/ready\"}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(packageRoot, "release-notes.md"), release.ReleaseNote ?? string.Empty, cancellationToken);

        var packagePath = Path.Combine(zipRoot, $"{artifactId}.zip");
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }
        ZipFile.CreateFromDirectory(packageRoot, packagePath, CompressionLevel.Fastest, includeBaseDirectory: false);

        var sha256 = ComputeSha256(packagePath);
        await File.WriteAllTextAsync(Path.Combine(metadata, "checksums.sha256"), $"{sha256}  {Path.GetFileName(packagePath)}", cancellationToken);

        return new AppPackageBuildResult(
            artifactId,
            sha256,
            packagePath,
            manifestPath,
            builtAt.ToString("O"));
    }

    private static string ResolveArtifactRoot(IConfiguration configuration)
    {
        var configuredRoot = configuration["Atlas:Runtime:ArtifactRoot"];
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var repoRoot = ResolveRepoRoot();
        return Path.Combine(repoRoot, "runtime", "artifacts");
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var srcBackend = Path.Combine(directory.FullName, "src", "backend");
            if (Directory.Exists(srcBackend))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(stream);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class FileSystemAppPackageInstaller : IAppPackageInstaller
{
    private readonly ISqlSugarClient db;
    private readonly IAppPackageBuilder packageBuilder;
    private readonly IAppRuntimeSupervisor runtimeSupervisor;

    public FileSystemAppPackageInstaller(
        ISqlSugarClient db,
        IAppPackageBuilder packageBuilder,
        IAppRuntimeSupervisor runtimeSupervisor)
    {
        this.db = db;
        this.packageBuilder = packageBuilder;
        this.runtimeSupervisor = runtimeSupervisor;
    }

    public Task<ReleaseInstallResult> RollbackAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default)
    {
        return InstallCoreAsync(tenantId, releaseId, tenantAppInstanceId, isRollback: true, cancellationToken);
    }

    public Task<ReleaseInstallResult> InstallAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default)
    {
        return InstallCoreAsync(tenantId, releaseId, tenantAppInstanceId, isRollback: false, cancellationToken);
    }

    private async Task<ReleaseInstallResult> InstallCoreAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        bool isRollback,
        CancellationToken cancellationToken)
    {
        var app = await db.Queryable<AppManifest>()
            .FirstAsync(row => row.TenantIdValue == tenantId.Value && row.Id == tenantAppInstanceId, cancellationToken);
        if (app is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "Tenant app instance not found.");
        }

        var package = await packageBuilder.BuildAsync(tenantId, releaseId, cancellationToken);
        var runtimeRegistration = new TenantAppInstanceRuntimeRegistration(
            app.Id.ToString(),
            app.AppKey,
            app.Name,
            app.Version,
            package.ArtifactId);
        var runtimeInfo = isRollback
            ? await runtimeSupervisor.RestartAsync(tenantId, runtimeRegistration, cancellationToken)
            : await runtimeSupervisor.StartAsync(tenantId, runtimeRegistration, cancellationToken);
        var health = await runtimeSupervisor.GetHealthAsync(tenantId, tenantAppInstanceId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(runtimeInfo.InstanceHome))
        {
            var releaseDir = Path.Combine(runtimeInfo.InstanceHome, "releases", $"release-{releaseId}");
            Directory.CreateDirectory(releaseDir);
            var targetPath = Path.Combine(releaseDir, Path.GetFileName(package.PackagePath));
            File.Copy(package.PackagePath, targetPath, overwrite: true);
            var installMetadataPath = Path.Combine(releaseDir, "install.json");
            var installMetadata = new
            {
                releaseId,
                artifactId = package.ArtifactId,
                artifactSha256 = package.ArtifactSha256,
                runtimeInfo,
                health
            };
            await File.WriteAllTextAsync(installMetadataPath, JsonSerializer.Serialize(installMetadata), cancellationToken);
        }

        var installedAt = DateTimeOffset.UtcNow.ToString("O");
        return new ReleaseInstallResult(
            releaseId.ToString(),
            tenantAppInstanceId.ToString(),
            isRollback ? "RolledBack" : "Installed",
            runtimeInfo.RuntimeStatus,
            health?.HealthStatus ?? runtimeInfo.HealthStatus,
            runtimeInfo.AssignedPort,
            runtimeInfo.CurrentPid,
            runtimeInfo.IngressUrl,
            runtimeInfo.LoginUrl,
            package.ArtifactId,
            package.ArtifactSha256,
            installedAt,
            null);
    }
}

public sealed class DefaultAppReleaseOrchestrator : IAppReleaseOrchestrator
{
    private readonly IAppPackageInstaller installer;
    private readonly ConcurrentDictionary<string, ReleaseInstallStatusInfo> statusCache = new(StringComparer.Ordinal);

    public DefaultAppReleaseOrchestrator(IAppPackageInstaller installer)
    {
        this.installer = installer;
    }

    public async Task<ReleaseInstallResult> InstallAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default)
    {
        var result = await installer.InstallAsync(tenantId, releaseId, tenantAppInstanceId, cancellationToken);
        var status = ToStatus(result, DateTimeOffset.UtcNow);
        statusCache[BuildCacheKey(tenantId, releaseId, tenantAppInstanceId)] = status;
        return result;
    }

    public async Task<ReleaseInstallResult> RollbackAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default)
    {
        var result = await installer.RollbackAsync(tenantId, releaseId, tenantAppInstanceId, cancellationToken);
        var status = ToStatus(result, DateTimeOffset.UtcNow);
        statusCache[BuildCacheKey(tenantId, releaseId, tenantAppInstanceId)] = status;
        return result;
    }

    public Task<ReleaseInstallStatusInfo?> GetInstallStatusAsync(
        TenantId tenantId,
        long releaseId,
        long tenantAppInstanceId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        statusCache.TryGetValue(BuildCacheKey(tenantId, releaseId, tenantAppInstanceId), out var status);
        return Task.FromResult<ReleaseInstallStatusInfo?>(status);
    }

    private static string BuildCacheKey(TenantId tenantId, long releaseId, long tenantAppInstanceId)
    {
        return $"{tenantId.Value:D}:{releaseId}:{tenantAppInstanceId}";
    }

    private static ReleaseInstallStatusInfo ToStatus(ReleaseInstallResult result, DateTimeOffset now)
    {
        return new ReleaseInstallStatusInfo(
            result.ReleaseId,
            result.TenantAppInstanceId,
            result.InstallStatus,
            result.RuntimeStatus,
            result.HealthStatus,
            result.AssignedPort,
            result.CurrentPid,
            result.IngressUrl,
            result.LoginUrl,
            result.ArtifactId,
            result.ArtifactSha256,
            now.ToString("O"),
            result.Message);
    }
}

public sealed class AppEntryQueryService : IAppEntryQueryService
{
    private readonly ISqlSugarClient db;
    private readonly IAppRuntimeSupervisor runtimeSupervisor;

    public AppEntryQueryService(
        ISqlSugarClient db,
        IAppRuntimeSupervisor runtimeSupervisor)
    {
        this.db = db;
        this.runtimeSupervisor = runtimeSupervisor;
    }

    public async Task<AppEntryInfo?> GetEntryAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = appKey.Trim();
        var app = await db.Queryable<AppManifest>()
            .FirstAsync(row => row.TenantIdValue == tenantId.Value && row.AppKey == normalizedKey, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var runtime = await runtimeSupervisor.GetRuntimeInfoAsync(tenantId, app.Id, cancellationToken);
        var runtimeUrl = runtime?.IngressUrl ?? $"/app-host/{Uri.EscapeDataString(app.AppKey)}/entry";
        var loginUrl = runtime?.LoginUrl ?? $"/app-host/{Uri.EscapeDataString(app.AppKey)}/login";
        return new AppEntryInfo(
            app.AppKey,
            app.Name,
            app.Icon,
            "default",
            $"登录并进入 {app.Name}",
            "Password",
            "/auth/app/callback",
            runtimeUrl,
            loginUrl);
    }

    public async Task<AppEntryLoginBeginResult?> BeginLoginAsync(
        TenantId tenantId,
        string appKey,
        string? redirectUri,
        CancellationToken cancellationToken = default)
    {
        var entry = await GetEntryAsync(tenantId, appKey, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var normalizedRedirect = NormalizeRedirectUri(redirectUri);
        var loginUrl = string.IsNullOrWhiteSpace(normalizedRedirect)
            ? entry.LoginUrl
            : $"{entry.LoginUrl}{(entry.LoginUrl.Contains('?') ? '&' : '?')}redirect={Uri.EscapeDataString(normalizedRedirect)}";
        return new AppEntryLoginBeginResult(
            entry.AppKey,
            loginUrl,
            entry.RuntimeUrl,
            entry.CallbackUrl,
            normalizedRedirect);
    }

    public async Task<AppEntryLoginOptions?> GetLoginOptionsAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken = default)
    {
        var entry = await GetEntryAsync(tenantId, appKey, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        return new AppEntryLoginOptions(
            entry.AppKey,
            entry.AuthMode,
            entry.LoginTitle,
            entry.LogoUrl,
            entry.Theme);
    }

    private static string? NormalizeRedirectUri(string? redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return null;
        }

        var trimmed = redirectUri.Trim();
        return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : null;
    }
}
