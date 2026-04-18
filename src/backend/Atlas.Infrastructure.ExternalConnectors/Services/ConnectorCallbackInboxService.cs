using System.Text.Json;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.Core.Security;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

public sealed class ConnectorCallbackInboxService : IConnectorCallbackInboxService
{
    private readonly IConnectorRegistry _registry;
    private readonly IExternalIdentityProviderRepository _providerRepository;
    private readonly IExternalCallbackEventRepository _eventRepository;
    private readonly IExternalDirectorySyncService _directorySyncService;
    private readonly IExternalApprovalInstanceLinkRepository _instanceLinkRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly IReplayGuard _replayGuard;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConnectorCallbackInboxService> _logger;

    public ConnectorCallbackInboxService(
        IConnectorRegistry registry,
        IExternalIdentityProviderRepository providerRepository,
        IExternalCallbackEventRepository eventRepository,
        IExternalDirectorySyncService directorySyncService,
        IExternalApprovalInstanceLinkRepository instanceLinkRepository,
        ISecretProtector secretProtector,
        IReplayGuard replayGuard,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGenerator,
        TimeProvider timeProvider,
        ILogger<ConnectorCallbackInboxService> logger)
    {
        _registry = registry;
        _providerRepository = providerRepository;
        _eventRepository = eventRepository;
        _directorySyncService = directorySyncService;
        _instanceLinkRepository = instanceLinkRepository;
        _secretProtector = secretProtector;
        _replayGuard = replayGuard;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ConnectorCallbackInboxResult> AcceptAsync(long providerId, string topic, IReadOnlyDictionary<string, string> query, IReadOnlyDictionary<string, string> headers, byte[] body, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var provider = await _providerRepository.GetByIdAsync(tenantId, providerId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"Provider {providerId} not found.");

        var providerType = provider.ProviderType.ToProviderType();
        var verifier = _registry.GetEventVerifier(providerType);
        // 把 token / aes key / verification token 通过 headers 注入给 verifier；这里基于 SecretJson 解码后的字段。
        var enrichedHeaders = EnrichHeadersWithSecrets(headers, provider, providerType);

        ConnectorWebhookEnvelope envelope;
        try
        {
            envelope = verifier.Verify(query, enrichedHeaders, body);
        }
        catch (ConnectorException ex)
        {
            _logger.LogWarning(ex, "Connector webhook verify failed for provider {ProviderId} topic {Topic}.", providerId, topic);
            return new ConnectorCallbackInboxResult { Status = "rejected", Reason = ex.Code, Topic = topic };
        }

        var idempotencyKey = envelope.IdempotencyKey ?? $"{topic}:{Guid.NewGuid():N}";

        // 重放 + 幂等防护
        if (!_replayGuard.TryAccept(idempotencyKey, envelope.ReceivedAt, TimeSpan.FromMinutes(5)))
        {
            return new ConnectorCallbackInboxResult { Status = "duplicated", IdempotencyKey = idempotencyKey, Topic = envelope.Topic };
        }
        var existing = await _eventRepository.GetByIdempotencyAsync(tenantId, providerId, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            existing.MarkDuplicated(_timeProvider.GetUtcNow());
            await _eventRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            return new ConnectorCallbackInboxResult { Status = "duplicated", IdempotencyKey = idempotencyKey, Topic = envelope.Topic };
        }

        var entity = new ExternalCallbackEvent(
            tenantId,
            _idGenerator.NextId(),
            providerId,
            ResolveKind(topic),
            envelope.Topic,
            idempotencyKey,
            _secretProtector.Encrypt(envelope.PayloadJson),
            JsonSerializer.Serialize(headers),
            envelope.ReceivedAt);
        await _eventRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        entity.MarkVerified(_timeProvider.GetUtcNow());
        await _eventRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);

        try
        {
            await ApplyEventAsync(providerId, envelope, cancellationToken).ConfigureAwait(false);
            entity.MarkProcessed(_timeProvider.GetUtcNow());
            await _eventRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            return new ConnectorCallbackInboxResult { Status = "accepted", IdempotencyKey = idempotencyKey, Topic = envelope.Topic };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Connector webhook handler failed (provider={ProviderId}, topic={Topic}).", providerId, envelope.Topic);
            entity.MarkFailed(ex.Message, retryDelaySeconds: 60, _timeProvider.GetUtcNow(), maxRetry: 5);
            await _eventRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            return new ConnectorCallbackInboxResult { Status = "deferred", IdempotencyKey = idempotencyKey, Topic = envelope.Topic, Reason = ex.Message };
        }
    }

    private async Task ApplyEventAsync(long providerId, ConnectorWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.Topic)
        {
            case "change_contact": // 企微通讯录变更
            case "contact.user.updated_v3": // 飞书通讯录变更
            case "contact.user.created_v3":
            case "contact.user.deleted_v3":
            case "contact.department.updated_v3":
            case "contact.department.created_v3":
            case "contact.department.deleted_v3":
                await DispatchDirectoryEventAsync(providerId, envelope, cancellationToken).ConfigureAwait(false);
                break;

            // 审批状态变更：把外部系统的 sp_no/instance_code → 本地 ApprovalProcessInstance.LocalInstanceId 状态推进。
            case "sys_approval_change": // 企微 OA 审批
            case "approval_instance":   // 飞书三方审批 + 飞书审批中心
            case "approval_task":       // 飞书审批任务级
            case "bpms_instance_change": // 钉钉新版工作流
            case "bpms_task_change":     // 钉钉任务节点
                await DispatchApprovalEventAsync(providerId, envelope, cancellationToken).ConfigureAwait(false);
                break;

            default:
                _logger.LogInformation("Connector webhook topic '{Topic}' has no inline handler; remained verified for downstream processing.", envelope.Topic);
                break;
        }
    }

    private async Task DispatchApprovalEventAsync(long providerId, ConnectorWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        var (externalInstanceId, status) = ExtractApprovalEventCore(envelope);
        if (string.IsNullOrEmpty(externalInstanceId))
        {
            _logger.LogWarning("Approval event missing externalInstanceId; topic={Topic}.", envelope.Topic);
            return;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var link = await _instanceLinkRepository.GetByExternalAsync(tenantId, providerId, externalInstanceId, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            _logger.LogInformation("Approval event for externalInstanceId={ExternalInstanceId} has no local link; saving as verified for later replay.", externalInstanceId);
            return;
        }

        link.RecordExternalStatus(status ?? "Unknown", _timeProvider.GetUtcNow());
        await _instanceLinkRepository.UpdateAsync(link, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Approval event applied: link={LinkId} → status={Status}.", link.Id, status);
    }

    private static (string ExternalInstanceId, string? Status) ExtractApprovalEventCore(ConnectorWebhookEnvelope envelope)
    {
        try
        {
            using var doc = JsonDocument.Parse(envelope.PayloadJson);
            var root = doc.RootElement;

            // 企微：xml 转 JSON 后 ApprovalInfo.SpNo / SpStatus
            if (root.TryGetProperty("ApprovalInfo", out var ap))
            {
                var spNo = ap.TryGetProperty("SpNo", out var sn) ? sn.GetString() : null;
                var spStatus = ap.TryGetProperty("SpStatus", out var ss) ? ss.GetString() : null;
                if (!string.IsNullOrEmpty(spNo))
                {
                    return (spNo, spStatus);
                }
            }
            // 飞书：event.instance_code / event.status
            if (root.TryGetProperty("event", out var ev))
            {
                var ic = ev.TryGetProperty("instance_code", out var ic2) ? ic2.GetString() : null;
                var st = ev.TryGetProperty("status", out var st2) ? st2.GetString() : null;
                if (!string.IsNullOrEmpty(ic))
                {
                    return (ic, st);
                }
            }
            // 钉钉：processInstanceId / result
            if (root.TryGetProperty("processInstanceId", out var pid))
            {
                var status = root.TryGetProperty("result", out var rs) ? rs.GetString() : null;
                return (pid.GetString() ?? string.Empty, status);
            }
        }
        catch (JsonException)
        {
        }
        return (string.Empty, null);
    }

    public async Task<int> ProcessPendingRetriesAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
        {
            return 0;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var pending = await _eventRepository.ListPendingRetryAsync(tenantId, batchSize, cancellationToken).ConfigureAwait(false);
        if (pending.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        foreach (var entity in pending)
        {
            // NextRetryAt 还没到的跳过；仓储应已过滤但兜底再判一次。
            if (entity.NextRetryAt is { } next && next > _timeProvider.GetUtcNow())
            {
                continue;
            }

            var envelope = ReconstructEnvelope(entity);
            try
            {
                await ApplyEventAsync(entity.ProviderId, envelope, cancellationToken).ConfigureAwait(false);
                entity.MarkProcessed(_timeProvider.GetUtcNow());
                await _eventRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Connector inbox retry failed: event={EventId}, retryCount={RetryCount}.", entity.Id, entity.RetryCount);
                entity.MarkFailed(ex.Message, retryDelaySeconds: ComputeBackoffSeconds(entity.RetryCount), _timeProvider.GetUtcNow(), maxRetry: 5);
                await _eventRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            }
        }
        return processed;
    }

    private ConnectorWebhookEnvelope ReconstructEnvelope(ExternalCallbackEvent entity)
    {
        var payload = _secretProtector.Decrypt(entity.RawPayloadEncrypted) ?? "{}";
        return new ConnectorWebhookEnvelope
        {
            ProviderType = string.Empty,
            Topic = entity.Topic,
            PayloadJson = payload,
            IdempotencyKey = entity.IdempotencyKey,
            ReceivedAt = entity.ReceivedAt,
        };
    }

    private static int ComputeBackoffSeconds(int retryCount)
    {
        // 60s, 120s, 240s, 480s, 960s。指数退避配合 maxRetry=5。
        var seconds = 60 * Math.Pow(2, Math.Min(retryCount, 4));
        return (int)Math.Min(seconds, 3600);
    }

    private async Task DispatchDirectoryEventAsync(long providerId, ConnectorWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        var kind = ResolveDirectoryEventKind(envelope.Topic);
        var entityId = ExtractEntityId(envelope);
        if (string.IsNullOrEmpty(entityId))
        {
            _logger.LogWarning("Directory event has no entityId; skipping.");
            return;
        }
        var directoryEvent = new ExternalDirectoryEvent
        {
            ProviderType = envelope.ProviderType,
            ProviderTenantId = string.Empty,
            Kind = kind,
            EntityId = entityId,
            OccurredAt = envelope.ReceivedAt,
            RawJson = envelope.PayloadJson,
        };
        await _directorySyncService.ApplyIncrementalEventAsync(providerId, directoryEvent, "webhook", cancellationToken).ConfigureAwait(false);
    }

    private static ExternalDirectoryEventKind ResolveDirectoryEventKind(string topic) => topic switch
    {
        "contact.user.created_v3" => ExternalDirectoryEventKind.UserCreated,
        "contact.user.updated_v3" => ExternalDirectoryEventKind.UserUpdated,
        "contact.user.deleted_v3" => ExternalDirectoryEventKind.UserDeleted,
        "contact.department.created_v3" => ExternalDirectoryEventKind.DepartmentCreated,
        "contact.department.updated_v3" => ExternalDirectoryEventKind.DepartmentUpdated,
        "contact.department.deleted_v3" => ExternalDirectoryEventKind.DepartmentDeleted,
        _ => ExternalDirectoryEventKind.UserUpdated,
    };

    private static string ExtractEntityId(ConnectorWebhookEnvelope envelope)
    {
        try
        {
            using var doc = JsonDocument.Parse(envelope.PayloadJson);
            if (doc.RootElement.TryGetProperty("event", out var ev))
            {
                if (ev.TryGetProperty("user", out var user) && user.TryGetProperty("user_id", out var uid))
                {
                    return uid.GetString() ?? string.Empty;
                }
                if (ev.TryGetProperty("department", out var dept) && dept.TryGetProperty("department_id", out var did))
                {
                    return did.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException) { }
        return string.Empty;
    }

    private static CallbackInboxKind ResolveKind(string topic)
    {
        if (topic is "approval-status" or "approval_instance" or "approval_task")
        {
            return CallbackInboxKind.ApprovalStatus;
        }
        if (topic == "contact-change" || topic.StartsWith("contact.", StringComparison.Ordinal))
        {
            return CallbackInboxKind.ContactChange;
        }
        if (topic is "card.action.trigger" or "card.message.action")
        {
            return CallbackInboxKind.MessageInteraction;
        }
        return CallbackInboxKind.Other;
    }

    private IReadOnlyDictionary<string, string> EnrichHeadersWithSecrets(IReadOnlyDictionary<string, string> headers, ExternalIdentityProvider provider, string providerType)
    {
        var clone = headers.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var plain = _secretProtector.Decrypt(provider.SecretEncrypted);
        if (string.IsNullOrEmpty(plain))
        {
            return clone;
        }
        try
        {
            using var doc = JsonDocument.Parse(plain);
            if (string.Equals(providerType, "wecom", StringComparison.OrdinalIgnoreCase))
            {
                if (doc.RootElement.TryGetProperty("callbackToken", out var tk) && tk.ValueKind == JsonValueKind.String)
                {
                    clone["x-wecom-token"] = tk.GetString() ?? string.Empty;
                }
                if (doc.RootElement.TryGetProperty("callbackEncodingAesKey", out var aes) && aes.ValueKind == JsonValueKind.String)
                {
                    clone["x-wecom-encoding-aes-key"] = aes.GetString() ?? string.Empty;
                }
                // 解密后强制校验 corpId 必须等于配置的 corp，否则 WeComCallbackVerifier 抛 WebhookDecryptFailed。
                clone["x-wecom-corpid"] = provider.ProviderTenantId ?? string.Empty;
            }
            else if (string.Equals(providerType, "feishu", StringComparison.OrdinalIgnoreCase))
            {
                if (doc.RootElement.TryGetProperty("eventEncryptKey", out var ek) && ek.ValueKind == JsonValueKind.String)
                {
                    clone["x-feishu-encrypt-key"] = ek.GetString() ?? string.Empty;
                }
                if (doc.RootElement.TryGetProperty("eventVerificationToken", out var vt) && vt.ValueKind == JsonValueKind.String)
                {
                    clone["x-feishu-verification-token"] = vt.GetString() ?? string.Empty;
                }
            }
            else if (string.Equals(providerType, "dingtalk", StringComparison.OrdinalIgnoreCase))
            {
                if (doc.RootElement.TryGetProperty("callbackToken", out var tk) && tk.ValueKind == JsonValueKind.String)
                {
                    clone["x-dingtalk-token"] = tk.GetString() ?? string.Empty;
                }
                if (doc.RootElement.TryGetProperty("callbackAesKey", out var aes) && aes.ValueKind == JsonValueKind.String)
                {
                    clone["x-dingtalk-aes-key"] = aes.GetString() ?? string.Empty;
                }
                // 钉钉 corpId 也按 ProviderTenantId 注入。
                clone["x-dingtalk-corpid"] = provider.ProviderTenantId ?? string.Empty;
            }
        }
        catch (JsonException) { }
        return clone;
    }
}
