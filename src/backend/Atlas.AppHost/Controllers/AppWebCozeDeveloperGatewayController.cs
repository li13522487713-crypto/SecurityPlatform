using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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

    [HttpPost("draftbot/publish/connector/list")]
    [HttpPost("/api/draftbot/publish/connector/list")]
    public async Task<ActionResult<object>> PublishConnectorList(
        [FromBody] CozeDraftBotPublishConnectorListRequest? request,
        CancellationToken cancellationToken)
    {
        var connectorState = TryParsePositiveId(request?.bot_id, out var botId)
            ? await LoadAgentConnectorStateAsync(botId, cancellationToken)
            : null;
        var state = connectorState?.State ?? new CozeCompatConnectorStateDocument();
        var publishConnectors = SupportedPublishConnectors
            .Select(definition => BuildPublishConnectorPayload(definition, state, request?.space_id, request?.bot_id))
            .ToArray();

        return Ok(new
        {
            code = 0,
            msg = "success",
            publish_connector_list = publishConnectors,
            submit_bot_market_option = new
            {
                can_open_source = true
            },
            connector_brand_info_map = SupportedPublishConnectors
                .Where(definition => !string.IsNullOrWhiteSpace(definition.BrandId))
                .ToDictionary(
                    definition => definition.BrandId!,
                    definition => (object)new
                    {
                        id = definition.BrandId!,
                        name = definition.Name,
                        icon = definition.Icon
                    },
                    StringComparer.OrdinalIgnoreCase),
            publish_tips = new
            {
                cost_tips = string.IsNullOrWhiteSpace(request?.bot_id)
                    ? string.Empty
                    : "Compat publish tips"
            }
        });
    }

    [HttpPost("connector/query_schemas")]
    [HttpPost("/api/connector/query_schemas")]
    public ActionResult<object> QueryConnectorSchemas([FromBody] CozeQueryConnectorSchemaRequest? request)
    {
        var connector = GetSupportedConnector(request?.connector_id);
        if (connector is null || connector.SchemaFields.Count == 0)
        {
            return Ok(new
            {
                code = 0,
                msg = "success",
                title_text = string.Empty,
                start_text = string.Empty,
                schema_area_pages = Array.Empty<object>()
            });
        }

        return Ok(new
        {
            code = 0,
            msg = "success",
            title_text = connector.ConfigureTitle,
            start_text = connector.ConfigureDescription,
            schema_area_pages = new object[]
            {
                new
                {
                    schema_area = new
                    {
                        title_text = connector.ConfigureTitle,
                        description = connector.ConfigureDescription,
                        step_order = 1,
                        schema_list = connector.SchemaFields.Select(field => new
                        {
                            name = field.Name,
                            title = field.Title,
                            required = field.Required,
                            component = "Input",
                            type = "string",
                            rules = field.Required
                                ? new object[]
                                {
                                    new
                                    {
                                        required = true,
                                        message = $"{field.Title} is required"
                                    }
                                }
                                : Array.Empty<object>()
                        }).ToArray()
                    }
                }
            }
        });
    }

    [HttpPost("draftbot/bind/get_connector_config")]
    [HttpPost("/api/draftbot/bind/get_connector_config")]
    public async Task<ActionResult<object>> GetBindConnectorConfig(
        [FromBody] CozeDraftBotConnectorConfigRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var connectorContext = await LoadAgentConnectorStateAsync(botId, cancellationToken);
        if (connectorContext is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        var definition = GetSupportedConnector(request?.connector_id);
        if (definition is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("connector_id is invalid"));
        }

        var connectorState = GetOrCreateConnectorState(connectorContext.Value.State, definition);
        return Ok(new
        {
            code = 0,
            msg = "success",
            config = new
            {
                connector_id = definition.Id,
                app_id = connectorState.AppId ?? string.Empty,
                detail = connectorState.Detail
            }
        });
    }

    [HttpPost("draftbot/bind/save_connector_config")]
    [HttpPost("/api/draftbot/bind/save_connector_config")]
    public async Task<ActionResult<object>> SaveBindConnectorConfig(
        [FromBody] CozeDraftBotConnectorConfigRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var connectorContext = await LoadAgentConnectorStateAsync(botId, cancellationToken);
        if (connectorContext is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        var definition = GetSupportedConnector(request?.connector_id);
        if (definition is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("connector_id is invalid"));
        }

        var connectorState = GetOrCreateConnectorState(connectorContext.Value.State, definition);
        connectorState.AppId = request?.app_id;
        connectorState.Detail = NormalizeConnectorDetail(request?.detail);
        await PersistAgentConnectorStateAsync(connectorContext.Value.Entity, connectorContext.Value.State, cancellationToken);

        return Ok(new
        {
            code = 0,
            msg = "success"
        });
    }

    [HttpPost("draftbot/bind/connector")]
    [HttpPost("/api/draftbot/bind/connector")]
    public async Task<ActionResult<object>> BindConnector(
        [FromBody] CozeBindConnectorCompatRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var connectorContext = await LoadAgentConnectorStateAsync(botId, cancellationToken);
        if (connectorContext is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        var definition = GetSupportedConnector(request?.connector_id);
        if (definition is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("connector_id is invalid"));
        }

        var connectorState = GetOrCreateConnectorState(connectorContext.Value.State, definition);
        connectorState.Detail = NormalizeConnectorDetail(request?.connector_info);
        connectorState.BindId = string.IsNullOrWhiteSpace(connectorState.BindId)
            ? Guid.NewGuid().ToString("N")
            : connectorState.BindId;
        connectorState.ConfigStatus = definition.DefaultConfiguredStatus;
        connectorState.ConnectorStatus = 0;
        await PersistAgentConnectorStateAsync(connectorContext.Value.Entity, connectorContext.Value.State, cancellationToken);

        return Ok(new
        {
            code = 0,
            msg = "success",
            bind_id = connectorState.BindId,
            bind_bot_id = request?.bot_id ?? string.Empty,
            bind_bot_name = connectorContext.Value.Entity.Name,
            bind_space_id = connectorContext.Value.Entity.WorkspaceId?.ToString(CultureInfo.InvariantCulture) ?? request?.space_id ?? string.Empty,
            bind_agent_type = request?.agent_type ?? 0
        });
    }

    [HttpPost("draftbot/unbind/connector")]
    [HttpPost("/api/draftbot/unbind/connector")]
    public async Task<ActionResult<object>> UnBindConnector(
        [FromBody] CozeUnBindConnectorCompatRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var connectorContext = await LoadAgentConnectorStateAsync(botId, cancellationToken);
        if (connectorContext is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        var definition = GetSupportedConnector(request?.connector_id);
        if (definition is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("connector_id is invalid"));
        }

        var connectorState = GetOrCreateConnectorState(connectorContext.Value.State, definition);
        connectorState.BindId = string.Empty;
        connectorState.Detail = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        connectorState.ConfigStatus = 2;
        connectorState.ConnectorStatus = 0;
        await PersistAgentConnectorStateAsync(connectorContext.Value.Entity, connectorContext.Value.State, cancellationToken);

        return Ok(new
        {
            code = 0,
            msg = "success"
        });
    }

    [HttpPost("draftbot/publish")]
    [HttpPost("/api/draftbot/publish")]
    public async Task<ActionResult<object>> PublishDraftBot(
        [FromBody] CozePublishDraftBotCompatRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot_id is required"));
        }

        var connectorContext = await LoadAgentConnectorStateAsync(botId, cancellationToken);
        if (connectorContext is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        var selectedConnectorIds = request?.connectors?.Keys
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
        if (selectedConnectorIds.Length == 0)
        {
            return Ok(CozeCompatGatewaySupport.Fail("connectors are required"));
        }

        var publishResult = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var hasSuccess = false;
        foreach (var connectorId in selectedConnectorIds)
        {
            var definition = GetSupportedConnector(connectorId);
            if (definition is null)
            {
                publishResult[connectorId] = BuildPublishResultPayload(
                    connector: new
                    {
                        id = connectorId,
                        name = connectorId,
                        share_link = string.Empty,
                        bind_info = new Dictionary<string, string>()
                    },
                    status: 2,
                    message: "connector is not supported");
                continue;
            }

            var connectorState = GetOrCreateConnectorState(connectorContext.Value.State, definition);
            var requestConnectorDetail = request?.connectors is not null && request.connectors.TryGetValue(connectorId, out var submittedDetail)
                ? NormalizeConnectorDetail(submittedDetail)
                : connectorState.Detail;
            if (requestConnectorDetail.Count > 0)
            {
                connectorState.Detail = requestConnectorDetail;
            }

            if (definition.RequiresBinding && string.IsNullOrWhiteSpace(connectorState.BindId))
            {
                publishResult[connectorId] = BuildPublishResultPayload(
                    connector: BuildPublishResultConnectorPayload(definition, connectorState, request?.space_id, request?.bot_id),
                    status: 2,
                    message: "connector is not configured");
                continue;
            }

            if (definition.RequiresUserAuth && connectorState.AuthStatus != 1)
            {
                publishResult[connectorId] = BuildPublishResultPayload(
                    connector: BuildPublishResultConnectorPayload(definition, connectorState, request?.space_id, request?.bot_id),
                    status: 2,
                    message: "connector is not authorized");
                continue;
            }

            connectorState.LastPublishedAt = now.ToUnixTimeMilliseconds();
            connectorState.LastPublishId = request?.publish_id;
            connectorState.LastCommitVersion = request?.commit_version;
            connectorState.LastHistoryInfo = request?.history_info;
            connectorState.IsLastPublished = true;
            connectorState.ShareLink = BuildConnectorShareLink(definition, request?.space_id, request?.bot_id);
            connectorState.ConfigStatus = definition.DefaultConfiguredStatus;
            connectorState.ConnectorStatus = 0;
            publishResult[connectorId] = BuildPublishResultPayload(
                connector: BuildPublishResultConnectorPayload(definition, connectorState, request?.space_id, request?.bot_id),
                status: 1,
                message: string.Empty);
            hasSuccess = true;
        }

        if (hasSuccess)
        {
            var commandService = HttpContext.RequestServices.GetService<IAgentCommandService>();
            if (commandService is not null)
            {
                await commandService.PublishAsync(_tenantProvider.GetTenantId(), botId, cancellationToken);
            }
        }

        await PersistAgentConnectorStateAsync(connectorContext.Value.Entity, connectorContext.Value.State, cancellationToken);

        return Ok(new
        {
            code = 0,
            msg = "success",
            data = new
            {
                publish_result = publishResult,
                check_not_pass = false,
                hit_manual_check = false,
                publish_monetization_result = false
            }
        });
    }

    [HttpGet("user/auth/connector_state")]
    [HttpGet("/api/user/auth/connector_state")]
    public ActionResult<object> GetConnectorAuthState([FromQuery] string? connector_id)
    {
        var definition = GetSupportedConnector(connector_id);
        if (definition is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("connector_id is invalid"));
        }

        return Ok(new
        {
            code = 0,
            msg = "success",
            data = new
            {
                state = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["connector_id"] = definition.Id,
                    ["origin"] = "publish",
                    ["issued_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
                }
            }
        });
    }

    [HttpPost("user/auth/cancel")]
    [HttpPost("/api/user/auth/cancel")]
    public async Task<ActionResult<object>> CancelUserAuth(
        [FromBody] CozeCancelUserAuthCompatRequest? request,
        CancellationToken cancellationToken)
    {
        var definition = GetSupportedConnector(request?.connector_id);
        if (definition is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("connector_id is invalid"));
        }

        var repository = HttpContext.RequestServices.GetService<AgentRepository>();
        if (repository is null)
        {
            return Ok(CozeCompatGatewaySupport.SuccessWithoutData());
        }

        var tenantId = _tenantProvider.GetTenantId();
        var paged = await repository.GetPagedAsync(
            tenantId,
            keyword: null,
            status: null,
            workspaceId: null,
            pageIndex: 1,
            pageSize: 500,
            cancellationToken);

        foreach (var agent in paged.Items)
        {
            var document = ParseConnectorStateDocument(agent.PublishedConnectorConfigJson);
            if (!document.Connectors.TryGetValue(definition.Id, out var connectorState))
            {
                continue;
            }

            connectorState.AuthStatus = 2;
            connectorState.AuthState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            connectorState.ConfigStatus = definition.RequiresBinding ? connectorState.ConfigStatus : 2;
            await PersistAgentConnectorStateAsync(agent, document, cancellationToken);
        }

        return Ok(new
        {
            code = 0,
            msg = "success"
        });
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

    [HttpPost("developer/get_icon")]
    [HttpPost("/api/developer/get_icon")]
    public ActionResult<object> GetIcon()
    {
        // Returns a minimal stub so the Coze SDK bot-creation dialog can render
        // an icon section without a network error. Real icon management is not
        // required at this stage.
        return Ok(CozeCompatGatewaySupport.Success(new
        {
            icon_url = string.Empty,
            icon_list = new[] 
            { 
                new { url = "", uri = "" } 
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

    private static readonly IReadOnlyList<CozeCompatConnectorDefinition> SupportedPublishConnectors =
    [
        new(
            "1001",
            "Web SDK",
            string.Empty,
            "Atlas Web SDK publish channel",
            BindType: 6,
            DefaultConfiguredStatus: 1,
            BrandId: "1001",
            RequiresBinding: false,
            ConfigureTitle: "Web SDK",
            ConfigureDescription: "Web SDK channel is ready to publish without extra configuration.",
            SchemaFields: Array.Empty<CozeCompatConnectorSchemaField>()),
        new(
            "2001",
            "Generic Endpoint",
            string.Empty,
            "Configurable endpoint publish channel",
            BindType: 3,
            DefaultConfiguredStatus: 1,
            BrandId: null,
            RequiresBinding: true,
            ConfigureTitle: "Configure Generic Endpoint",
            ConfigureDescription: "Provide the endpoint address and token that Coze native publish should use.",
            SchemaFields:
            [
                new("endpoint_url", "Endpoint URL", true),
                new("api_key", "API Key", false),
                new("share_link", "Share Link", false)
            ]),
        new(
            "3001",
            "OAuth Demo",
            string.Empty,
            "OAuth based publish channel",
            BindType: 2,
            DefaultConfiguredStatus: 2,
            BrandId: null,
            RequiresBinding: false,
            ConfigureTitle: "OAuth Demo",
            ConfigureDescription: "Authorize this connector before publishing.",
            SchemaFields: Array.Empty<CozeCompatConnectorSchemaField>(),
            RequiresUserAuth: true)
    ];

    private static CozeCompatConnectorDefinition? GetSupportedConnector(string? connectorId)
        => SupportedPublishConnectors.FirstOrDefault(item => string.Equals(item.Id, connectorId, StringComparison.OrdinalIgnoreCase));

    private async Task<(Agent Entity, CozeCompatConnectorStateDocument State)?> LoadAgentConnectorStateAsync(long botId, CancellationToken cancellationToken)
    {
        var repository = HttpContext.RequestServices.GetService<AgentRepository>();
        if (repository is null)
        {
            return null;
        }

        var entity = await repository.FindByIdAsync(_tenantProvider.GetTenantId(), botId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return (entity, ParseConnectorStateDocument(entity.PublishedConnectorConfigJson));
    }

    private async Task PersistAgentConnectorStateAsync(
        Agent entity,
        CozeCompatConnectorStateDocument state,
        CancellationToken cancellationToken)
    {
        var repository = HttpContext.RequestServices.GetService<AgentRepository>();
        if (repository is null)
        {
            return;
        }

        entity.Update(
            entity.Name,
            entity.Description,
            entity.AvatarUrl,
            entity.SystemPrompt,
            entity.PersonaMarkdown,
            entity.Goals,
            entity.ReplyLogic,
            entity.OutputFormat,
            entity.Constraints,
            entity.OpeningMessage,
            entity.PresetQuestionsJson,
            entity.DatabaseBindingsJson,
            entity.VariableBindingsJson,
            entity.ModelConfigId,
            entity.ModelName,
            entity.Temperature,
            entity.MaxTokens,
            entity.DefaultWorkflowId,
            entity.DefaultWorkflowName,
            entity.EnableMemory,
            entity.EnableShortTermMemory,
            entity.EnableLongTermMemory,
            entity.LongTermMemoryTopK,
            entity.Mode,
            entity.PromptVersion,
            entity.LayoutConfigJson,
            entity.DebugConfigJson,
            JsonSerializer.Serialize(state, JsonOptions),
            entity.WorkspaceId);
        await repository.UpdateAsync(entity, cancellationToken);
    }

    private static CozeCompatConnectorStateDocument ParseConnectorStateDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CozeCompatConnectorStateDocument();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<CozeCompatConnectorStateDocument>(json, JsonOptions);
            return parsed ?? new CozeCompatConnectorStateDocument();
        }
        catch
        {
            return new CozeCompatConnectorStateDocument();
        }
    }

    private static CozeCompatConnectorItemState GetOrCreateConnectorState(
        CozeCompatConnectorStateDocument state,
        CozeCompatConnectorDefinition definition)
    {
        if (!state.Connectors.TryGetValue(definition.Id, out var connectorState))
        {
            connectorState = new CozeCompatConnectorItemState
            {
                ConfigStatus = definition.RequiresBinding ? 2 : definition.DefaultConfiguredStatus,
                ConnectorStatus = 0,
                IsLastPublished = true
            };
            state.Connectors[definition.Id] = connectorState;
        }

        connectorState.Detail = NormalizeConnectorDetail(connectorState.Detail);
        return connectorState;
    }

    private static Dictionary<string, string> NormalizeConnectorDetail(IDictionary<string, string>? detail)
    {
        return detail?
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(
                item => item.Key.Trim(),
                item => item.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static object BuildPublishConnectorPayload(
        CozeCompatConnectorDefinition definition,
        CozeCompatConnectorStateDocument state,
        string? spaceId,
        string? botId)
    {
        var connectorState = GetOrCreateConnectorState(state, definition);
        var shareLink = string.IsNullOrWhiteSpace(connectorState.ShareLink)
            ? BuildConnectorShareLink(definition, spaceId, botId)
            : connectorState.ShareLink!;
        return new
        {
            id = definition.Id,
            name = definition.Name,
            icon = definition.Icon,
            desc = definition.Description,
            share_link = shareLink,
            config_status = connectorState.ConfigStatus <= 0
                ? (definition.RequiresBinding ? 2 : definition.DefaultConfiguredStatus)
                : connectorState.ConfigStatus,
            last_publish_time = connectorState.LastPublishedAt,
            bind_type = definition.BindType,
            bind_info = connectorState.Detail,
            bind_id = connectorState.BindId ?? string.Empty,
            auth_status = connectorState.AuthStatus,
            auth_login_info = definition.RequiresUserAuth
                ? new
                {
                    app_id = definition.Id,
                    response_type = "code",
                    authorize_url = $"/sign?connector_id={Uri.EscapeDataString(definition.Id)}",
                    scope = "bot.publish",
                    client_id = definition.Id
                }
                : null,
            is_last_published = connectorState.IsLastPublished,
            connector_status = connectorState.ConnectorStatus,
            privacy_policy = string.Empty,
            user_agreement = string.Empty,
            allow_punish = 0,
            not_allow_reason = string.Empty,
            config_status_toast = string.Empty,
            brand_id = definition.BrandId,
            support_monetization = false
        };
    }

    private static string BuildConnectorShareLink(
        CozeCompatConnectorDefinition definition,
        string? spaceId,
        string? botId)
    {
        if (!string.Equals(definition.Id, "1001", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(spaceId) || string.IsNullOrWhiteSpace(botId))
        {
            return string.Empty;
        }

        return $"/space/{spaceId}/bot/{botId}";
    }

    private static object BuildPublishResultConnectorPayload(
        CozeCompatConnectorDefinition definition,
        CozeCompatConnectorItemState state,
        string? spaceId,
        string? botId)
    {
        return new
        {
            id = definition.Id,
            name = definition.Name,
            icon = definition.Icon,
            share_link = string.IsNullOrWhiteSpace(state.ShareLink)
                ? BuildConnectorShareLink(definition, spaceId, botId)
                : state.ShareLink,
            bind_info = state.Detail
        };
    }

    private static object BuildPublishResultPayload(object connector, int status, string message)
    {
        return new
        {
            connector,
            code = status == 1 ? 0 : 1,
            msg = message,
            publish_result_status = status
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

public sealed record CozeDraftBotPublishConnectorListRequest(
    string? space_id,
    string? bot_id,
    string? commit_version);

public sealed record CozeQueryConnectorSchemaRequest(
    string? connector_id,
    string? scene);

public sealed record CozeDraftBotConnectorConfigRequest(
    string? space_id,
    string? bot_id,
    string? connector_id,
    string? app_id,
    Dictionary<string, string>? detail,
    long? agent_type);

public sealed record CozeBindConnectorCompatRequest(
    string? space_id,
    string? bot_id,
    string? connector_id,
    Dictionary<string, string>? connector_info,
    long? agent_type);

public sealed record CozeUnBindConnectorCompatRequest(
    string? space_id,
    string? bot_id,
    string? connector_id,
    string? bind_id,
    long? agent_type);

public sealed record CozePublishDraftBotCompatRequest(
    string? space_id,
    string? bot_id,
    CozeDraftBotWorkInfoCompat? work_info,
    Dictionary<string, Dictionary<string, string>>? connector_list,
    Dictionary<string, Dictionary<string, string>>? connectors,
    int? botMode,
    string? canvas_data,
    string? publish_id,
    string? commit_version,
    int? publish_type,
    string? pre_publish_ext,
    string? history_info);

public sealed record CozeCompatConnectorDefinition(
    string Id,
    string Name,
    string Icon,
    string Description,
    int BindType,
    int DefaultConfiguredStatus,
    string? BrandId,
    bool RequiresBinding,
    string ConfigureTitle,
    string ConfigureDescription,
    IReadOnlyList<CozeCompatConnectorSchemaField> SchemaFields,
    bool RequiresUserAuth = false);

public sealed record CozeCompatConnectorSchemaField(
    string Name,
    string Title,
    bool Required);

public sealed class CozeCompatConnectorStateDocument
{
    public Dictionary<string, CozeCompatConnectorItemState> Connectors { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CozeCompatConnectorItemState
{
    public string? BindId { get; set; }

    public string? AppId { get; set; }

    public Dictionary<string, string> Detail { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public long LastPublishedAt { get; set; }

    public string? LastPublishId { get; set; }

    public string? LastCommitVersion { get; set; }

    public string? LastHistoryInfo { get; set; }

    public string? ShareLink { get; set; }

    public int ConfigStatus { get; set; }

    public int ConnectorStatus { get; set; }

    public bool IsLastPublished { get; set; } = true;

    public int AuthStatus { get; set; } = 2;

    public Dictionary<string, string> AuthState { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed record CozeCancelUserAuthCompatRequest(string? connector_id);
