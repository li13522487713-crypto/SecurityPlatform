using Atlas.Application.Governance.Abstractions;
using Atlas.Application.Governance.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using SqlSugar;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.Governance;

public sealed class DlpService : IDlpService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public DlpService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<DataClassificationResponse>> GetClassificationsAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Queryable<DataClassification>()
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new DataClassificationResponse(
            item.Id.ToString(),
            item.Code,
            item.Name,
            item.Level,
            item.Scope,
            item.BaselineJson,
            item.UpdatedAt.ToString("O"))).ToArray();
    }

    public async Task<string> CreateClassificationAsync(TenantId tenantId, long userId, DataClassificationRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new DataClassification(
            tenantId,
            _idGenerator.NextId(),
            request.Code.Trim(),
            request.Name.Trim(),
            request.Level <= 0 ? 1 : request.Level,
            string.IsNullOrWhiteSpace(request.Scope) ? "tenant" : request.Scope.Trim(),
            NormalizeJson(request.BaselineJson, "{}"),
            userId,
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id.ToString();
    }

    public async Task<IReadOnlyList<SensitiveLabelResponse>> GetLabelsAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Queryable<SensitiveLabel>()
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new SensitiveLabelResponse(
            item.Id.ToString(),
            item.Code,
            item.Name,
            item.TargetType,
            item.RuleJson,
            item.UpdatedAt.ToString("O"))).ToArray();
    }

    public async Task<string> CreateLabelAsync(TenantId tenantId, long userId, SensitiveLabelRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new SensitiveLabel(
            tenantId,
            _idGenerator.NextId(),
            request.Code.Trim(),
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.TargetType) ? "field" : request.TargetType.Trim(),
            NormalizeJson(request.RuleJson, "{}"),
            userId,
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id.ToString();
    }

    public async Task<IReadOnlyList<DlpPolicyResponse>> GetPoliciesAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Queryable<DlpPolicy>()
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new DlpPolicyResponse(
            item.Id.ToString(),
            item.Name,
            item.Enabled,
            item.ScopeJson,
            item.ChannelRuleJson,
            item.UpdatedAt.ToString("O"))).ToArray();
    }

    public async Task<string> CreatePolicyAsync(TenantId tenantId, long userId, DlpPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new DlpPolicy(
            tenantId,
            _idGenerator.NextId(),
            request.Name.Trim(),
            request.Enabled,
            NormalizeJson(request.ScopeJson, "{}"),
            NormalizeJson(request.ChannelRuleJson, "{}"),
            userId,
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id.ToString();
    }

    public async Task<IReadOnlyList<OutboundChannelResponse>> GetOutboundChannelsAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Queryable<DlpOutboundChannel>()
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new OutboundChannelResponse(
            item.Id.ToString(),
            item.ChannelKey,
            item.DisplayName,
            item.ChannelType,
            item.Enabled,
            item.ConfigJson,
            item.UpdatedAt.ToString("O"))).ToArray();
    }

    public async Task<string> CreateOutboundChannelAsync(TenantId tenantId, long userId, OutboundChannelRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new DlpOutboundChannel(
            tenantId,
            _idGenerator.NextId(),
            request.ChannelKey.Trim(),
            request.DisplayName.Trim(),
            request.ChannelType.Trim(),
            request.Enabled,
            NormalizeJson(request.ConfigJson, "{}"),
            userId,
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id.ToString();
    }

    public async Task<object> BindMaskPolicyAsync(TenantId tenantId, long userId, DlpBindingRequest request, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.AppInstanceId, out var appInstanceId) || appInstanceId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "appInstanceId 无效。");
        }

        var existing = await _db.Queryable<AppExposurePolicy>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);
        var maskPolicies = ParseDictionary(existing?.MaskPoliciesJson);
        maskPolicies[request.DataSet.Trim()] = request.MaskFields?.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        var now = DateTimeOffset.UtcNow;
        var maskJson = JsonSerializer.Serialize(maskPolicies, JsonOptions);
        if (existing is null)
        {
            var created = new AppExposurePolicy(tenantId, _idGenerator.NextId(), appInstanceId, userId, now);
            created.Update("[]", "[]", maskJson, userId, now);
            await _db.Insertable(created).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            existing.Update(existing.ExposedDataSetsJson, existing.AllowedCommandsJson, maskJson, userId, now);
            await _db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }

        return new
        {
            appInstanceId = request.AppInstanceId,
            dataSet = request.DataSet,
            maskFields = maskPolicies[request.DataSet.Trim()]
        };
    }

    public Task<object> CreateExportJobAsync(TenantId tenantId, long userId, DlpTransferJobRequest request, CancellationToken cancellationToken = default)
        => CreateTransferJobAsync(tenantId, userId, request, "export", cancellationToken);

    public Task<object> CreateDownloadJobAsync(TenantId tenantId, long userId, DlpTransferJobRequest request, CancellationToken cancellationToken = default)
        => CreateTransferJobAsync(tenantId, userId, request, "download", cancellationToken);

    public async Task<object> CreateExternalShareApprovalAsync(TenantId tenantId, long userId, ExternalShareApprovalRequest request, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.AppInstanceId, out var appInstanceId) || appInstanceId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "appInstanceId 无效。");
        }

        var now = DateTimeOffset.UtcNow;
        var approval = new ExternalShareApproval(
            tenantId,
            _idGenerator.NextId(),
            appInstanceId,
            request.DataSet.Trim(),
            request.Target.Trim(),
            request.Reason.Trim(),
            "Pending",
            userId,
            now);
        await _db.Insertable(approval).ExecuteCommandAsync(cancellationToken);

        return new
        {
            id = approval.Id.ToString(),
            status = approval.Status,
            createdAt = approval.CreatedAt.ToString("O")
        };
    }

    public async Task<OutboundCheckResponse> CheckOutboundAsync(TenantId tenantId, long userId, OutboundCheckRequest request, CancellationToken cancellationToken = default)
    {
        _ = userId;
        if (!long.TryParse(request.AppInstanceId, out var appInstanceId) || appInstanceId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "appInstanceId 无效。");
        }

        var normalizedDataSet = request.DataSet.Trim();
        var policy = await _db.Queryable<AppExposurePolicy>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);

        var exposedDataSets = ParseArray(policy?.ExposedDataSetsJson);
        var decision = exposedDataSets.Contains(normalizedDataSet, StringComparer.OrdinalIgnoreCase) ? "Allow" : "Deny";
        var reason = decision == "Allow" ? "命中暴露白名单。": "数据集未授权外发。";
        var maskPolicies = ParseDictionary(policy?.MaskPoliciesJson);
        var maskedFields = maskPolicies.TryGetValue(normalizedDataSet, out var fields) ? fields : [];

        var now = DateTimeOffset.UtcNow;
        var leakageEvent = new LeakageEvent(
            tenantId,
            _idGenerator.NextId(),
            null,
            appInstanceId,
            normalizedDataSet,
            request.ChannelKey.Trim(),
            decision,
            reason,
            JsonSerializer.Serialize(new
            {
                target = request.Target,
                payload = NormalizeJson(request.PayloadJson, "{}")
            }, JsonOptions),
            now);
        await _db.Insertable(leakageEvent).ExecuteCommandAsync(cancellationToken);

        var evidence = new EvidencePackage(
            tenantId,
            _idGenerator.NextId(),
            leakageEvent.Id,
            JsonSerializer.Serialize(new
            {
                leakageEventId = leakageEvent.Id.ToString(),
                decision,
                reason,
                maskedFields
            }, JsonOptions),
            "Ready",
            now);
        await _db.Insertable(evidence).ExecuteCommandAsync(cancellationToken);

        return new OutboundCheckResponse(
            decision,
            reason,
            maskedFields,
            leakageEvent.Id.ToString(),
            evidence.Id.ToString());
    }

    public async Task<IReadOnlyList<LeakageEventResponse>> GetEventsAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Queryable<LeakageEvent>()
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new LeakageEventResponse(
            item.Id.ToString(),
            item.AppInstanceId.ToString(),
            item.DataSet,
            item.ChannelKey,
            item.Decision,
            item.Reason,
            item.TargetSummary,
            item.CreatedAt.ToString("O"))).ToArray();
    }

    public async Task<IReadOnlyList<EvidencePackageResponse>> GetEvidencePackagesAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Queryable<EvidencePackage>()
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new EvidencePackageResponse(
            item.Id.ToString(),
            item.LeakageEventId.ToString(),
            item.SummaryJson,
            item.Status,
            item.CreatedAt.ToString("O"))).ToArray();
    }

    private async Task<object> CreateTransferJobAsync(
        TenantId tenantId,
        long userId,
        DlpTransferJobRequest request,
        string jobType,
        CancellationToken cancellationToken)
    {
        var result = await CheckOutboundAsync(
            tenantId,
            userId,
            new OutboundCheckRequest(
                request.AppInstanceId,
                request.DataSet,
                request.ChannelKey,
                request.Target,
                "{}"),
            cancellationToken);
        return new
        {
            jobType,
            decision = result.Decision,
            reason = result.Reason,
            leakageEventId = result.LeakageEventId,
            evidencePackageId = result.EvidencePackageId
        };
    }

    private static string NormalizeJson(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return fallback;
        }

        try
        {
            using var doc = JsonDocument.Parse(input);
            return doc.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string[] ParseArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<string, string[]> ParseDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json, JsonOptions);
            if (parsed is null)
            {
                return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }

            return new Dictionary<string, string[]>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
