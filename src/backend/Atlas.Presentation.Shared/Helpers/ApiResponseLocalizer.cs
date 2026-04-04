using Atlas.Application.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Atlas.Presentation.Shared.Helpers;

/// <summary>
/// 从当前请求解析 <see cref="IStringLocalizer{Messages}"/>，用于 Controller 中 <see cref="Atlas.Core.Models.ApiResponse{T}"/> 的 message 字段本地化。
/// </summary>
public static class ApiResponseLocalizer
{
    public static string T(HttpContext httpContext, string resourceKey)
    {
        var localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();
        return localizer[resourceKey].Value;
    }

    public static string T(HttpContext httpContext, string resourceKey, params object[] arguments)
    {
        var localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();
        return localizer[resourceKey, arguments].Value;
    }
}
