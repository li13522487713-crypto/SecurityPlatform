using System.Text.Json;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Abstractions;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class AppBridgeService : IAppBridgeQueryService, IAppBridgeCommandService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IIdempotencyRecordRepository _idempotencyRecordRepository;
    private readonly IAppCommandDispatcher _dispatcher;
    private readonly IAuditWriter _auditWriter;

    public AppBridgeService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGeneratorAccessor,
        IIdempotencyRecordRepository idempotencyRecordRepository,
        IAppCommandDispatcher dispatcher,
        IAuditWriter auditWriter)
    {
        _db = db;
        _idGeneratorAccessor = idGeneratorAccessor;
        _idempotencyRecordRepository = idempotencyRecordRepository;
        _dispatcher = dispatcher;
        _auditWriter = auditWriter;
    }

    public async Task<PagedResult<OnlineAppProjectionItem>> QueryOnlineAppsAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var appQuery = _db.Queryable<LowCodeApp>();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            appQuery = appQuery.Where(item => item.Name.Contains(keyword) || item.AppKey.Contains(keyword));
        }

        var total = await appQuery.CountAsync(cancellationToken);
        var appRows = await appQuery.OrderBy(item => item.Name)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        if (appRows.Count == 0)
        {
            return new PagedResult<OnlineAppProjectionItem>([], total, pageIndex, pageSize);
        }

        var appIds = appRows.Select(item => item.Id).Distinct().ToArray();
        var registrations = await _db.Queryable<AppBridgeRegistration>()
            .Where(item => SqlFunc.ContainsArray(appIds, item.AppInstanceId))
            .ToListAsync(cancellationToken);
        var registrationMap = registrations.ToDictionary(item => item.AppInstanceId);

        var items = appRows.Select(app =>
        {
            registrationMap.TryGetValue(app.Id, out var registration);
            var mode = registration?.Mode ?? AppBridgeMode.LocalManaged;
            var health = registration?.HealthStatus ?? AppBridgeHealthStatus.Unknown;
            var runtimeStatus = registration?.RuntimeStatus ?? app.Status.ToString();
            var releaseVersion = registration?.ReleaseVersion ?? app.Version.ToString();
            var lastSeenAt = registration?.LastSeenAt ?? app.UpdatedAt;

            return new OnlineAppProjectionItem(
                AppInstanceId: app.Id.ToString(),
                AppKey: app.AppKey,
                AppName: app.Name,
                BridgeMode: mode.ToString(),
                RuntimeStatus: runtimeStatus,
                HealthStatus: health.ToString(),
                ReleaseVersion: releaseVersion,
                LastSeenAt: lastSeenAt.ToString("O"));
        }).ToArray();

        return new PagedResult<OnlineAppProjectionItem>(items, total, pageIndex, pageSize);
    }

    public async Task<OnlineAppProjectionDetail?> GetOnlineAppByInstanceIdAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(item => item.Id == appInstanceId, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var registration = await _db.Queryable<AppBridgeRegistration>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);
        var supportedCommands = ParseStringArray(registration?.SupportedCommandsJson);

        return new OnlineAppProjectionDetail(
            AppInstanceId: app.Id.ToString(),
            AppKey: app.AppKey,
            AppName: app.Name,
            BridgeMode: (registration?.Mode ?? AppBridgeMode.LocalManaged).ToString(),
            RuntimeStatus: registration?.RuntimeStatus ?? app.Status.ToString(),
            HealthStatus: (registration?.HealthStatus ?? AppBridgeHealthStatus.Unknown).ToString(),
            ReleaseVersion: registration?.ReleaseVersion ?? app.Version.ToString(),
            BridgeEndpoint: registration?.BridgeEndpoint,
            SupportedCommands: supportedCommands,
            LastSeenAt: (registration?.LastSeenAt ?? app.UpdatedAt).ToString("O"));
    }

    public async Task<AppExposurePolicyResponse> GetExposurePolicyAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var policy = await _db.Queryable<AppExposurePolicy>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);
        if (policy is null)
        {
            return new AppExposurePolicyResponse(
                appInstanceId.ToString(),
                [],
                [],
                new Dictionary<string, IReadOnlyList<string>>(),
                DateTimeOffset.MinValue.ToString("O"));
        }

        return MapPolicy(policy);
    }

    public async Task<PagedResult<AppCommandListItem>> QueryCommandsAsync(
        TenantId tenantId,
        PagedRequest request,
        string? appInstanceId = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;
        var query = _db.Queryable<AppCommand>();
        if (!string.IsNullOrWhiteSpace(appInstanceId) && long.TryParse(appInstanceId, out var appId))
        {
            query = query.Where(item => item.AppInstanceId == appId);
        }

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AppCommandStatus>(status, true, out var statusValue))
        {
            query = query.Where(item => item.Status == statusValue);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(item => item.CommandType.Contains(keyword)
                || (item.AppKey != null && item.AppKey.Contains(keyword)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.CreatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(row => new AppCommandListItem(
            row.Id.ToString(),
            row.AppInstanceId.ToString(),
            row.AppKey,
            row.CommandType,
            row.RiskLevel.ToString(),
            row.DryRun,
            row.Status.ToString(),
            row.Initiator,
            row.CreatedAt.ToString("O"),
            row.UpdatedAt.ToString("O"))).ToArray();

        return new PagedResult<AppCommandListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<AppCommandDetail?> GetCommandByIdAsync(
        TenantId tenantId,
        long commandId,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var row = await _db.Queryable<AppCommand>()
            .FirstAsync(item => item.Id == commandId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        return MapCommandDetail(row);
    }

    public async Task<IReadOnlyList<AppCommandDetail>> GetPendingFederatedCommandsAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var rows = await _db.Queryable<AppCommand>()
            .Where(item => item.AppInstanceId == appInstanceId
                && (item.Status == AppCommandStatus.Pending || item.Status == AppCommandStatus.Dispatched))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(MapCommandDetail).ToArray();
    }

    public async Task<ExposedDataQueryResponse> QueryExposedDataAsync(
        TenantId tenantId,
        long appInstanceId,
        ExposedDataQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var policy = await _db.Queryable<AppExposurePolicy>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);
        var allowedDataSets = ParseStringArray(policy?.ExposedDataSetsJson);
        var targetDataSet = request.DataSet.Trim().ToLowerInvariant();
        if (!allowedDataSets.Contains(targetDataSet, StringComparer.OrdinalIgnoreCase))
        {
            return new ExposedDataQueryResponse(
                targetDataSet,
                new PagedResult<Dictionary<string, object?>>(Array.Empty<Dictionary<string, object?>>(), 0, request.Paged.PageIndex, request.Paged.PageSize));
        }

        var pageIndex = request.Paged.PageIndex <= 0 ? 1 : request.Paged.PageIndex;
        var pageSize = request.Paged.PageSize <= 0 ? 20 : request.Paged.PageSize;
        var maskPolicies = ParseMaskPolicy(policy?.MaskPoliciesJson);
        var maskedFields = maskPolicies.TryGetValue(targetDataSet, out var fields)
            ? fields
            : [];

        PagedResult<Dictionary<string, object?>> result = targetDataSet switch
        {
            "users" => await QueryUsersAsync(pageIndex, pageSize, request.Paged.Keyword, maskedFields, cancellationToken),
            "departments" => await QueryDepartmentsAsync(pageIndex, pageSize, request.Paged.Keyword, maskedFields, cancellationToken),
            "positions" => await QueryPositionsAsync(pageIndex, pageSize, request.Paged.Keyword, maskedFields, cancellationToken),
            "projects" => await QueryProjectsAsync(pageIndex, pageSize, request.Paged.Keyword, maskedFields, cancellationToken),
            _ => new PagedResult<Dictionary<string, object?>>(Array.Empty<Dictionary<string, object?>>(), 0, pageIndex, pageSize)
        };

        var audit = new AuditRecord(
            tenantId,
            "system",
            "appbridge.exposure.query",
            "Success",
            $"App:{appInstanceId}/DataSet:{targetDataSet}",
            null,
            null);
        await _auditWriter.WriteAsync(audit, cancellationToken);

        return new ExposedDataQueryResponse(targetDataSet, result);
    }

    public async Task<AppExposurePolicyResponse> UpdateExposurePolicyAsync(
        TenantId tenantId,
        long userId,
        long appInstanceId,
        AppExposurePolicyUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var dataSetsJson = JsonSerializer.Serialize(request.ExposedDataSets ?? [], JsonOptions);
        var commandsJson = JsonSerializer.Serialize(request.AllowedCommands ?? [], JsonOptions);
        var masksJson = JsonSerializer.Serialize(request.MaskPolicies ?? new Dictionary<string, IReadOnlyList<string>>(), JsonOptions);

        var policy = await _db.Queryable<AppExposurePolicy>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);
        if (policy is null)
        {
            policy = new AppExposurePolicy(
                tenantId,
                _idGeneratorAccessor.NextId(),
                appInstanceId,
                userId,
                now);
            policy.Update(dataSetsJson, commandsJson, masksJson, userId, now);
            await _db.Insertable(policy).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            policy.Update(dataSetsJson, commandsJson, masksJson, userId, now);
            await _db.Updateable(policy).ExecuteCommandAsync(cancellationToken);
        }

        return MapPolicy(policy);
    }

    public async Task<long> CreateCommandAsync(
        TenantId tenantId,
        long userId,
        AppCommandCreateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdempotencyKey = idempotencyKey?.Trim() ?? string.Empty;
        if (normalizedIdempotencyKey.Length == 0)
        {
            throw new BusinessException("缺少 Idempotency-Key。", ErrorCodes.ValidationError);
        }

        if (!long.TryParse(request.AppInstanceId, out var appInstanceId))
        {
            throw new InvalidOperationException("appInstanceId invalid.");
        }

        var policy = await _db.Queryable<AppExposurePolicy>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);
        if (policy is null)
        {
            throw new BusinessException("当前应用未配置命令授权策略，禁止下发命令。", ErrorCodes.Forbidden);
        }

        var normalizedCommandType = request.CommandType?.Trim() ?? string.Empty;
        var allowedCommands = ParseStringArray(policy.AllowedCommandsJson);
        if (allowedCommands.Count == 0
            || !allowedCommands.Contains(normalizedCommandType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException($"命令 {normalizedCommandType} 未被授权。", ErrorCodes.Forbidden);
        }

        var now = DateTimeOffset.UtcNow;
        var riskLevel = ClassifyRiskLevel(normalizedCommandType);
        if (riskLevel == AppCommandRiskLevel.High && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessException("高风险命令必须填写 reason。", ErrorCodes.ValidationError);
        }
        const string apiName = "appbridge.commands.create";
        var requestHash = BuildIdempotencyRequestHash(request);
        var existing = await _idempotencyRecordRepository.FindActiveAsync(
            tenantId,
            userId,
            apiName,
            normalizedIdempotencyKey,
            now,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new BusinessException("幂等键冲突：同一 Idempotency-Key 的请求体不一致。", ErrorCodes.ValidationError);
            }

            if (existing.Status == IdempotencyStatus.Completed
                && long.TryParse(existing.ResourceId, out var existingCommandId))
            {
                return existingCommandId;
            }

            throw new BusinessException("命令正在处理中，请稍后重试。", ErrorCodes.ValidationError);
        }

        var idempotencyRecord = new IdempotencyRecord(
            tenantId,
            userId,
            apiName,
            normalizedIdempotencyKey,
            requestHash,
            now,
            now.AddHours(24),
            _idGeneratorAccessor.NextId());
        var reserved = await _idempotencyRecordRepository.TryAddAsync(idempotencyRecord, cancellationToken);
        if (!reserved)
        {
            var concurrentRecord = await _idempotencyRecordRepository.FindActiveAsync(
                tenantId,
                userId,
                apiName,
                normalizedIdempotencyKey,
                now,
                cancellationToken);
            if (concurrentRecord is not null
                && string.Equals(concurrentRecord.RequestHash, requestHash, StringComparison.Ordinal)
                && concurrentRecord.Status == IdempotencyStatus.Completed
                && long.TryParse(concurrentRecord.ResourceId, out var concurrentCommandId))
            {
                return concurrentCommandId;
            }

            throw new BusinessException("幂等请求冲突，请稍后重试。", ErrorCodes.ValidationError);
        }

        var command = new AppCommand(
            tenantId,
            _idGeneratorAccessor.NextId(),
            appInstanceId,
            normalizedCommandType,
            string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson,
            request.DryRun,
            riskLevel,
            normalizedIdempotencyKey,
            userId.ToString(),
            request.Reason,
            now);

        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(item => item.Id == appInstanceId, cancellationToken);
        command.BindAppKey(app?.AppKey);

        await _db.Insertable(command).ExecuteCommandAsync(cancellationToken);

        var registration = await _db.Queryable<AppBridgeRegistration>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);

        if (riskLevel == AppCommandRiskLevel.High && !request.DryRun)
        {
            command.BindApprovalRequest($"APR-{_idGeneratorAccessor.NextId()}");
            command.MarkDispatched(DateTimeOffset.UtcNow, "High-risk command is waiting for approval.");
            await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);
        }
        else if (registration?.Mode == AppBridgeMode.Federated)
        {
            command.MarkDispatched(DateTimeOffset.UtcNow, "Federated command pending pull.");
            await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await _dispatcher.DispatchLocalAsync(tenantId, command, cancellationToken);
        }

        var audit = new AuditRecord(
            tenantId,
            userId.ToString(),
            "appbridge.command.create",
            "Success",
            $"Command:{command.Id}/Type:{command.CommandType}",
            null,
            null);
        await _auditWriter.WriteAsync(audit, cancellationToken);
        idempotencyRecord.Complete(
            statusCode: 200,
            responseBody: JsonSerializer.Serialize(new { id = command.Id }, JsonOptions),
            responseContentType: "application/json",
            resourceId: command.Id.ToString(),
            completedAt: DateTimeOffset.UtcNow);
        await _idempotencyRecordRepository.UpdateAsync(idempotencyRecord, cancellationToken);

        return command.Id;
    }

    public async Task AcknowledgeFederatedCommandAsync(
        TenantId tenantId,
        long commandId,
        FederatedCommandAckRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var command = await _db.Queryable<AppCommand>()
            .FirstAsync(item => item.Id == commandId, cancellationToken)
            ?? throw new InvalidOperationException("Command not found.");

        command.MarkAcked(DateTimeOffset.UtcNow, request.Message);
        await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);

        var audit = new AuditRecord(
            tenantId,
            "federated",
            "appbridge.command.ack",
            "Success",
            $"Command:{command.Id}/Type:{command.CommandType}",
            null,
            null);
        await _auditWriter.WriteAsync(audit, cancellationToken);
    }

    public async Task CompleteFederatedCommandAsync(
        TenantId tenantId,
        long commandId,
        FederatedCommandResultRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var command = await _db.Queryable<AppCommand>()
            .FirstAsync(item => item.Id == commandId, cancellationToken)
            ?? throw new InvalidOperationException("Command not found.");

        var now = DateTimeOffset.UtcNow;
        var status = request.Status.Trim().ToLowerInvariant();
        if (status is "succeeded" or "success")
        {
            command.MarkSucceeded(string.IsNullOrWhiteSpace(request.ResultJson) ? "{}" : request.ResultJson, now, request.Message);
        }
        else if (status is "cancelled" or "canceled")
        {
            command.MarkCancelled(now, request.Message);
        }
        else
        {
            command.MarkFailed(string.IsNullOrWhiteSpace(request.ResultJson) ? "{}" : request.ResultJson, now, request.Message);
        }

        await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);

        var audit = new AuditRecord(
            tenantId,
            "federated",
            "appbridge.command.result",
            "Success",
            $"Command:{command.Id}/Status:{command.Status}",
            null,
            null);
        await _auditWriter.WriteAsync(audit, cancellationToken);
    }

    public async Task RegisterFederatedAsync(
        TenantId tenantId,
        FederatedRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        await UpsertFederatedRegistrationAsync(tenantId, request, cancellationToken);
        var audit = new AuditRecord(
            tenantId,
            "federated",
            "appbridge.federated.register",
            "Success",
            $"App:{request.AppInstanceId}",
            null,
            null);
        await _auditWriter.WriteAsync(audit, cancellationToken);
    }

    public async Task HeartbeatFederatedAsync(
        TenantId tenantId,
        FederatedHeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        await UpsertFederatedRegistrationAsync(
            tenantId,
            new FederatedRegisterRequest(
                request.AppInstanceId,
                request.AppKey,
                request.RuntimeStatus,
                request.HealthStatus,
                request.ReleaseVersion,
                request.BridgeEndpoint,
                request.SupportedCommands),
            cancellationToken);
        var audit = new AuditRecord(
            tenantId,
            "federated",
            "appbridge.federated.heartbeat",
            "Success",
            $"App:{request.AppInstanceId}",
            null,
            null);
        await _auditWriter.WriteAsync(audit, cancellationToken);
    }

    private async Task UpsertFederatedRegistrationAsync(
        TenantId tenantId,
        FederatedRegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(request.AppInstanceId, out var appInstanceId))
        {
            throw new InvalidOperationException("appInstanceId invalid.");
        }

        var now = DateTimeOffset.UtcNow;
        var registration = await _db.Queryable<AppBridgeRegistration>()
            .FirstAsync(item => item.AppInstanceId == appInstanceId, cancellationToken);
        var commandsJson = JsonSerializer.Serialize(request.SupportedCommands ?? [], JsonOptions);
        var metadataJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["source"] = "federated",
            ["reportedAt"] = now.ToString("O")
        }, JsonOptions);
        if (registration is null)
        {
            registration = new AppBridgeRegistration(
                tenantId,
                _idGeneratorAccessor.NextId(),
                appInstanceId,
                request.AppKey,
                AppBridgeMode.Federated,
                now);
            registration.UpdateHeartbeat(
                request.RuntimeStatus,
                ParseHealthStatus(request.HealthStatus),
                request.ReleaseVersion,
                request.BridgeEndpoint,
                commandsJson,
                metadataJson,
                now);
            await _db.Insertable(registration).ExecuteCommandAsync(cancellationToken);
            return;
        }

        registration.UpdateHeartbeat(
            request.RuntimeStatus,
            ParseHealthStatus(request.HealthStatus),
            request.ReleaseVersion,
            request.BridgeEndpoint,
            commandsJson,
            metadataJson,
            now);
        await _db.Updateable(registration).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<PagedResult<Dictionary<string, object?>>> QueryUsersAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        IReadOnlyList<string> maskedFields,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<UserAccount>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var text = keyword.Trim();
            query = query.Where(item => item.Username.Contains(text) || item.DisplayName.Contains(text));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.Id)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(item => BuildDictionary(maskedFields, new Dictionary<string, object?>
        {
            ["id"] = item.Id.ToString(),
            ["username"] = item.Username,
            ["displayName"] = item.DisplayName,
            ["email"] = item.Email,
            ["phoneNumber"] = item.PhoneNumber
        })).ToArray();
        return new PagedResult<Dictionary<string, object?>>(items, total, pageIndex, pageSize);
    }

    private async Task<PagedResult<Dictionary<string, object?>>> QueryDepartmentsAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        IReadOnlyList<string> maskedFields,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Department>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var text = keyword.Trim();
            query = query.Where(item => item.Name.Contains(text) || item.Code.Contains(text));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.SortOrder).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(item => BuildDictionary(maskedFields, new Dictionary<string, object?>
        {
            ["id"] = item.Id.ToString(),
            ["name"] = item.Name,
            ["code"] = item.Code,
            ["parentId"] = item.ParentId?.ToString()
        })).ToArray();
        return new PagedResult<Dictionary<string, object?>>(items, total, pageIndex, pageSize);
    }

    private async Task<PagedResult<Dictionary<string, object?>>> QueryPositionsAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        IReadOnlyList<string> maskedFields,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Position>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var text = keyword.Trim();
            query = query.Where(item => item.Name.Contains(text) || item.Code.Contains(text));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.SortOrder).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(item => BuildDictionary(maskedFields, new Dictionary<string, object?>
        {
            ["id"] = item.Id.ToString(),
            ["name"] = item.Name,
            ["code"] = item.Code,
            ["isActive"] = item.IsActive
        })).ToArray();
        return new PagedResult<Dictionary<string, object?>>(items, total, pageIndex, pageSize);
    }

    private async Task<PagedResult<Dictionary<string, object?>>> QueryProjectsAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        IReadOnlyList<string> maskedFields,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Project>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var text = keyword.Trim();
            query = query.Where(item => item.Name.Contains(text) || item.Code.Contains(text));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.Id).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(item => BuildDictionary(maskedFields, new Dictionary<string, object?>
        {
            ["id"] = item.Id.ToString(),
            ["name"] = item.Name,
            ["code"] = item.Code,
            ["isActive"] = item.IsActive
        })).ToArray();
        return new PagedResult<Dictionary<string, object?>>(items, total, pageIndex, pageSize);
    }

    private static Dictionary<string, object?> BuildDictionary(
        IReadOnlyList<string> maskedFields,
        Dictionary<string, object?> source)
    {
        if (maskedFields.Count == 0)
        {
            return source;
        }

        foreach (var field in maskedFields)
        {
            if (!source.TryGetValue(field, out var value) || value is null)
            {
                continue;
            }

            source[field] = value switch
            {
                string text when text.Length <= 2 => "**",
                string text => $"{text[..1]}***{text[^1..]}",
                _ => "***"
            };
        }

        return source;
    }

    private static AppExposurePolicyResponse MapPolicy(AppExposurePolicy policy)
    {
        return new AppExposurePolicyResponse(
            policy.AppInstanceId.ToString(),
            ParseStringArray(policy.ExposedDataSetsJson),
            ParseStringArray(policy.AllowedCommandsJson),
            ParseMaskPolicy(policy.MaskPoliciesJson),
            policy.UpdatedAt.ToString("O"));
    }

    private static AppCommandDetail MapCommandDetail(AppCommand row)
    {
        return new AppCommandDetail(
            row.Id.ToString(),
            row.AppInstanceId.ToString(),
            row.AppKey,
            row.CommandType,
            row.RiskLevel.ToString(),
            row.PayloadJson,
            row.DryRun,
            row.Status.ToString(),
            row.Initiator,
            row.Reason,
            row.ApprovalRequestId,
            row.Message,
            row.ResultJson,
            row.CreatedAt.ToString("O"),
            row.UpdatedAt.ToString("O"),
            row.StartedAt?.ToString("O"),
            row.CompletedAt?.ToString("O"));
    }

    private static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return rows?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static Dictionary<string, IReadOnlyList<string>> ParseMaskPolicy(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var row = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, JsonOptions);
            if (row is null)
            {
                return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            }

            return row.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)(pair.Value ?? new List<string>()),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static AppBridgeHealthStatus ParseHealthStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AppBridgeHealthStatus.Unknown;
        }

        if (Enum.TryParse<AppBridgeHealthStatus>(value, true, out var parsed))
        {
            return parsed;
        }

        return AppBridgeHealthStatus.Unknown;
    }

    private static string BuildIdempotencyRequestHash(AppCommandCreateRequest request)
    {
        var normalizedPayload = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson.Trim();
        var source = $"{request.AppInstanceId}|{request.CommandType?.Trim()}|{request.DryRun}|{request.Reason?.Trim()}|{normalizedPayload}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash);
    }

    private static AppCommandRiskLevel ClassifyRiskLevel(string commandType)
    {
        var normalized = (commandType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "release.activate")
        {
            return AppCommandRiskLevel.High;
        }

        if (normalized is "runtime.restart")
        {
            return AppCommandRiskLevel.Medium;
        }

        if (normalized is "organization.sync-structure")
        {
            return AppCommandRiskLevel.Low;
        }

        return AppCommandRiskLevel.Medium;
    }
}

public interface ILocalAppCommandHandler
{
    string CommandType { get; }

    Task<LocalAppCommandDispatchResult> ExecuteAsync(
        TenantId tenantId,
        AppCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record LocalAppCommandDispatchResult(
    bool Success,
    string Message,
    string ResultJson);

public sealed class OrganizationSyncStructureCommandHandler : ILocalAppCommandHandler
{
    public string CommandType => "organization.sync-structure";

    public Task<LocalAppCommandDispatchResult> ExecuteAsync(
        TenantId tenantId,
        AppCommand command,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        _ = cancellationToken;
        var payload = string.IsNullOrWhiteSpace(command.PayloadJson) ? "{}" : command.PayloadJson;
        var result = JsonSerializer.Serialize(new
        {
            command = CommandType,
            dryRun = command.DryRun,
            mode = command.DryRun ? "preview" : "applied",
            diff = new
            {
                departmentsAdded = command.DryRun ? 2 : 0,
                positionsSynced = command.DryRun ? 4 : 4
            },
            payload
        });
        var message = command.DryRun
            ? "组织结构同步 dry-run 完成，已返回 diff。"
            : "组织结构同步执行完成。";
        return Task.FromResult(new LocalAppCommandDispatchResult(true, message, result));
    }
}

public sealed class RuntimeRestartCommandHandler : ILocalAppCommandHandler
{
    public string CommandType => "runtime.restart";

    public Task<LocalAppCommandDispatchResult> ExecuteAsync(
        TenantId tenantId,
        AppCommand command,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        _ = cancellationToken;
        var result = JsonSerializer.Serialize(new
        {
            command = CommandType,
            dryRun = command.DryRun,
            mode = command.DryRun ? "preview" : "executed",
            diff = new
            {
                restartWindow = "00:00:05",
                affectedNodes = 1
            }
        });
        var message = command.DryRun
            ? "运行时重启 dry-run 完成，未执行实际重启。"
            : "运行时重启执行完成。";
        return Task.FromResult(new LocalAppCommandDispatchResult(true, message, result));
    }
}

public sealed class ReleaseActivateCommandHandler : ILocalAppCommandHandler
{
    public string CommandType => "release.activate";

    public Task<LocalAppCommandDispatchResult> ExecuteAsync(
        TenantId tenantId,
        AppCommand command,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        _ = cancellationToken;
        var result = JsonSerializer.Serialize(new
        {
            command = CommandType,
            dryRun = command.DryRun,
            mode = command.DryRun ? "preview" : "executed",
            diff = new
            {
                targetRelease = "latest",
                expectedDowntimeSeconds = 0
            }
        });
        var message = command.DryRun
            ? "发布激活 dry-run 完成，已返回 diff 预览。"
            : "发布激活执行完成。";
        return Task.FromResult(new LocalAppCommandDispatchResult(true, message, result));
    }
}

public sealed class LocalAppCommandDispatcher : IAppCommandDispatcher
{
    private readonly ISqlSugarClient _db;
    private readonly IAuditWriter _auditWriter;
    private readonly IReadOnlyDictionary<string, ILocalAppCommandHandler> _handlers;

    public LocalAppCommandDispatcher(
        ISqlSugarClient db,
        IAuditWriter auditWriter,
        IEnumerable<ILocalAppCommandHandler> handlers)
    {
        _db = db;
        _auditWriter = auditWriter;
        _handlers = handlers
            .GroupBy(item => item.CommandType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task DispatchLocalAsync(TenantId tenantId, AppCommand command, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        command.MarkDispatched(now, "Local dispatcher accepted command.");
        await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);

        command.MarkAcked(DateTimeOffset.UtcNow, "Local adapter acknowledged.");
        await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);

        command.MarkRunning(DateTimeOffset.UtcNow, "Local adapter executing.");
        await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);

        if (!_handlers.TryGetValue(command.CommandType, out var handler))
        {
            var missingHandlerResult = JsonSerializer.Serialize(new
            {
                error = "handler_not_found",
                commandType = command.CommandType
            });
            command.MarkFailed(missingHandlerResult, DateTimeOffset.UtcNow, "Local handler not found.");
            await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            var dispatchResult = await handler.ExecuteAsync(tenantId, command, cancellationToken);
            if (dispatchResult.Success)
            {
                command.MarkSucceeded(dispatchResult.ResultJson, DateTimeOffset.UtcNow, dispatchResult.Message);
            }
            else
            {
                command.MarkFailed(dispatchResult.ResultJson, DateTimeOffset.UtcNow, dispatchResult.Message);
            }

            await _db.Updateable(command).ExecuteCommandAsync(cancellationToken);
        }

        var audit = new AuditRecord(
            tenantId,
            command.Initiator,
            "appbridge.command.dispatch",
            command.Status == AppCommandStatus.Succeeded ? "Success" : "Failed",
            $"Command:{command.Id}/Type:{command.CommandType}",
            null,
            null);
        await _auditWriter.WriteAsync(audit, cancellationToken);
    }
}
