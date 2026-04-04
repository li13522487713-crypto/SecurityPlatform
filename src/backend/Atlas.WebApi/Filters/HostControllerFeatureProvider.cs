using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Atlas.WebApi.Filters;

/// <summary>
/// 根据宿主类型过滤控制器：
/// PlatformHost 排除 [AppRuntimeOnly]，AppHost 排除 [PlatformOnly]，
/// WebApi（兼容模式）不排除任何控制器。
/// </summary>
public sealed class HostControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly HostType hostType;

    public HostControllerFeatureProvider(HostType hostType)
    {
        this.hostType = hostType;
    }

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (hostType == HostType.WebApi)
        {
            return;
        }

        for (var i = feature.Controllers.Count - 1; i >= 0; i--)
        {
            var controller = feature.Controllers[i];

            if (hostType == HostType.PlatformHost
                && controller.GetCustomAttribute<AppRuntimeOnlyAttribute>() is not null)
            {
                feature.Controllers.RemoveAt(i);
            }
            else if (hostType == HostType.AppHost
                && controller.GetCustomAttribute<PlatformOnlyAttribute>() is not null)
            {
                feature.Controllers.RemoveAt(i);
            }
        }
    }
}

public enum HostType
{
    WebApi,
    PlatformHost,
    AppHost
}
