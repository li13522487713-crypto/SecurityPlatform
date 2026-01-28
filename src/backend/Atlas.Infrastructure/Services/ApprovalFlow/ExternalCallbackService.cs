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
    private readonly IIdGenerator _idGenerator;
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
        IIdGenerator idGenerator,
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
        _idGenerator = idGenerator;
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

            // 为每个配置创建回调记录并发送
            foreach (var config in configs)
            {
                await SendCallbackAsync(tenantId, config, instance, task, nodeId, eventType, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // 回调失败不影响主流程，记录日志
            _logger?.LogError(ex, "触发回调失败：租户={TenantId}, 实例={InstanceId}, 事件={EventType}", tenantId, instance.Id, eventType);
        }
    }

    /// <summary>
    /// 发送回调
    /// </summary>
    private async Task SendCallbackAsync(
        TenantId tenantId,
        ApprovalExternalCallbackConfig config,
        ApprovalProcessInstance instance,
        ApprovalTask? task,
        string? nodeId,
        CallbackEventType eventType,
        CancellationToken cancellationToken)
    {
        // URL白名单校验
        if (!ValidateCallbackUrl(config.CallbackUrl))
        {
            _logger?.LogWarning("回调URL不在白名单中：{CallbackUrl}", config.CallbackUrl);
            throw new InvalidOperationException($"回调URL不在白名单中：{config.CallbackUrl}");
        }

        // 构建请求体（脱敏处理）
        var requestBody = BuildRequestBody(instance, task, nodeId, eventType, sanitize: true);

        // 生成幂等键
        var idempotencyKey = GenerateIdempotencyKey(instance.Id, task?.Id, nodeId, eventType, config.Id);

        // 检查是否已存在（幂等性保护）
        var existingRecord = await _recordRepository.GetByIdempotencyKeyAsync(tenantId, idempotencyKey, cancellationToken);
        if (existingRecord != null && existingRecord.Status == CallbackStatus.Success)
        {
            return; // 已成功回调，跳过
        }

        // 创建或更新回调记录
        ApprovalExternalCallbackRecord record;
        if (existingRecord != null)
        {
            record = existingRecord;
        }
        else
        {
            record = new ApprovalExternalCallbackRecord(
                tenantId,
                config.Id,
                instance.Id,
                task?.Id,
                nodeId,
                eventType,
                config.CallbackUrl,
                requestBody,
                idempotencyKey,
                _idGenerator.NextId());
            await _recordRepository.AddAsync(record, cancellationToken);
        }

        // 发送回调
        try
        {
            record.MarkSending(_timeProvider.GetUtcNow());
            await _recordRepository.UpdateAsync(record, cancellationToken);

            // 解密SecretKey（如果已加密）
            var secretKey = DecryptSecretKey(config.SecretKey);

            // 生成签名
            var headers = GenerateHeaders(requestBody, secretKey, _timeProvider.GetUtcNow());

            // 发送 HTTP 回调
            var responseBody = await _callbackHandler.SendCallbackAsync(
                config.CallbackUrl,
                requestBody,
                headers,
                config.TimeoutSeconds,
                cancellationToken);

            // 标记成功
            record.MarkSuccess(responseBody, _timeProvider.GetUtcNow());
            await _recordRepository.UpdateAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            // 标记失败并设置重试
            record.MarkFailed(ex.Message, config.RetryIntervalSeconds, _timeProvider.GetUtcNow());
            await _recordRepository.UpdateAsync(record, cancellationToken);
            throw; // 重新抛出异常，由调用方决定是否重试
        }
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

        foreach (var record in pendingRecords)
        {
            ApprovalExternalCallbackConfig? config = null;
            try
            {
                config = await _configRepository.GetByIdAsync(tenantId, record.ConfigId, cancellationToken);
                if (config == null || !config.IsEnabled)
                {
                    record.MarkCancelled();
                    await _recordRepository.UpdateAsync(record, cancellationToken);
                    continue;
                }

                // 检查是否超过最大重试次数
                if (!record.CanRetry(config.MaxRetryCount))
                {
                    record.MarkCancelled();
                    await _recordRepository.UpdateAsync(record, cancellationToken);
                    continue;
                }

                // 重新发送
                record.MarkSending(_timeProvider.GetUtcNow());
                await _recordRepository.UpdateAsync(record, cancellationToken);

                // 解密SecretKey（如果已加密）
                var secretKey = DecryptSecretKey(config.SecretKey);

                var headers = GenerateHeaders(record.RequestBody, secretKey, _timeProvider.GetUtcNow());

                var responseBody = await _callbackHandler.SendCallbackAsync(
                    record.CallbackUrl,
                    record.RequestBody,
                    headers,
                    config.TimeoutSeconds,
                    cancellationToken);

                record.MarkSuccess(responseBody, _timeProvider.GetUtcNow());
                await _recordRepository.UpdateAsync(record, cancellationToken);
            }
            catch (Exception ex)
            {
                var retryIntervalSeconds = config?.RetryIntervalSeconds ?? 300;
                record.MarkFailed(ex.Message, retryIntervalSeconds, _timeProvider.GetUtcNow());
                await _recordRepository.UpdateAsync(record, cancellationToken);
            }
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
