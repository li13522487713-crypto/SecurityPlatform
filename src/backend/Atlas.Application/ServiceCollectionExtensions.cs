using Atlas.Application.Workflow;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
        
        // 添加工作流应用层服务（包括验证器）
        services.AddWorkflowApplication();
        
        return services;
    }
}
