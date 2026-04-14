using System.Collections.Concurrent;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using IdGen;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.IdGen;

public sealed class SnowflakeIdGeneratorProvider : IIdGeneratorProvider
{
    private readonly IdGeneratorMappingOptions _options;
    private readonly ConcurrentDictionary<(Guid TenantId, string AppId), IIdGenerator> _generators;
    private readonly Dictionary<(Guid TenantId, string AppId), int> _mapping;
    private readonly IdGeneratorOptions _generatorOptions;

    public SnowflakeIdGeneratorProvider(IOptions<IdGeneratorMappingOptions> options)
    {
        _options = options.Value;
        _generators = new ConcurrentDictionary<(Guid, string), IIdGenerator>();
        _mapping = new Dictionary<(Guid, string), int>();
        _generatorOptions = new IdGeneratorOptions(new IdStructure(41, 10, 12));
        ValidateAndLoad();
    }

    public long NextId(TenantId tenantId, string appId)
    {
        if (tenantId.IsEmpty)
        {
            throw new BusinessException("SnowflakeTenantIdRequired", ErrorCodes.ValidationError);
        }

        var resolvedAppId = ResolveAppId(appId);
        var key = (tenantId.Value, resolvedAppId);
        if (!_mapping.TryGetValue(key, out var generatorId))
        {
            var defaultKey = (tenantId.Value, _options.DefaultAppId.Trim());
            if (!string.Equals(resolvedAppId, defaultKey.Item2, StringComparison.Ordinal)
                && _mapping.TryGetValue(defaultKey, out var defaultGeneratorId))
            {
                generatorId = defaultGeneratorId;
                key = defaultKey;
            }
            else if (_options.FallbackGeneratorId is not null)
            {
                generatorId = _options.FallbackGeneratorId.Value;
            }
            else
            {
                throw new BusinessException(
                    $"Snowflake generator is not configured for tenant {tenantId.Value:D} and app {resolvedAppId}.",
                    ErrorCodes.ValidationError);
            }
        }

        var generator = _generators.GetOrAdd(key, _ => new SnowflakeIdGenerator(generatorId, _generatorOptions));
        return generator.NextId();
    }

    private void ValidateAndLoad()
    {
        if (_options.Mappings.Count == 0)
        {
            throw new InvalidOperationException("IdGenerator.Mappings 未配置，无法生成ID。");
        }

        foreach (var mapping in _options.Mappings)
        {
            if (!Guid.TryParse(mapping.TenantId, out var tenantGuid))
            {
                throw new InvalidOperationException($"IdGenerator.Mappings 中 TenantId 格式错误: {mapping.TenantId}");
            }

            if (string.IsNullOrWhiteSpace(mapping.AppId))
            {
                throw new InvalidOperationException($"IdGenerator.Mappings 中 AppId 不能为空，TenantId={mapping.TenantId}");
            }

            if (mapping.GeneratorId is < 0 or > 1023)
            {
                throw new InvalidOperationException($"IdGenerator.Mappings 中 GeneratorId 超出范围(0-1023): {mapping.GeneratorId}");
            }

            var normalizedAppId = mapping.AppId.Trim();
            var key = (tenantGuid, normalizedAppId);
            if (_mapping.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"IdGenerator.Mappings 存在重复项: TenantId={mapping.TenantId}, AppId={normalizedAppId}");
            }

            _mapping[key] = mapping.GeneratorId;
            _generators.TryAdd(key, new SnowflakeIdGenerator(mapping.GeneratorId, _generatorOptions));
        }
    }

    private string ResolveAppId(string appId)
    {
        var resolved = string.IsNullOrWhiteSpace(appId) ? _options.DefaultAppId : appId.Trim();
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new BusinessException("SnowflakeAppIdRequired", ErrorCodes.ValidationError);
        }

        return resolved;
    }
}
