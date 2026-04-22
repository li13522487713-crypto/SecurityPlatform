using System.Globalization;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.Platform.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Controllers.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/app-web/coze-developer")]
[Authorize]
public sealed class AppWebCozeDeveloperGatewayController : ControllerBase
{
    private readonly IWorkspacePortalService _workspacePortalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AppWebCozeDeveloperGatewayController(
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _workspacePortalService = workspacePortalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("space/list")]
    public async Task<ActionResult<object>> SpaceList(
        [FromBody] CozeGetSpaceListRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        var workspaces = await _workspacePortalService.ListWorkspacesAsync(
            tenantId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        var filtered = string.IsNullOrWhiteSpace(request?.search_word)
            ? workspaces
            : workspaces.Where(item =>
                    $"{item.Name} {item.Description ?? string.Empty} {item.AppKey}"
                        .Contains(request.search_word, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var page = request?.page > 0 ? request.page.Value : 1;
        var size = request?.size > 0 ? request.size.Value : filtered.Count;
        var skipped = Math.Max(0, (page - 1) * size);
        var paged = filtered.Skip(skipped).Take(size).ToArray();
        var botSpaces = paged.Select(MapSpaceItem).ToArray();

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            bot_space_list = botSpaces,
            recently_used_space_list = botSpaces,
            has_personal_space = false,
            total = filtered.Count,
            has_more = skipped + botSpaces.Length < filtered.Count
        }));
    }

    [HttpPost("bot/get_type_list")]
    public async Task<ActionResult<object>> GetTypeList(
        [FromBody] CozeGetTypeListRequest? request,
        CancellationToken cancellationToken)
    {
        var queryService = HttpContext.RequestServices.GetService<IModelConfigQueryService>();
        if (queryService is null)
        {
            return Ok(CozeCompatGatewaySupport.Success(CozeCompatGatewaySupport.BuildTypeListPayload(Array.Empty<Atlas.Application.AiPlatform.Models.ModelConfigDto>(), request?.model_scene)));
        }

        var models = await queryService.GetAllEnabledAsync(
            _tenantProvider.GetTenantId(),
            workspaceId: null,
            cancellationToken);

        return Ok(CozeCompatGatewaySupport.Success(
            CozeCompatGatewaySupport.BuildTypeListPayload(models, request?.model_scene)));
    }

    [HttpPost("draftbot/get_draft_bot_list")]
    public async Task<ActionResult<object>> GetDraftBotList(
        [FromBody] CozeGetDraftBotListRequest? request,
        CancellationToken cancellationToken)
    {
        var teamAgentService = HttpContext.RequestServices.GetService<ITeamAgentService>();
        if (teamAgentService is null)
        {
            return Ok(CozeCompatGatewaySupport.Success(new
            {
                total = 0,
                list = Array.Empty<object>()
            }));
        }

        var pageIndex = request?.page > 0 ? request.page!.Value : 1;
        var pageSize = request?.size > 0 ? request.size!.Value : 20;
        var paged = await teamAgentService.GetPagedAsync(
            _tenantProvider.GetTenantId(),
            request?.name,
            null,
            null,
            null,
            null,
            pageIndex,
            pageSize,
            cancellationToken);

        var list = paged.Items.Select(item => new
        {
            id = item.Id.ToString(CultureInfo.InvariantCulture),
            name = item.Name,
            desc = item.Description ?? string.Empty,
            icon = string.Empty,
            space_id = request?.space_id ?? string.Empty,
            create_time = CozeCompatGatewaySupport.ToUnixMilliseconds(item.CreatedAt),
            update_time = CozeCompatGatewaySupport.ToUnixMilliseconds(item.UpdatedAt),
            publish_time = 0,
            publish_status = item.Status.ToString().ToLowerInvariant(),
            agent_type = string.IsNullOrWhiteSpace(item.AgentType) ? "team_agent" : item.AgentType
        }).ToArray();

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            total = paged.Total,
            list,
            has_more = pageIndex * pageSize < paged.Total
        }));
    }

    [HttpPost("draftbot/get_display_info")]
    public async Task<ActionResult<object>> GetDraftBotDisplayInfo(
        [FromBody] CozeGetDraftBotDisplayInfoRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var teamAgentService = HttpContext.RequestServices.GetService<ITeamAgentService>();
        if (teamAgentService is null)
        {
            return Ok(CozeCompatGatewaySupport.Success(new
            {
                bot_id = request?.bot_id ?? string.Empty,
                name = $"team-agent-{botId}",
                description = string.Empty,
                icon_url = string.Empty,
                publish_status = "draft"
            }));
        }

        var detail = await teamAgentService.GetByIdAsync(_tenantProvider.GetTenantId(), botId, cancellationToken);
        if (detail is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            bot_id = detail.Id.ToString(CultureInfo.InvariantCulture),
            name = detail.Name,
            description = detail.Description ?? string.Empty,
            icon_url = string.Empty,
            agent_type = detail.AgentType,
            publish_status = detail.Status.ToString().ToLowerInvariant(),
            create_time = CozeCompatGatewaySupport.ToUnixMilliseconds(detail.CreatedAt),
            update_time = CozeCompatGatewaySupport.ToUnixMilliseconds(detail.UpdatedAt),
            schema_config_json = detail.SchemaConfigJson ?? string.Empty
        }));
    }

    [HttpPost("bot/upload_file")]
    public ActionResult<object> UploadBotFile()
    {
        return Ok(CozeCompatGatewaySupport.Success(new
        {
            file_id = Guid.NewGuid().ToString("N"),
            file_url = string.Empty
        }));
    }

    [HttpPost("{**path}")]
    public ActionResult<object> PostFallback([FromRoute] string? path)
    {
        return Ok(CozeCompatGatewaySupport.Success(CozeCompatGatewaySupport.BuildDeveloperFallbackData(path)));
    }

    [HttpGet("{**path}")]
    public ActionResult<object> GetFallback([FromRoute] string? path)
    {
        return Ok(CozeCompatGatewaySupport.Success(CozeCompatGatewaySupport.BuildDeveloperFallbackData(path)));
    }

    private static bool TryParsePositiveId(string? raw, out long value)
    {
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            value = 0;
            return false;
        }

        return value > 0;
    }

    private static object MapSpaceItem(Atlas.Application.Platform.Models.WorkspaceListItem item)
    {
        var roleType = string.Equals(item.RoleCode, "Owner", StringComparison.OrdinalIgnoreCase)
            ? 1
            : string.Equals(item.RoleCode, "Admin", StringComparison.OrdinalIgnoreCase)
                ? 2
                : 3;

        return new
        {
            id = item.Id,
            name = item.Name,
            description = item.Description,
            icon_url = item.Icon ?? string.Empty,
            role_type = roleType,
            space_type = 1,
            space_mode = 0,
            hide_operation = false
        };
    }
}
