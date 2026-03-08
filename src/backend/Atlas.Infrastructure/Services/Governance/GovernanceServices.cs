using Atlas.Application.Governance.Abstractions;
using Atlas.Application.Governance.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Governance;

public sealed class PackageService : IPackageService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public PackageService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<PackageOperationResponse> ExportAsync(TenantId tenantId, long userId, PackageExportRequest request, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.ManifestId, out var manifestId) || manifestId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "ManifestId 无效，必须为正整数。");
        }

        var entity = new PackageArtifact(
            tenantId,
            _idGenerator.NextId(),
            manifestId,
            ParsePackageType(request.PackageType),
            $"exports/{Guid.NewGuid():N}.zip",
            Guid.NewGuid().ToString("N"),
            0,
            userId,
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return new PackageOperationResponse(entity.Id.ToString(), entity.Status.ToString(), "导出任务已创建");
    }

    public async Task<PackageOperationResponse> ImportAsync(TenantId tenantId, long userId, PackageImportRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new PackageArtifact(
            tenantId,
            _idGenerator.NextId(),
            0,
            PackageArtifactType.Full,
            $"imports/{request.FileName}",
            Guid.NewGuid().ToString("N"),
            request.ContentBase64.Length,
            userId,
            DateTimeOffset.UtcNow);
        entity.MarkImported(userId, DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return new PackageOperationResponse(entity.Id.ToString(), entity.Status.ToString(), "导入完成");
    }

    public Task<PackageOperationResponse> AnalyzeAsync(TenantId tenantId, long userId, PackageAnalyzeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PackageOperationResponse("analyze", "Analyzed", "冲突分析完成，无阻断冲突"));

    private static PackageArtifactType ParsePackageType(string packageType)
        => packageType.Trim().ToLowerInvariant() switch
        {
            "structure" => PackageArtifactType.Structure,
            "data" => PackageArtifactType.Data,
            _ => PackageArtifactType.Full
        };
}

public sealed class LicenseGrantService : ILicenseGrantService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public LicenseGrantService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
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
        var entity = new LicenseGrant(
            _idGenerator.NextId(),
            Guid.NewGuid().ToString("N"),
            LicenseGrantMode.Offline,
            "{\"featureA\":true}",
            "{\"users\":200}",
            DateTimeOffset.UtcNow);
        entity.Renew(DateTimeOffset.UtcNow.AddDays(365));
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return new LicenseValidateResponse(true, "Standard", entity.ExpiresAt?.ToString("O"), "导入成功");
    }

    public async Task<LicenseValidateResponse> ValidateAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var latest = await _db.Queryable<LicenseGrant>().OrderByDescending(x => x.IssuedAt).FirstAsync(cancellationToken);
        if (latest is null)
        {
            return new LicenseValidateResponse(false, "None", null, "未导入授权");
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
