using System.Globalization;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Authorize]
public sealed class CozePluginApiCompatController : ControllerBase
{
    private readonly IAiWorkspaceService _workspaceService;
    private readonly IAiPluginService _pluginService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public CozePluginApiCompatController(
        IAiWorkspaceService workspaceService,
        IAiPluginService pluginService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _workspaceService = workspaceService;
        _pluginService = pluginService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("/api/plugin_api/library_resource_list")]
    public async Task<ActionResult<object>> LibraryResourceList(
        [FromBody] CozeLibraryResourceListRequest? request,
        CancellationToken cancellationToken)
    {
        var library = await _workspaceService.GetLibraryAsync(
            _tenantProvider.GetTenantId(),
            new AiLibraryQueryRequest(
                request?.name,
                MapLibraryResourceType(request?.res_type_filter),
                1,
                request?.size is > 0 ? Math.Min(request.size.Value, 100) : 15),
            cancellationToken);

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var resourceList = library.Items
            .Select(item => MapLibraryResourceInfo(item, currentUser, request?.space_id))
            .Where(item => item is not null)
            .ToArray();

        return Ok(new
        {
            code = 0,
            msg = "success",
            resource_list = resourceList,
            cursor = string.Empty,
            has_more = false,
            BaseResp = new { }
        });
    }

    [HttpPost("/api/plugin_api/del_plugin")]
    public async Task<ActionResult<object>> DeletePlugin(
        [FromBody] CozeDeletePluginRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.plugin_id, out var pluginId))
        {
            return Ok(new
            {
                code = 400,
                msg = "plugin_id is invalid",
                BaseResp = new { }
            });
        }

        await _pluginService.DeleteAsync(_tenantProvider.GetTenantId(), pluginId, cancellationToken);
        return Ok(new
        {
            code = 0,
            msg = "success",
            BaseResp = new { }
        });
    }

    private static string? MapLibraryResourceType(IReadOnlyList<int>? typeFilter)
    {
        var value = typeFilter?.FirstOrDefault();
        return value switch
        {
            1 => "plugin",
            2 => "workflow",
            4 => "knowledge-base",
            6 => "prompt",
            7 => "database",
            _ => null
        };
    }

    private static object? MapLibraryResourceInfo(
        AiLibraryItem item,
        CurrentUserInfo currentUser,
        string? spaceId)
    {
        var resType = item.ResourceType.ToLowerInvariant() switch
        {
            "plugin" => 1,
            "workflow" => 2,
            "knowledge-base" => 4,
            "prompt" => 6,
            "database" => 7,
            _ => 0
        };

        if (resType == 0)
        {
            return null;
        }

        var isChatflow = item.ResourceType.Equals("workflow", StringComparison.OrdinalIgnoreCase)
            && item.Path.Contains("/chat_flow/", StringComparison.OrdinalIgnoreCase);

        return new
        {
            res_id = item.ResourceId.ToString(CultureInfo.InvariantCulture),
            res_type = resType,
            res_sub_type = isChatflow ? 1 : 0,
            name = item.Name,
            desc = item.Description ?? string.Empty,
            icon = string.Empty,
            creator_id = currentUser.UserId.ToString(CultureInfo.InvariantCulture),
            creator_avatar = string.Empty,
            creator_name = string.IsNullOrWhiteSpace(currentUser.DisplayName) ? currentUser.Username : currentUser.DisplayName,
            user_name = currentUser.Username,
            publish_status = 1,
            biz_res_status = 0,
            collaboration_enable = false,
            edit_time = new DateTimeOffset(item.UpdatedAt).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            space_id = spaceId ?? string.Empty,
            biz_extend = new Dictionary<string, string>(),
            actions = BuildLibraryActions(item.ResourceType),
            detail_disable = false,
            del_flag = false,
            res_third_type = 0
        };
    }

    private static object[] BuildLibraryActions(string resourceType)
    {
        return resourceType.ToLowerInvariant() switch
        {
            "plugin" =>
            [
                new { key = 2, enable = true },
                new { key = 4, enable = true }
            ],
            "prompt" =>
            [
                new { key = 2, enable = true },
                new { key = 4, enable = true }
            ],
            "knowledge-base" =>
            [
                new { key = 2, enable = true },
                new { key = 3, enable = true }
            ],
            _ => Array.Empty<object>()
        };
    }

    private static bool TryParsePositiveId(string? raw, out long value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(raw)
               && long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               && value > 0;
    }
}

public sealed record CozeLibraryResourceListRequest(
    int? user_filter,
    IReadOnlyList<int>? res_type_filter,
    string? name,
    int? publish_status_filter,
    string? space_id,
    int? size,
    string? cursor,
    IReadOnlyList<string>? search_keys,
    bool? is_get_imageflow);

public sealed record CozeDeletePluginRequest(string? plugin_id);
