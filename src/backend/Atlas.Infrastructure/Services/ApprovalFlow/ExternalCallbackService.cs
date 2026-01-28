using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;

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

    public ExternalCallbackService(
        IApprovalExternalCallbackConfigRepository configRepository,
        IApprovalExternalCallbackRecordRepository recordRepository,
        IApprovalInstanceRepository instanceRepository,
        IApprovalTaskRepository? taskRepository,
        IExternalCallbackHandler callbackHandler,
        IIdGenerator idGenerator,
        TimeProvider? timeProvider = null)
    {
        _configRepository = configRepository;
        _recordRepository = recordRepository;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _callbackHandler = callbackHandler;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
        catch
        {
            // 回调失败不影响主流程，记录日志即可
            // TODO: 记录错误日志
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
        // 构建请求体
        var requestBody = BuildRequestBody(instance, task, nodeId, eventType);

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

            // 生成签名
            var headers = GenerateHeaders(requestBody, config.SecretKey, _timeProvider.GetUtcNow());

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

                var headers = GenerateHeaders(record.RequestBody, config.SecretKey, _timeProvider.GetUtcNow());

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
        CallbackEventType eventType)
    {
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
            dataJson = instance.DataJson
        };

        return JsonSerializer.Serialize(payload);
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
