using Atlas.Application.Visualization.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Application.Visualization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVisualizationApplication(this IServiceCollection services)
    {
        // 当前为骨架实现，无需额外注册。
        return services;
    }
}
