using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.System.Events;
using Atlas.Core.Abstractions;
using Atlas.Core.Events;
using Atlas.Core.Tenancy;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AdminAiConfigService : IAdminAiConfigService
{
    private const string Prefix = "admin.ai.";
    private static readonly string EnableAiPlatformKey = $"{Prefix}enable-platform";
    private static readonly string EnableOpenPlatformKey = $"{Prefix}enable-open-platform";
    private static readonly string EnableCodeSandboxKey = $"{Prefix}enable-code-sandbox";
    private static readonly string EnableMarketplaceKey = $"{Prefix}enable-marketplace";
    private static readonly string EnableContentModerationKey = $"{Prefix}enable-content-moderation";
    private static readonly string MaxDailyTokensPerUserKey = $"{Prefix}max-daily-tokens-per-user";
    private static readonly string MaxKnowledgeRetrievalCountKey = $"{Prefix}max-knowledge-retrieval-count";
    private static readonly string[] ConfigKeys =
    [
        EnableAiPlatformKey,
        EnableOpenPlatformKey,
        EnableCodeSandboxKey,
        EnableMarketplaceKey,
        EnableContentModerationKey,
        MaxDailyTokensPerUserKey,
        MaxKnowledgeRetrievalCountKey
    ];

    private readonly SystemConfigRepository _systemConfigRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IEventBus _eventBus;

    public AdminAiConfigService(
        SystemConfigRepository systemConfigRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IEventBus eventBus)
    {
        _systemConfigRepository = systemConfigRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _eventBus = eventBus;
    }

    public async Task<AdminAiConfigDto> GetAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var configs = await _systemConfigRepository.GetByKeysAsync(tenantId, ConfigKeys, cancellationToken);
        var map = configs.ToDictionary(x => x.ConfigKey, x => x.ConfigValue, StringComparer.OrdinalIgnoreCase);
        return new AdminAiConfigDto(
            ParseBool(map, EnableAiPlatformKey, true),
            ParseBool(map, EnableOpenPlatformKey, true),
            ParseBool(map, EnableCodeSandboxKey, true),
            ParseBool(map, EnableMarketplaceKey, true),
            ParseBool(map, EnableContentModerationKey, true),
            ParseInt(map, MaxDailyTokensPerUserKey, 500000),
            ParseInt(map, MaxKnowledgeRetrievalCountKey, 8));
    }

    public async Task UpdateAsync(TenantId tenantId, AdminAiConfigUpdateRequest request, CancellationToken cancellationToken)
    {
        var desiredMap = new Dictionary<string, (string Value, string Name, string Remark)>(StringComparer.OrdinalIgnoreCase)
        {
            [EnableAiPlatformKey] = (request.EnableAiPlatform ? "true" : "false", "启用 AI 平台", "总开关"),
            [EnableOpenPlatformKey] = (request.EnableOpenPlatform ? "true" : "false", "启用开放平台", "控制 Open API 开关"),
            [EnableCodeSandboxKey] = (request.EnableCodeSandbox ? "true" : "false", "启用代码沙箱", "控制工作流代码执行开关"),
            [EnableMarketplaceKey] = (request.EnableMarketplace ? "true" : "false", "启用 AI 市场", "控制 AI 市场模块"),
            [EnableContentModerationKey] = (request.EnableContentModeration ? "true" : "false", "启用内容审核", "控制 AI 内容安全审核"),
            [MaxDailyTokensPerUserKey] = (request.MaxDailyTokensPerUser.ToString(), "单用户每日 Token 上限", "超限后拒绝调用"),
            [MaxKnowledgeRetrievalCountKey] = (request.MaxKnowledgeRetrievalCount.ToString(), "知识检索最大召回数", "RAG 检索上限")
        };

        var existing = await _systemConfigRepository.GetByKeysAsync(tenantId, ConfigKeys, cancellationToken);
        var existingMap = existing.ToDictionary(x => x.ConfigKey, x => x, StringComparer.OrdinalIgnoreCase);
        var inserts = new List<SystemConfig>();
        var updates = new List<SystemConfig>();
        var changedEvents = new List<SystemConfigChangedEvent>();

        foreach (var key in ConfigKeys)
        {
            var desired = desiredMap[key];
            if (existingMap.TryGetValue(key, out var config))
            {
                var oldValue = config.ConfigValue;
                config.Update(desired.Value, desired.Name, desired.Remark);
                updates.Add(config);
                if (!string.Equals(oldValue, config.ConfigValue, StringComparison.Ordinal))
                {
                    changedEvents.Add(new SystemConfigChangedEvent(tenantId, config.ConfigKey, config.AppId, oldValue, config.ConfigValue));
                }
            }
            else
            {
                var entity = new SystemConfig(
                    tenantId,
                    key,
                    desired.Value,
                    desired.Name,
                    false,
                    _idGeneratorAccessor.NextId());
                entity.Update(desired.Value, desired.Name, desired.Remark);
                inserts.Add(entity);
                changedEvents.Add(new SystemConfigChangedEvent(tenantId, entity.ConfigKey, entity.AppId, null, entity.ConfigValue));
            }
        }

        await _systemConfigRepository.AddRangeAsync(inserts, cancellationToken);
        await _systemConfigRepository.UpdateRangeAsync(updates, cancellationToken);

        foreach (var changedEvent in changedEvents)
        {
            await _eventBus.PublishAsync(changedEvent, cancellationToken);
        }
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> map, string key, bool defaultValue)
    {
        if (!map.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> map, string key, int defaultValue)
    {
        if (!map.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
