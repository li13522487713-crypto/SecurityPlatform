using Atlas.Application.Workflow.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Application.Workflow;

/// <summary>
/// 工作流应用层依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加工作流应用层服务
    /// </summary>
    public static IServiceCollection AddWorkflowApplication(this IServiceCollection services)
    {
        // 注册验证器
        services.AddValidatorsFromAssemblyContaining<StartWorkflowRequestValidator>();

        return services;
    }
}
