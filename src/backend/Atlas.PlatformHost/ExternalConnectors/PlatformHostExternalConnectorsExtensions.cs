using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Mappings;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Validators;
using Atlas.Infrastructure.ExternalConnectors.DependencyInjection;
using Atlas.PlatformHost.ExternalConnectors.Bridges;
using Atlas.Sdk.ConnectorPlugins.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.PlatformHost.ExternalConnectors;

public static class PlatformHostExternalConnectorsExtensions
{
    /// <summary>
    /// 在 PlatformHost.Program.cs 中调用一次：完成 ExternalConnectors 在 Web 主机层的全部装配
    /// （桥接 ISecretProtector / ILocalUserDirectory / IConnectorJwtIssuer，
    /// 注入 Application/Infrastructure 服务，注册 WeCom + Feishu + DingTalk provider，
    /// 注入 FluentValidation 校验器与 AutoMapper Profile）。
    /// </summary>
    public static IServiceCollection AddPlatformExternalConnectors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddExternalConnectorsCore();
        services.AddWeComConnector();
        services.AddFeishuConnector();
        services.AddDingTalkConnector();
        services.AddConnectorPluginNodes();

        services.TryAddScoped<ISecretProtector, ConnectorSecretProtectorBridge>();
        services.TryAddScoped<ILocalUserDirectory, ConnectorLocalUserDirectoryBridge>();
        services.TryAddScoped<IConnectorJwtIssuer, ConnectorJwtIssuerBridge>();
        services.TryAddSingleton<Atlas.Infrastructure.ExternalConnectors.HostedServices.ITenantContextWriter, ConnectorTenantContextWriterBridge>();

        services.AddAutoMapper(typeof(ExternalConnectorsMappingProfile).Assembly);

        services.AddScoped<IValidator<ExternalIdentityProviderCreateRequest>, ExternalIdentityProviderCreateRequestValidator>();
        services.AddScoped<IValidator<ExternalIdentityProviderUpdateRequest>, ExternalIdentityProviderUpdateRequestValidator>();
        services.AddScoped<IValidator<ExternalIdentityProviderRotateSecretRequest>, ExternalIdentityProviderRotateSecretRequestValidator>();
        services.AddScoped<IValidator<ManualBindingRequest>, ManualBindingRequestValidator>();
        services.AddScoped<IValidator<BindingConflictResolutionRequest>, BindingConflictResolutionRequestValidator>();
        services.AddScoped<IValidator<OAuthInitiationRequest>, OAuthInitiationRequestValidator>();
        services.AddScoped<IValidator<OAuthCallbackRequest>, OAuthCallbackRequestValidator>();

        return services;
    }
}
