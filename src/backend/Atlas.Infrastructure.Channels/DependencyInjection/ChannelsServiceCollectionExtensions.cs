using Atlas.Infrastructure.Channels.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Channels.DependencyInjection;

public static class ChannelsServiceCollectionExtensions
{
    /// <summary>绑定渠道相关配置（微信开放平台片段等）。Senparc 具体注册在宿主具备有效 AppId/Secret 后按需扩展。</summary>
    public static IServiceCollection AddAtlasInfrastructureChannels(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<WeixinOpenChannelOptions>()
            .Bind(configuration.GetSection(WeixinOpenChannelOptions.SectionName));

        return services;
    }
}
