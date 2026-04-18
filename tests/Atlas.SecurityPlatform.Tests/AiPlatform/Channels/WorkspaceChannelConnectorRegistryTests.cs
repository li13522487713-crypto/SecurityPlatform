using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Core.Exceptions;
using Atlas.Infrastructure.Services.AiPlatform.Channels;

namespace Atlas.SecurityPlatform.Tests.AiPlatform.Channels;

/// <summary>
/// 覆盖 M-G02-C1：渠道注册中心 DI 行为。
/// </summary>
public sealed class WorkspaceChannelConnectorRegistryTests
{
    [Fact]
    public void Resolve_ShouldReturnRegisteredConnector_CaseInsensitive()
    {
        var sdk = new FakeConnector("web-sdk");
        var openApi = new FakeConnector("open-api");
        var registry = new WorkspaceChannelConnectorRegistry(new IWorkspaceChannelConnector[] { sdk, openApi });

        Assert.Same(sdk, registry.Resolve("web-sdk"));
        Assert.Same(sdk, registry.Resolve("WEB-SDK"));
        Assert.Same(openApi, registry.Resolve(" open-api "));
    }

    [Fact]
    public void Resolve_ShouldReturnNull_WhenChannelTypeMissingOrUnknown()
    {
        var registry = new WorkspaceChannelConnectorRegistry(new IWorkspaceChannelConnector[]
        {
            new FakeConnector("web-sdk")
        });

        Assert.Null(registry.Resolve(""));
        Assert.Null(registry.Resolve(null!));
        Assert.Null(registry.Resolve("unknown-channel"));
    }

    [Fact]
    public void RequireResolve_ShouldThrowBusinessException_WhenChannelTypeNotRegistered()
    {
        var registry = new WorkspaceChannelConnectorRegistry(Array.Empty<IWorkspaceChannelConnector>());

        var ex = Assert.Throws<BusinessException>(() => registry.RequireResolve("feishu"));
        Assert.Equal("CHANNEL_TYPE_NOT_SUPPORTED", ex.Code);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDuplicateChannelTypeRegistered()
    {
        var connectors = new IWorkspaceChannelConnector[]
        {
            new FakeConnector("feishu"),
            new FakeConnector("FEISHU")
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new WorkspaceChannelConnectorRegistry(connectors));
        Assert.Contains("Duplicate channel connector", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenChannelTypeIsBlank()
    {
        var connectors = new IWorkspaceChannelConnector[]
        {
            new FakeConnector("   ")
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new WorkspaceChannelConnectorRegistry(connectors));
        Assert.Contains("empty ChannelType", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SupportedChannelTypes_ShouldBeSortedAndDeduplicated()
    {
        var registry = new WorkspaceChannelConnectorRegistry(new IWorkspaceChannelConnector[]
        {
            new FakeConnector("wechat-mp"),
            new FakeConnector("feishu"),
            new FakeConnector("web-sdk"),
            new FakeConnector("open-api")
        });

        Assert.Equal(new[] { "feishu", "open-api", "web-sdk", "wechat-mp" }, registry.SupportedChannelTypes);
    }

    private sealed class FakeConnector : IWorkspaceChannelConnector
    {
        public FakeConnector(string channelType)
        {
            ChannelType = channelType;
        }

        public string ChannelType { get; }

        public Task<ChannelPublishResult> PublishAsync(ChannelPublishContext context, CancellationToken cancellationToken)
            => Task.FromResult(new ChannelPublishResult(true, "ok", null, null));

        public Task<ChannelDispatchResult> HandleInboundAsync(ChannelInboundContext context, CancellationToken cancellationToken)
            => Task.FromResult(new ChannelDispatchResult(true, null, null));

        public Task SendOutboundAsync(ChannelOutboundContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
