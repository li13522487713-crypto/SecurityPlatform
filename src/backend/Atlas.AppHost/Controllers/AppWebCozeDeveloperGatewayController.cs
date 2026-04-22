using System.Globalization;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
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
    [HttpPost("/api/space/list")]
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
    [HttpPost("/api/bot/get_type_list")]
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
    [HttpPost("/api/draftbot/get_draft_bot_list")]
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
    [HttpPost("/api/draftbot/get_display_info")]
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

    [HttpPost("draftbot/create")]
    [HttpPost("/api/draftbot/create")]
    public async Task<ActionResult<object>> CreateDraftBot(
        [FromBody] CozeDraftBotCreateCompatRequest? request,
        CancellationToken cancellationToken)
    {
        var agentCommandService = HttpContext.RequestServices.GetService<IAgentCommandService>();
        if (agentCommandService is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("agent service unavailable"));
        }

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var workspaceId = TryParsePositiveId(request?.space_id, out var parsedWorkspaceId)
            ? parsedWorkspaceId
            : (long?)null;

        var createRequest = new AgentCreateRequest(
            string.IsNullOrWhiteSpace(request?.name) ? "未命名智能体" : request!.name!,
            request?.description,
            request?.icon_uri,
            SystemPrompt: request?.work_info?.prompt,
            PersonaMarkdown: null,
            Goals: null,
            ReplyLogic: null,
            OutputFormat: null,
            Constraints: null,
            OpeningMessage: null,
            PresetQuestions: Array.Empty<string>(),
            KnowledgeBindings: Array.Empty<AgentKnowledgeBindingInput>(),
            DatabaseBindings: Array.Empty<AgentDatabaseBindingInput>(),
            VariableBindings: Array.Empty<AgentVariableBindingInput>(),
            KnowledgeBaseIds: Array.Empty<long>(),
            DatabaseBindingIds: Array.Empty<long>(),
            VariableBindingIds: Array.Empty<long>(),
            ModelConfigId: null,
            ModelName: null,
            Temperature: null,
            MaxTokens: null,
            DefaultWorkflowId: null,
            DefaultWorkflowName: null,
            EnableMemory: false,
            EnableShortTermMemory: false,
            EnableLongTermMemory: false,
            LongTermMemoryTopK: null,
            WorkspaceId: workspaceId);

        var id = await agentCommandService.CreateAsync(
            _tenantProvider.GetTenantId(),
            currentUser.UserId,
            createRequest,
            cancellationToken);

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            bot_id = id.ToString(CultureInfo.InvariantCulture),
            check_not_pass = false,
            check_not_pass_msg = string.Empty
        }));
    }

    [HttpPost("draftbot/get_bot_info")]
    [HttpPost("/api/draftbot/get_bot_info")]
    public async Task<ActionResult<object>> GetDraftBotInfo(
        [FromBody] CozeGetDraftBotInfoCompatRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var queryService = HttpContext.RequestServices.GetService<IAgentQueryService>();
        if (queryService is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("agent query service unavailable"));
        }

        var detail = await queryService.GetByIdAsync(_tenantProvider.GetTenantId(), botId, cancellationToken);
        if (detail is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        return Ok(CozeCompatGatewaySupport.Success(BuildDraftBotInfoPayload(detail, request?.space_id)));
    }

    [HttpPost("draftbot/update")]
    [HttpPost("/api/draftbot/update")]
    public async Task<ActionResult<object>> UpdateDraftBot(
        [FromBody] CozeUpdateDraftBotCompatRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var queryService = HttpContext.RequestServices.GetService<IAgentQueryService>();
        var commandService = HttpContext.RequestServices.GetService<IAgentCommandService>();
        if (queryService is null || commandService is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("agent service unavailable"));
        }

        var existing = await queryService.GetByIdAsync(_tenantProvider.GetTenantId(), botId, cancellationToken);
        if (existing is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        var workspaceId = TryParsePositiveId(request?.space_id, out var parsedWorkspaceId)
            ? parsedWorkspaceId
            : existing.WorkspaceId;

        var updateRequest = new AgentUpdateRequest(
            string.IsNullOrWhiteSpace(request?.name) ? existing.Name : request!.name!,
            request?.description ?? existing.Description,
            request?.icon_uri ?? existing.AvatarUrl,
            request?.work_info?.prompt ?? existing.SystemPrompt,
            existing.PersonaMarkdown,
            existing.Goals,
            existing.ReplyLogic,
            existing.OutputFormat,
            existing.Constraints,
            existing.OpeningMessage,
            existing.PresetQuestions ?? Array.Empty<string>(),
            existing.KnowledgeBindings?.Select(binding => new AgentKnowledgeBindingInput(
                binding.KnowledgeBaseId,
                binding.IsEnabled,
                binding.InvokeMode,
                binding.TopK,
                binding.ScoreThreshold,
                binding.EnabledContentTypes,
                binding.RewriteQueryTemplate)).ToArray() ?? Array.Empty<AgentKnowledgeBindingInput>(),
            existing.DatabaseBindings?.Select(binding => new AgentDatabaseBindingInput(
                binding.DatabaseId,
                binding.Alias,
                binding.AccessMode,
                binding.TableAllowlist,
                binding.IsDefault)).ToArray() ?? Array.Empty<AgentDatabaseBindingInput>(),
            existing.VariableBindings?.Select(binding => new AgentVariableBindingInput(
                binding.VariableId,
                binding.Alias,
                binding.IsRequired,
                binding.DefaultValueOverride)).ToArray() ?? Array.Empty<AgentVariableBindingInput>(),
            existing.DatabaseBindingIds ?? Array.Empty<long>(),
            existing.VariableBindingIds ?? Array.Empty<long>(),
            existing.ModelConfigId,
            existing.ModelName,
            existing.Temperature,
            existing.MaxTokens,
            existing.DefaultWorkflowId,
            existing.DefaultWorkflowName,
            existing.EnableMemory,
            existing.EnableShortTermMemory,
            existing.EnableLongTermMemory,
            existing.LongTermMemoryTopK,
            existing.KnowledgeBaseIds ?? Array.Empty<long>(),
            existing.PluginBindings?.Select(binding => new AgentPluginBindingInput(
                binding.PluginId,
                binding.SortOrder,
                binding.IsEnabled,
                binding.ToolConfigJson,
                binding.ToolBindings?.Select(tool => new AgentPluginToolBindingInput(
                    tool.ApiId,
                    tool.IsEnabled,
                    tool.TimeoutSeconds,
                    tool.FailurePolicy,
                    tool.ParameterBindings.Select(param => new AgentPluginParameterBindingInput(
                        param.ParameterName,
                        param.ValueSource,
                        param.LiteralValue,
                        param.VariableKey)).ToArray())).ToArray() ?? Array.Empty<AgentPluginToolBindingInput>()))
                .ToArray() ?? Array.Empty<AgentPluginBindingInput>(),
            workspaceId);

        await commandService.UpdateAsync(_tenantProvider.GetTenantId(), botId, updateRequest, cancellationToken);

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            has_change = true,
            check_not_pass = false,
            branch = "PersonalDraft",
            same_with_online = false,
            check_not_pass_msg = string.Empty
        }));
    }

    [HttpPost("draftbot/delete")]
    [HttpPost("/api/draftbot/delete")]
    public async Task<ActionResult<object>> DeleteDraftBot(
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
            return Ok(CozeCompatGatewaySupport.Success(new { }));
        }

        await teamAgentService.DeleteAsync(_tenantProvider.GetTenantId(), botId, cancellationToken);
        return Ok(CozeCompatGatewaySupport.Success(new { }));
    }

    [HttpPost("draftbot/duplicate")]
    [HttpPost("/api/draftbot/duplicate")]
    public async Task<ActionResult<object>> DuplicateDraftBot(
        [FromBody] CozeGetDraftBotDisplayInfoRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var teamAgentService = HttpContext.RequestServices.GetService<ITeamAgentService>();
        if (teamAgentService is null)
        {
            return Ok(CozeCompatGatewaySupport.Success(new
            {
                bot_id = string.Empty,
                name = string.Empty,
                user_info = BuildCreatorInfo(currentUser)
            }));
        }

        var duplicatedId = await teamAgentService.DuplicateAsync(
            _tenantProvider.GetTenantId(),
            currentUser.UserId,
            botId,
            cancellationToken);
        var duplicated = await teamAgentService.GetByIdAsync(_tenantProvider.GetTenantId(), duplicatedId, cancellationToken);

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            bot_id = duplicatedId.ToString(CultureInfo.InvariantCulture),
            name = duplicated?.Name ?? string.Empty,
            user_info = BuildCreatorInfo(currentUser)
        }));
    }

    [HttpPost("bot/upload_file")]
    [HttpPost("/api/bot/upload_file")]
    public ActionResult<object> UploadBotFile()
    {
        return Ok(CozeCompatGatewaySupport.Success(new
        {
            file_id = Guid.NewGuid().ToString("N"),
            file_url = string.Empty
        }));
    }

    [HttpPost("space/info")]
    [HttpPost("/api/space/info")]
    public async Task<ActionResult<object>> GetSpaceInfo(
        [FromBody] CozeGetSpaceInfoRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var workspaces = await _workspacePortalService.ListWorkspacesAsync(
            _tenantProvider.GetTenantId(),
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        var match = string.IsNullOrWhiteSpace(request?.space_id)
            ? workspaces.FirstOrDefault()
            : workspaces.FirstOrDefault(item => string.Equals(item.Id, request.space_id, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return Ok(CozeCompatGatewaySupport.Success(new { data = (object?)null }));
        }

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            data = new
            {
                id = match.Id,
                name = match.Name,
                description = match.Description ?? string.Empty,
                icon_url = match.Icon ?? string.Empty,
                space_type = 1,
                role_type = string.Equals(match.RoleCode, "Owner", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : string.Equals(match.RoleCode, "Admin", StringComparison.OrdinalIgnoreCase)
                        ? 2
                        : 3,
                space_mode = 0
            }
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

    private static object BuildCreatorInfo(CurrentUserInfo currentUser)
    {
        return new
        {
            id = currentUser.UserId.ToString(CultureInfo.InvariantCulture),
            name = string.IsNullOrWhiteSpace(currentUser.DisplayName) ? currentUser.Username : currentUser.DisplayName,
            avatar_url = string.Empty,
            user_unique_name = currentUser.Username,
            user_label = new { }
        };
    }

    private static object BuildDraftBotInfoPayload(AgentDetail detail, string? spaceId)
    {
        return new
        {
            id = detail.Id.ToString(CultureInfo.InvariantCulture),
            name = detail.Name,
            description = detail.Description ?? string.Empty,
            icon_uri = detail.AvatarUrl ?? string.Empty,
            icon_url = detail.AvatarUrl ?? string.Empty,
            visibility = 0,
            has_published = detail.PublishVersion > 0 ? 1 : 0,
            create_time = CozeCompatGatewaySupport.ToUnixMilliseconds(detail.CreatedAt).ToString(CultureInfo.InvariantCulture),
            update_time = detail.UpdatedAt is null
                ? CozeCompatGatewaySupport.ToUnixMilliseconds(detail.CreatedAt).ToString(CultureInfo.InvariantCulture)
                : CozeCompatGatewaySupport.ToUnixMilliseconds(detail.UpdatedAt.Value).ToString(CultureInfo.InvariantCulture),
            creator_id = detail.CreatorId.ToString(CultureInfo.InvariantCulture),
            space_id = spaceId ?? detail.WorkspaceId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            work_info = new
            {
                prompt = detail.SystemPrompt ?? string.Empty,
                tools = string.Empty,
                dataset = string.Empty,
                workflow = string.Empty
            },
            connectors = Array.Empty<object>(),
            bot_mode = 0,
            agents = Array.Empty<object>(),
            canvas_data = string.Empty,
            version = detail.PublishVersion.ToString(CultureInfo.InvariantCulture),
            bot_tag_info = Array.Empty<object>(),
            branch = "PersonalDraft",
            commit_version = detail.PublishVersion.ToString(CultureInfo.InvariantCulture),
            commit_time = detail.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture),
            publish_time = detail.PublishedAt?.ToString("O", CultureInfo.InvariantCulture)
        };
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

public sealed record CozeDraftBotCreateCompatRequest(
    string? space_id,
    string? name,
    string? description,
    string? icon_uri,
    string? create_from,
    string? app_id,
    int? business_type,
    string? folder_id,
    CozeDraftBotWorkInfoCompat? work_info);

public sealed record CozeGetDraftBotInfoCompatRequest(
    string? space_id,
    string? bot_id,
    string? version,
    string? source,
    int? botMode,
    string? commit_version);

public sealed record CozeUpdateDraftBotCompatRequest(
    string? space_id,
    string? bot_id,
    CozeDraftBotWorkInfoCompat? work_info,
    string? name,
    string? description,
    string? icon_uri,
    string? commit_version);

public sealed record CozeDraftBotWorkInfoCompat(
    string? prompt,
    string? tools,
    string? dataset,
    string? workflow);
