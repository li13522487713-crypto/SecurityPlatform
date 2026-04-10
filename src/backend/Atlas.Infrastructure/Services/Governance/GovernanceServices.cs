using Atlas.Application.Governance.Abstractions;
using Atlas.Application.Governance.Models;
using Atlas.Application.License.Abstractions;
using Atlas.Application.License.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.Governance;

public sealed class PackageService : IPackageService
{
    private readonly ISqlSugarClient _db;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppDataSourceProvisioner _appDataSourceProvisioner;
    private readonly IIdGeneratorAccessor _idGenerator;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PackageService(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppDataSourceProvisioner appDataSourceProvisioner,
        IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _appDbScopeFactory = appDbScopeFactory;
        _appDataSourceProvisioner = appDataSourceProvisioner;
        _idGenerator = idGenerator;
    }

    public PackageService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
        : this(db, new MainOnlyAppDbScopeFactory(db), new NoopAppDataSourceProvisioner(), idGenerator)
    {
    }

    public async Task<PackageOperationResponse> ExportAsync(TenantId tenantId, long userId, PackageExportRequest request, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.ManifestId, out var manifestId) || manifestId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "ManifestId 无效，必须为正整数。");
        }

        var manifest = await _db.Queryable<AppManifest>().FirstAsync(x => x.Id == manifestId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "应用清单不存在。");
        var releases = await _db.Queryable<AppRelease>().Where(x => x.ManifestId == manifestId).ToListAsync(cancellationToken);
        var lowCodeApp = await _db.Queryable<LowCodeApp>().FirstAsync(x => x.AppKey == manifest.AppKey, cancellationToken);
        var runtimeDb = await ResolveRuntimeDbByAppKeyAsync(tenantId, manifest.AppKey, cancellationToken);
        var routes = await runtimeDb.Queryable<RuntimeRoute>().Where(x => x.ManifestId == manifestId).ToListAsync(cancellationToken);
        var pages = lowCodeApp is null
            ? new List<LowCodePage>()
            : await _db.Queryable<LowCodePage>().Where(x => x.AppId == lowCodeApp.Id).ToListAsync(cancellationToken);

        var payload = new ProductizationPackagePayload
        {
            Manifest = ManifestPackageDto.FromEntity(manifest),
            Releases = releases.Select(ReleasePackageDto.FromEntity).ToArray(),
            Routes = routes.Select(RuntimeRoutePackageDto.FromEntity).ToArray(),
            LowCodeApp = lowCodeApp is null ? null : LowCodeAppPackageDto.FromEntity(lowCodeApp),
            Pages = pages.Select(LowCodePagePackageDto.FromEntity).ToArray()
        };
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var zipBytes = CreateZip(payloadBytes);
        var hash = Convert.ToHexString(SHA256.HashData(zipBytes));
        var packageDir = Path.Combine(AppContext.BaseDirectory, "packages");
        Directory.CreateDirectory(packageDir);
        var filePath = Path.Combine(packageDir, $"manifest-{manifestId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.zip");
        await File.WriteAllBytesAsync(filePath, zipBytes, cancellationToken);

        var entity = new PackageArtifact(
            tenantId,
            _idGenerator.NextId(),
            manifestId,
            ParsePackageType(request.PackageType),
            filePath,
            hash,
            zipBytes.LongLength,
            userId,
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return new PackageOperationResponse(entity.Id.ToString(), entity.Status.ToString(), "导出任务已创建");
    }

    public async Task<PackageOperationResponse> ImportAsync(TenantId tenantId, long userId, PackageImportRequest request, CancellationToken cancellationToken = default)
    {
        byte[] zipBytes;
        try
        {
            zipBytes = Convert.FromBase64String(request.ContentBase64);
        }
        catch (FormatException)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "导入内容不是有效的 Base64。");
        }

        var payloadBytes = ExtractPackageJson(zipBytes);
        var payload = JsonSerializer.Deserialize<ProductizationPackagePayload>(payloadBytes, JsonOptions)
            ?? throw new BusinessException(ErrorCodes.ValidationError, "导入包内容无效。");
        if (payload.Manifest is null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "导入包缺少应用清单。");
        }

        var conflictPolicy = (request.ConflictPolicy ?? "skip").Trim().ToLowerInvariant();
        var existing = await _db.Queryable<AppManifest>().FirstAsync(x => x.AppKey == payload.Manifest.AppKey, cancellationToken);
        long targetManifestId;
        string targetAppKey;
        if (existing is not null)
        {
            if (conflictPolicy == "skip")
            {
                targetManifestId = existing.Id;
                targetAppKey = existing.AppKey;
            }
            else if (conflictPolicy == "overwrite")
            {
                existing.Update(
                    payload.Manifest.Name,
                    payload.Manifest.Description,
                    payload.Manifest.Category,
                    payload.Manifest.Icon,
                    payload.Manifest.DataSourceId,
                    userId,
                    DateTimeOffset.UtcNow);
                await _db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
                targetManifestId = existing.Id;
                targetAppKey = existing.AppKey;
            }
            else
            {
                var renamed = new AppManifest(
                    tenantId,
                    _idGenerator.NextId(),
                    $"{payload.Manifest.AppKey}-{Guid.NewGuid().ToString("N")[..6]}",
                    payload.Manifest.Name,
                    userId,
                    DateTimeOffset.UtcNow);
                renamed.Update(
                    payload.Manifest.Name,
                    payload.Manifest.Description,
                    payload.Manifest.Category,
                    payload.Manifest.Icon,
                    payload.Manifest.DataSourceId,
                    userId,
                    DateTimeOffset.UtcNow);
                await _db.Insertable(renamed).ExecuteCommandAsync(cancellationToken);
                targetManifestId = renamed.Id;
                targetAppKey = renamed.AppKey;
            }
        }
        else
        {
            var imported = new AppManifest(
                tenantId,
                _idGenerator.NextId(),
                payload.Manifest.AppKey,
                payload.Manifest.Name,
                userId,
                DateTimeOffset.UtcNow);
            imported.Update(
                payload.Manifest.Name,
                payload.Manifest.Description,
                payload.Manifest.Category,
                payload.Manifest.Icon,
                payload.Manifest.DataSourceId,
                userId,
                DateTimeOffset.UtcNow);
            await _db.Insertable(imported).ExecuteCommandAsync(cancellationToken);
            targetManifestId = imported.Id;
            targetAppKey = imported.AppKey;
        }

        await ImportReleasesAsync(targetManifestId, payload.Releases, conflictPolicy, tenantId, userId, cancellationToken);
        await ImportLowCodeAppAndPagesAsync(targetAppKey, payload.LowCodeApp, payload.Pages, conflictPolicy, tenantId, userId, cancellationToken);
        await ImportRuntimeRoutesAsync(targetManifestId, targetAppKey, payload.Routes, conflictPolicy, tenantId, cancellationToken);

        var entity = new PackageArtifact(
            tenantId,
            _idGenerator.NextId(),
            targetManifestId,
            PackageArtifactType.Full,
            $"imports/{request.FileName}",
            Convert.ToHexString(SHA256.HashData(zipBytes)),
            zipBytes.LongLength,
            userId,
            DateTimeOffset.UtcNow);
        entity.MarkImported(userId, DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return new PackageOperationResponse(entity.Id.ToString(), entity.Status.ToString(), "导入完成");
    }

    private async Task ImportReleasesAsync(
        long manifestId,
        IReadOnlyList<ReleasePackageDto> releaseDtos,
        string conflictPolicy,
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        if (releaseDtos.Count == 0)
        {
            return;
        }

        if (conflictPolicy == "overwrite")
        {
            await _db.Deleteable<AppRelease>()
                .Where(x => x.ManifestId == manifestId)
                .ExecuteCommandAsync(cancellationToken);
        }

        var existingReleases = await _db.Queryable<AppRelease>()
            .Where(x => x.ManifestId == manifestId)
            .ToListAsync(cancellationToken);
        var existingVersions = existingReleases
            .Select(x => x.Version)
            .ToHashSet();
        var now = DateTimeOffset.UtcNow;
        var toInsert = releaseDtos
            .Where(x => !existingVersions.Contains(x.Version))
            .Select(x =>
            {
                var release = new AppRelease(
                    tenantId,
                    _idGenerator.NextId(),
                    manifestId,
                    x.Version <= 0 ? 1 : x.Version,
                    string.IsNullOrWhiteSpace(x.SnapshotJson) ? "{}" : x.SnapshotJson,
                    userId,
                    now);
                return release;
            })
            .ToList();

        if (toInsert.Count > 0)
        {
            await _db.Insertable(toInsert).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task ImportRuntimeRoutesAsync(
        long manifestId,
        string appKey,
        IReadOnlyList<RuntimeRoutePackageDto> routeDtos,
        string conflictPolicy,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        if (routeDtos.Count == 0)
        {
            return;
        }

        var runtimeDb = await ResolveRuntimeDbByAppKeyAsync(tenantId, appKey, cancellationToken);
        var existingRoutes = await runtimeDb.Queryable<RuntimeRoute>()
            .Where(x => x.AppKey == appKey)
            .ToListAsync(cancellationToken);
        var routeByPageKey = existingRoutes
            .GroupBy(x => x.PageKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var importedPageKeys = routeDtos
            .Select(x => x.PageKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toInsert = new List<RuntimeRoute>();
        var toUpdate = new List<RuntimeRoute>();

        foreach (var dto in routeDtos)
        {
            if (string.IsNullOrWhiteSpace(dto.PageKey))
            {
                continue;
            }

            if (routeByPageKey.TryGetValue(dto.PageKey, out var existing))
            {
                existing.RebindManifest(manifestId);
                if (dto.IsActive)
                {
                    existing.Activate(dto.SchemaVersion <= 0 ? 1 : dto.SchemaVersion, dto.EnvironmentCode);
                }
                else
                {
                    existing.Disable();
                }

                toUpdate.Add(existing);
                continue;
            }

            var created = new RuntimeRoute(
                tenantId,
                _idGenerator.NextId(),
                manifestId,
                appKey,
                dto.PageKey,
                dto.SchemaVersion <= 0 ? 1 : dto.SchemaVersion);
            if (!dto.IsActive)
            {
                created.Disable();
            }
            else if (!string.IsNullOrWhiteSpace(dto.EnvironmentCode))
            {
                created.Activate(dto.SchemaVersion <= 0 ? 1 : dto.SchemaVersion, dto.EnvironmentCode);
            }

            toInsert.Add(created);
        }

        if (conflictPolicy == "overwrite")
        {
            foreach (var stale in existingRoutes.Where(x => !importedPageKeys.Contains(x.PageKey) && x.IsActive))
            {
                stale.RebindManifest(manifestId);
                stale.Disable();
                toUpdate.Add(stale);
            }
        }

        if (toInsert.Count > 0)
        {
            await runtimeDb.Insertable(toInsert).ExecuteCommandAsync(cancellationToken);
        }

        if (toUpdate.Count > 0)
        {
            await runtimeDb.Updateable(toUpdate).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task<ISqlSugarClient> ResolveRuntimeDbByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken)
    {
        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey, cancellationToken);
        if (app is not null && app.Id > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
        }

        return _db;
    }

    private async Task ImportLowCodeAppAndPagesAsync(
        string appKey,
        LowCodeAppPackageDto? appDto,
        IReadOnlyList<LowCodePagePackageDto> pageDtos,
        string conflictPolicy,
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        if (appDto is null && pageDtos.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(x => x.AppKey == appKey, cancellationToken);
        if (app is null)
        {
            var appName = string.IsNullOrWhiteSpace(appDto?.Name) ? appKey : appDto.Name;
            app = new LowCodeApp(
                tenantId,
                appKey,
                appName,
                null,
                null,
                null,
                null,
                userId,
                _idGenerator.NextId(),
                now);
            await _db.Insertable(app).ExecuteCommandAsync(cancellationToken);
        }
        else if (appDto is not null && conflictPolicy == "overwrite" && !string.IsNullOrWhiteSpace(appDto.Name))
        {
            app.Update(appDto.Name, app.Description, app.Category, app.Icon, app.DataSourceId, userId, now);
            await _db.Updateable(app).ExecuteCommandAsync(cancellationToken);
        }

        await _appDataSourceProvisioner.EnsureProvisionedAsync(
            tenantId,
            app.Id,
            app.AppKey,
            userId,
            app.DataSourceId,
            cancellationToken);

        if (pageDtos.Count == 0)
        {
            return;
        }

        var existingPages = await _db.Queryable<LowCodePage>()
            .Where(x => x.AppId == app.Id)
            .ToListAsync(cancellationToken);
        var pageByKey = existingPages
            .GroupBy(x => x.PageKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var toInsert = new List<LowCodePage>();
        var toUpdate = new List<LowCodePage>();

        foreach (var dto in pageDtos)
        {
            if (string.IsNullOrWhiteSpace(dto.PageKey))
            {
                continue;
            }

            if (pageByKey.TryGetValue(dto.PageKey, out var existing))
            {
                if (conflictPolicy == "overwrite")
                {
                    existing.RestoreSnapshot(
                        string.IsNullOrWhiteSpace(dto.Name) ? existing.Name : dto.Name,
                        existing.PageType,
                        string.IsNullOrWhiteSpace(existing.SchemaJson) ? "{}" : existing.SchemaJson,
                        dto.RoutePath,
                        existing.Description,
                        existing.Icon,
                        existing.SortOrder,
                        existing.ParentPageId,
                        dto.Version <= 0 ? 1 : dto.Version,
                        dto.IsPublished,
                        existing.PermissionCode,
                        existing.DataTableKey,
                        userId,
                        now);
                    toUpdate.Add(existing);
                }

                continue;
            }

            var page = new LowCodePage(
                tenantId,
                app.Id,
                dto.PageKey,
                string.IsNullOrWhiteSpace(dto.Name) ? dto.PageKey : dto.Name,
                LowCodePageType.Blank,
                "{}",
                dto.RoutePath,
                null,
                null,
                0,
                null,
                userId,
                _idGenerator.NextId(),
                now);
            page.RestoreSnapshot(
                page.Name,
                page.PageType,
                page.SchemaJson,
                dto.RoutePath,
                page.Description,
                page.Icon,
                page.SortOrder,
                page.ParentPageId,
                dto.Version <= 0 ? 1 : dto.Version,
                dto.IsPublished,
                page.PermissionCode,
                page.DataTableKey,
                userId,
                now);
            toInsert.Add(page);
        }

        if (toInsert.Count > 0)
        {
            await _db.Insertable(toInsert).ExecuteCommandAsync(cancellationToken);
        }

        if (toUpdate.Count > 0)
        {
            await _db.Updateable(toUpdate).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task<PackageOperationResponse> AnalyzeAsync(TenantId tenantId, long userId, PackageAnalyzeRequest request, CancellationToken cancellationToken = default)
    {
        byte[] zipBytes;
        try
        {
            zipBytes = Convert.FromBase64String(request.ContentBase64);
        }
        catch (FormatException)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "分析内容不是有效的 Base64。");
        }

        var payloadBytes = ExtractPackageJson(zipBytes);
        var payload = JsonSerializer.Deserialize<ProductizationPackagePayload>(payloadBytes, JsonOptions);
        var appKey = payload?.Manifest?.AppKey;
        var conflict = !string.IsNullOrWhiteSpace(appKey) &&
            await _db.Queryable<AppManifest>().AnyAsync(x => x.AppKey == appKey, cancellationToken);
        var message = conflict
            ? $"检测到 AppKey 冲突：{appKey}"
            : "冲突分析完成，无阻断冲突";
        return new PackageOperationResponse("analyze", "Analyzed", message);
    }

    private static PackageArtifactType ParsePackageType(string packageType)
        => packageType.Trim().ToLowerInvariant() switch
        {
            "structure" => PackageArtifactType.Structure,
            "data" => PackageArtifactType.Data,
            _ => PackageArtifactType.Full
        };

    private static byte[] CreateZip(byte[] packageJsonBytes)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("package.json", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(packageJsonBytes, 0, packageJsonBytes.Length);
        }

        return ms.ToArray();
    }

    private static byte[] ExtractPackageJson(byte[] zipBytes)
    {
        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = archive.GetEntry("package.json")
            ?? throw new BusinessException(ErrorCodes.ValidationError, "导入包缺少 package.json。");
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed class ProductizationPackagePayload
    {
        public ManifestPackageDto? Manifest { get; set; }
        public IReadOnlyList<ReleasePackageDto> Releases { get; set; } = Array.Empty<ReleasePackageDto>();
        public IReadOnlyList<RuntimeRoutePackageDto> Routes { get; set; } = Array.Empty<RuntimeRoutePackageDto>();
        public LowCodeAppPackageDto? LowCodeApp { get; set; }
        public IReadOnlyList<LowCodePagePackageDto> Pages { get; set; } = Array.Empty<LowCodePagePackageDto>();
    }

    private sealed class ManifestPackageDto
    {
        public string AppKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Icon { get; set; }
        public long? DataSourceId { get; set; }

        public static ManifestPackageDto FromEntity(AppManifest entity) => new()
        {
            AppKey = entity.AppKey,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            Icon = entity.Icon,
            DataSourceId = entity.DataSourceId
        };
    }

    private sealed class ReleasePackageDto
    {
        public int Version { get; set; }
        public string? ReleaseNote { get; set; }
        public string SnapshotJson { get; set; } = "{}";

        public static ReleasePackageDto FromEntity(AppRelease entity) => new()
        {
            Version = entity.Version,
            ReleaseNote = entity.ReleaseNote,
            SnapshotJson = entity.SnapshotJson
        };
    }

    private sealed class RuntimeRoutePackageDto
    {
        public string AppKey { get; set; } = string.Empty;
        public string PageKey { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public bool IsActive { get; set; }
        public string EnvironmentCode { get; set; } = "prod";

        public static RuntimeRoutePackageDto FromEntity(RuntimeRoute entity) => new()
        {
            AppKey = entity.AppKey,
            PageKey = entity.PageKey,
            SchemaVersion = entity.SchemaVersion,
            IsActive = entity.IsActive,
            EnvironmentCode = entity.EnvironmentCode
        };
    }

    private sealed class LowCodeAppPackageDto
    {
        public string AppKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static LowCodeAppPackageDto FromEntity(LowCodeApp entity) => new()
        {
            AppKey = entity.AppKey,
            Name = entity.Name
        };
    }

    private sealed class LowCodePagePackageDto
    {
        public string PageKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? RoutePath { get; set; }
        public int Version { get; set; }
        public bool IsPublished { get; set; }

        public static LowCodePagePackageDto FromEntity(LowCodePage entity) => new()
        {
            PageKey = entity.PageKey,
            Name = entity.Name,
            RoutePath = entity.RoutePath,
            Version = entity.Version,
            IsPublished = entity.IsPublished
        };
    }

    private sealed class NoopAppDataSourceProvisioner : IAppDataSourceProvisioner
    {
        public Task EnsureProvisionedAsync(
            TenantId tenantId,
            long appInstanceId,
            string appKey,
            long operatorUserId,
            long? preferredDataSourceId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

public sealed class LicenseGrantService : ILicenseGrantService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ILicenseSignatureService _licenseSignatureService;
    private readonly IMachineFingerprintService _machineFingerprintService;
    private readonly ILicenseService _licenseService;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LicenseGrantService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGenerator,
        ILicenseSignatureService licenseSignatureService,
        IMachineFingerprintService machineFingerprintService,
        ILicenseService licenseService)
    {
        _db = db;
        _idGenerator = idGenerator;
        _licenseSignatureService = licenseSignatureService;
        _machineFingerprintService = machineFingerprintService;
        _licenseService = licenseService;
    }

    public async Task<string> CreateOfflineRequestAsync(TenantId tenantId, long userId, LicenseOfflineRequest request, CancellationToken cancellationToken = default)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var entity = new LicenseGrant(
            _idGenerator.NextId(),
            token,
            LicenseGrantMode.Offline,
            "{}",
            "{}",
            DateTimeOffset.UtcNow);
        entity.Renew(DateTimeOffset.UtcNow.AddDays(365));
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return token;
    }

    public async Task<LicenseValidateResponse> ImportAsync(TenantId tenantId, long userId, LicenseImportRequest request, CancellationToken cancellationToken = default)
    {
        var envelope = _licenseSignatureService.Parse(request.LicenseContent)
            ?? throw new BusinessException(ErrorCodes.ValidationError, "授权文件格式无效。");
        if (!_licenseSignatureService.Verify(envelope))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "授权文件签名校验失败。");
        }

        var payload = envelope.Payload;
        if (!string.IsNullOrWhiteSpace(payload.MachineFingerprint) &&
            !_machineFingerprintService.Matches(payload.MachineFingerprint))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "授权文件与当前机器指纹不匹配。");
        }

        var featuresJson = JsonSerializer.Serialize(payload.Features, JsonOptions);
        var limitsJson = JsonSerializer.Serialize(payload.Limits, JsonOptions);
        var entity = new LicenseGrant(
            _idGenerator.NextId(),
            Guid.NewGuid().ToString("N"),
            LicenseGrantMode.Offline,
            featuresJson,
            limitsJson,
            DateTimeOffset.UtcNow);
        entity.Renew(payload.ExpiresAt);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await _licenseService.ReloadAsync(cancellationToken);
        return new LicenseValidateResponse(true, payload.Edition, entity.ExpiresAt?.ToString("O"), "导入成功");
    }

    public async Task<LicenseValidateResponse> ValidateAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var latest = await _db.Queryable<LicenseGrant>().OrderByDescending(x => x.IssuedAt).FirstAsync(cancellationToken);
        if (latest is null)
        {
            return new LicenseValidateResponse(false, "None", null, "未导入授权");
        }

        var now = DateTimeOffset.UtcNow;
        if (latest.ExpiresAt.HasValue && latest.ExpiresAt.Value <= now)
        {
            return new LicenseValidateResponse(false, "Standard", latest.ExpiresAt.Value.ToString("O"), "授权已过期");
        }

        return new LicenseValidateResponse(true, "Standard", latest.ExpiresAt?.ToString("O"), "授权有效");
    }
}

public sealed class ToolAuthorizationService : IToolAuthorizationService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public ToolAuthorizationService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<PagedResult<ToolAuthorizationPolicyResponse>> QueryPoliciesAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<ToolAuthorizationPolicy>();
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => new ToolAuthorizationPolicyResponse(
            x.Id.ToString(),
            x.ToolId,
            x.ToolName,
            x.PolicyType.ToString(),
            x.RateLimitQuota,
            x.AuditEnabled)).ToArray();
        return new PagedResult<ToolAuthorizationPolicyResponse>(items, total, pageIndex, pageSize);
    }

    public async Task<string> CreatePolicyAsync(TenantId tenantId, long userId, ToolAuthorizationPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new ToolAuthorizationPolicy(
            tenantId,
            _idGenerator.NextId(),
            request.ToolId,
            request.ToolName,
            ParsePolicyType(request.PolicyType),
            userId,
            DateTimeOffset.UtcNow);
        entity.UpdatePolicy(
            ParsePolicyType(request.PolicyType),
            request.RateLimitQuota,
            ParseNullableLong(request.ApprovalFlowId),
            request.ConditionJson,
            request.AuditEnabled,
            userId,
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id.ToString();
    }

    public async Task UpdatePolicyAsync(TenantId tenantId, long userId, long id, ToolAuthorizationPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<ToolAuthorizationPolicy>().FirstAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("策略不存在");
        entity.UpdatePolicy(
            ParsePolicyType(request.PolicyType),
            request.RateLimitQuota,
            ParseNullableLong(request.ApprovalFlowId),
            request.ConditionJson,
            request.AuditEnabled,
            userId,
            DateTimeOffset.UtcNow);
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ToolAuthorizationSimulateResponse> SimulateAsync(TenantId tenantId, ToolAuthorizationSimulateRequest request, CancellationToken cancellationToken = default)
    {
        var policy = await _db.Queryable<ToolAuthorizationPolicy>()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstAsync(x => x.ToolId == request.ToolId, cancellationToken);
        if (policy is null)
        {
            return new ToolAuthorizationSimulateResponse("Deny", string.Empty, 0);
        }

        return new ToolAuthorizationSimulateResponse(policy.PolicyType.ToString(), policy.Id.ToString(), policy.RateLimitQuota);
    }

    private static ToolAuthorizationPolicyType ParsePolicyType(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "allow" => ToolAuthorizationPolicyType.Allow,
            "requireapproval" => ToolAuthorizationPolicyType.RequireApproval,
            _ => ToolAuthorizationPolicyType.Deny
        };

    private static long? ParseNullableLong(string? value)
        => long.TryParse(value, out var parsed) ? parsed : null;
}
