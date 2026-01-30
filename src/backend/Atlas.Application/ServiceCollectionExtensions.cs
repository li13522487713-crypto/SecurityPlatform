using Atlas.Application.Workflow;
using Atlas.Application.Visualization;
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
        // 可视化模块骨架（后续可按需扩展）
        services.AddVisualizationApplication();
        
        return services;
    }
}
