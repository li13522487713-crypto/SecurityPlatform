using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Core.Exceptions;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels;

/// <summary>
/// 默认渠道注册中心实现：在 DI 启动期收集所有 IWorkspaceChannelConnector，
/// 按 ChannelType 字符串建立大小写不敏感映射；同一 ChannelType 重复注册会抛 InvalidOperationException，
/// 防止两个 connector 同时声明同一渠道导致运行期不可预期。
/// </summary>
public sealed class WorkspaceChannelConnectorRegistry : IWorkspaceChannelConnectorRegistry
{
    private readonly ImmutableDictionary<string, IWorkspaceChannelConnector> _byType;

    public WorkspaceChannelConnectorRegistry(IEnumerable<IWorkspaceChannelConnector> connectors)
    {
        ArgumentNullException.ThrowIfNull(connectors);

        var builder = ImmutableDictionary.CreateBuilder<string, IWorkspaceChannelConnector>(StringComparer.OrdinalIgnoreCase);
        foreach (var connector in connectors)
        {
            if (connector is null)
            {
                continue;
            }
            var key = (connector.ChannelType ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Channel connector '{connector.GetType().FullName}' returned an empty ChannelType.");
            }
            if (builder.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"Duplicate channel connector registered for type '{key}': '{builder[key].GetType().FullName}' and '{connector.GetType().FullName}'.");
            }
            builder.Add(key, connector);
        }
        _byType = builder.ToImmutable();

        SupportedChannelTypes = _byType.Keys
            .OrderBy(static k => k, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();
    }

    public IReadOnlyList<string> SupportedChannelTypes { get; }

    public IWorkspaceChannelConnector? Resolve(string channelType)
    {
        if (string.IsNullOrWhiteSpace(channelType))
        {
            return null;
        }
        return _byType.TryGetValue(channelType.Trim(), out var connector) ? connector : null;
    }

    public IWorkspaceChannelConnector RequireResolve(string channelType)
    {
        var connector = Resolve(channelType);
        if (connector is null)
        {
            throw new BusinessException(
                "CHANNEL_TYPE_NOT_SUPPORTED",
                $"未注册的发布渠道类型：{channelType}");
        }
        return connector;
    }
}
