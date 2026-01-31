using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 外部回调服务实现
/// </summary>
public sealed class ExternalCallbackService
{
    private readonly IApprovalExternalCallbackConfigRepository _configRepository;
    private readonly IApprovalExternalCallbackRecordRepository _recordRepository;
    private readonly IApprovalInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository? _taskRepository;
    private readonly IExternalCallbackHandler _callbackHandler;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly CallbackSecurityOptions _securityOptions;
    private readonly DataProtectionService? _dataProtectionService;
    private readonly ILogger<ExternalCallbackService>? _logger;

    public ExternalCallbackService(
        IApprovalExternalCallbackConfigRepository configRepository,
        IApprovalExternalCallbackRecordRepository recordRepository,
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository? taskRepository,
        IExternalCallbackHandler callbackHandler,
        IIdGeneratorAccessor idGeneratorAccessor,
        IOptions<CallbackSecurityOptions>? securityOptions = null,
        DataProtectionService? dataProtectionService = null,
        ILogger<ExternalCallbackService>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _configRepository = configRepository;
        _recordRepository = recordRepository;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _callbackHandler = callbackHandler;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _securityOptions = securityOptions?.Value ?? new CallbackSecurityOptions();
        _dataProtectionService = dataProtectionService;
        _logger = logger;
    }

    /// <summary>
    /// 触发回调（异步，失败不影响主流程）
    /// </summary>
    public async Task TriggerCallbackAsync(
        TenantId tenantId,
        CallbackEventType eventType,
        ApprovalProcessInstance instance,
        ApprovalTask? task,
        string? nodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            // 获取回调配置（先查找流程级配置，再查找系统级配置）
            var configs = await _configRepository.GetByFlowAndEventAsync(
                tenantId, instance.DefinitionId, eventType, cancellationToken);

            if (configs.Count == 0)
            {
                configs = await _configRepository.GetSystemConfigsAsync(tenantId, eventType, cancellationToken);
            }

            if (configs.Count == 0)
            {
                return; // 没有配置回调，直接返回
            }

            var now = _timeProvider.GetUtcNow();
            var workItems = BuildWorkItems(tenantId, configs, instance, task, nodeId, eventType);
            if (workItems.Count == 0)
            {
                return;
            }

            var existingRecords = await _recordRepository.QueryByIdempotencyKeysAsync(
                tenantId,
                workItems.Select(x => x.IdempotencyKey).ToArray(),
                cancellationToken);
            var recordMap = existingRecords.ToDictionary(x => x.IdempotencyKey);

            var newRecords = new List<ApprovalExternalCallbackRecord>();
            var recordsToMarkSending = new List<ApprovalExternalCallbackRecord>();
            var sendItems = new List<CallbackWorkItem>();

            foreach (var item in workItems)
            {
                if (recordMap.TryGetValue(item.IdempotencyKey, out var existingRecord))
                {
                    if (existingRecord.Status == CallbackStatus.Success)
                    {
                        continue;
                    }

                    existingRecord.MarkSending(now);
                    recordsToMarkSending.Add(existingRecord);
                    sendItems.Add(new CallbackWorkItem(item.Config, existingRecord, item.SecretKey));
                    continue;
                }

                var record = new ApprovalExternalCallbackRecord(
                    tenantId,
                    item.Config.Id,
                    instance.Id,
                    task?.Id,
                    nodeId,
                    eventType,
                    item.Config.CallbackUrl,
                    item.RequestBody,
                    item.IdempotencyKey,
                    _idGeneratorAccessor.NextId());
                record.MarkSending(now);
                newRecords.Add(record);
                sendItems.Add(new CallbackWorkItem(item.Config, record, item.SecretKey));
            }

            if (newRecords.Count > 0)
            {
                await _recordRepository.AddRangeAsync(newRecords, cancellationToken);
            }

            if (recordsToMarkSending.Count > 0)
            {
                await _recordRepository.UpdateRangeAsync(recordsToMarkSending, cancellationToken);
            }

            var updatedRecords = new List<ApprovalExternalCallbackRecord>();
            foreach (var item in sendItems)
            {
                var record = item.Record;
                if (record == null)
                {
                    _logger?.LogWarning("回调记录为空，跳过发送：租户={TenantId}, 实例={InstanceId}, 事件={EventType}", tenantId, instance.Id, eventType);
                    continue;
                }

                try
                {
                    var headers = GenerateHeaders(record.RequestBody, item.SecretKey, now);
                    var responseBody = await _callbackHandler.SendCallbackAsync(
                        item.Config.CallbackUrl,
                        record.RequestBody,
                        headers,
                        item.Config.TimeoutSeconds,
                        cancellationToken);

                    record.MarkSuccess(responseBody, now);
                }
                catch (Exception ex)
                {
                    record.MarkFailed(ex.Message, item.Config.RetryIntervalSeconds, now);
                }

                updatedRecords.Add(record);
            }

            if (updatedRecords.Count > 0)
            {
                await _recordRepository.UpdateRangeAsync(updatedRecords, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // 回调失败不影响主流程，记录日志
            _logger?.LogError(ex, "触发回调失败：租户={TenantId}, 实例={InstanceId}, 事件={EventType}", tenantId, instance.Id, eventType);
        }
    }

    private List<CallbackWorkItem> BuildWorkItems(
        TenantId tenantId,
        IReadOnlyList<ApprovalExternalCallbackConfig> configs,
        ApprovalProcessInstance instance,
        ApprovalTask? task,
        string? nodeId,
        CallbackEventType eventType)
    {
        var workItems = new List<CallbackWorkItem>();
        foreach (var config in configs)
        {
            if (!ValidateCallbackUrl(config.CallbackUrl))
            {
                _logger?.LogWarning("回调URL不在白名单中：{CallbackUrl}", config.CallbackUrl);
                continue;
            }

            var requestBody = BuildRequestBody(instance, task, nodeId, eventType, sanitize: true);
            var idempotencyKey = GenerateIdempotencyKey(instance.Id, task?.Id, nodeId, eventType, config.Id);
            var secretKey = DecryptSecretKey(config.SecretKey);
            workItems.Add(new CallbackWorkItem(config, requestBody, idempotencyKey, secretKey));
        }

        return workItems;
    }

    /// <summary>
    /// 重试失败的回调
    /// </summary>
    public async Task RetryFailedCallbacksAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var currentTime = _timeProvider.GetUtcNow();
        var pendingRecords = await _recordRepository.GetPendingRetriesAsync(tenantId, currentTime, cancellationToken);
        if (pendingRecords.Count == 0)
        {
            return;
        }

        var configIds = pendingRecords.Select(x => x.ConfigId).Distinct().ToArray();
        var configs = await _configRepository.QueryByIdsAsync(tenantId, configIds, cancellationToken);
        var configMap = configs.ToDictionary(x => x.Id);

        var recordsToSend = new List<ApprovalExternalCallbackRecord>();
        var recordsToUpdate = new List<ApprovalExternalCallbackRecord>();

        foreach (var record in pendingRecords)
        {
            if (!configMap.TryGetValue(record.ConfigId, out var config) || !config.IsEnabled)
            {
                record.MarkCancelled();
                recordsToUpdate.Add(record);
                continue;
            }

            if (!record.CanRetry(config.MaxRetryCount))
            {
                record.MarkCancelled();
                recordsToUpdate.Add(record);
                continue;
            }

            record.MarkSending(currentTime);
            recordsToSend.Add(record);
        }

        if (recordsToSend.Count > 0)
        {
            await _recordRepository.UpdateRangeAsync(recordsToSend, cancellationToken);
        }

        foreach (var record in recordsToSend)
        {
            try
            {
                var config = configMap[record.ConfigId];
                var secretKey = DecryptSecretKey(config.SecretKey);
                var headers = GenerateHeaders(record.RequestBody, secretKey, currentTime);

                var responseBody = await _callbackHandler.SendCallbackAsync(
                    record.CallbackUrl,
                    record.RequestBody,
                    headers,
                    config.TimeoutSeconds,
                    cancellationToken);

                record.MarkSuccess(responseBody, currentTime);
            }
            catch (Exception ex)
            {
                var retryIntervalSeconds = configMap.TryGetValue(record.ConfigId, out var config)
                    ? config.RetryIntervalSeconds
                    : 300;
                record.MarkFailed(ex.Message, retryIntervalSeconds, currentTime);
            }

            recordsToUpdate.Add(record);
        }

        if (recordsToUpdate.Count > 0)
        {
            await _recordRepository.UpdateRangeAsync(recordsToUpdate, cancellationToken);
        }
    }

    private sealed record CallbackWorkItem(
        ApprovalExternalCallbackConfig Config,
        string RequestBody,
        string IdempotencyKey,
        string SecretKey)
    {
        public ApprovalExternalCallbackRecord? Record { get; private set; }

        public CallbackWorkItem(ApprovalExternalCallbackConfig config, ApprovalExternalCallbackRecord record, string secretKey)
            : this(config, record.RequestBody, record.IdempotencyKey, secretKey)
        {
            Record = record;
        }
    }

    /// <summary>
    /// 构建请求体
    /// </summary>
    private static string BuildRequestBody(
        ApprovalProcessInstance instance,
        ApprovalTask? task,
        string? nodeId,
        CallbackEventType eventType,
        bool sanitize = false)
    {
        // 如果启用脱敏，则不包含完整的DataJson（可能包含敏感信息）
        // 只包含必要的业务标识
        object? dataJsonValue = sanitize ? null : instance.DataJson;

        var payload = new
        {
            eventType = eventType.ToString(),
            instanceId = instance.Id,
            definitionId = instance.DefinitionId,
            businessKey = instance.BusinessKey,
            status = instance.Status.ToString(),
            taskId = task?.Id,
            nodeId = nodeId,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            dataJson = dataJsonValue
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// 验证回调URL是否在白名单中
    /// </summary>
    private bool ValidateCallbackUrl(string callbackUrl)
    {
        if (_securityOptions.AllowedCallbackDomains.Count == 0)
        {
            // 未配置白名单，允许所有URL（生产环境应配置白名单）
            return true;
        }

        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return _securityOptions.AllowedCallbackDomains.Any(domain =>
        {
            if (string.Equals(host, domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 支持通配符域名（如 *.example.com）
            if (domain.StartsWith("*."))
            {
                var domainSuffix = domain.Substring(2);
                return host.EndsWith("." + domainSuffix, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        });
    }

    /// <summary>
    /// 解密SecretKey（如果已加密）
    /// </summary>
    private string DecryptSecretKey(string secretKey)
    {
        if (string.IsNullOrEmpty(secretKey))
        {
            return secretKey;
        }

        // 如果配置了数据保护服务，尝试解密
        if (_dataProtectionService != null && !string.IsNullOrEmpty(_securityOptions.DataProtectionKey))
        {
            try
            {
                return _dataProtectionService.Decrypt(secretKey);
            }
            catch
            {
                // 解密失败，可能是未加密的旧数据，返回原值
                return secretKey;
            }
        }

        return secretKey;
    }

    /// <summary>
    /// 生成幂等键
    /// </summary>
    private static string GenerateIdempotencyKey(
        long instanceId,
        long? taskId,
        string? nodeId,
        CallbackEventType eventType,
        long configId)
    {
        var keyParts = new List<string>
        {
            instanceId.ToString(),
            taskId?.ToString() ?? "0",
            nodeId ?? "",
            eventType.ToString(),
            configId.ToString()
        };

        var keyString = string.Join("|", keyParts);
        var bytes = Encoding.UTF8.GetBytes(keyString);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 生成请求头（包含签名和时间戳）
    /// </summary>
    private static Dictionary<string, string> GenerateHeaders(
        string requestBody,
        string secretKey,
        DateTimeOffset timestamp)
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Callback-Timestamp"] = timestamp.ToUnixTimeSeconds().ToString(),
            ["X-Callback-Version"] = "1.0"
        };

        // 生成签名：HMAC-SHA256(requestBody + timestamp, secretKey)
        var signString = $"{requestBody}{timestamp.ToUnixTimeSeconds()}";
        var signBytes = Encoding.UTF8.GetBytes(signString);
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(signBytes);
        var signature = Convert.ToHexString(hashBytes);

        headers["X-Callback-Signature"] = signature;

        return headers;
    }
}




