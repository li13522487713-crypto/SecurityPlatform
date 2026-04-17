using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Mappings;
using Atlas.Application.LowCode.Repositories;
using Atlas.Application.LowCode.Validators;
using Atlas.Infrastructure.Repositories.LowCode;
using Atlas.Infrastructure.Services.LowCode;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// 低代码（M01）DI 注册集合：仓储 + 服务 + AutoMapper Profile + FluentValidation。
/// 在 ServiceCollectionExtensions.cs 由共享入口调用。
/// </summary>
public static class LowCodeServiceRegistration
{
    public static IServiceCollection AddLowCodeInfrastructure(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IAppDefinitionRepository, AppDefinitionRepository>();
        services.AddScoped<IPageDefinitionRepository, PageDefinitionRepository>();
        services.AddScoped<IAppVariableRepository, AppVariableRepository>();
        services.AddScoped<IAppContentParamRepository, AppContentParamRepository>();
        services.AddScoped<IAppVersionArchiveRepository, AppVersionArchiveRepository>();
        services.AddScoped<IAppPublishArtifactRepository, AppPublishArtifactRepository>();
        services.AddScoped<IAppResourceReferenceRepository, AppResourceReferenceRepository>();

        // Services
        services.AddScoped<IAppDefinitionQueryService, AppDefinitionQueryService>();
        services.AddScoped<IAppDefinitionCommandService, AppDefinitionCommandService>();

        // AutoMapper（Profile 在 Atlas.Application.LowCode 程序集中，按 marker 类型集中注册）
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<LowCodeMappingProfile>();
        });

        // FluentValidation（按程序集扫描 Application.LowCode 即可覆盖所有 *RequestValidator）
        services.AddValidatorsFromAssemblyContaining<AppDefinitionCreateRequestValidator>(
            includeInternalTypes: false);

        return services;
    }
}
