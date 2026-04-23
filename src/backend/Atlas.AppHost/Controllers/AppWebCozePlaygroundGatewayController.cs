using System.Globalization;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Controllers.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/app-web/coze-playground")]
[Authorize]
public sealed class AppWebCozePlaygroundGatewayController : ControllerBase
{
    private readonly IWorkspacePortalService _workspacePortalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AppWebCozePlaygroundGatewayController(
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _workspacePortalService = workspacePortalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("space/list")]
    [HttpPost("/api/playground_api/space/list")]
    public async Task<ActionResult<object>> GetSpaceList(
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

    [HttpPost("space/save")]
    [HttpPost("/api/playground_api/space/save")]
    public async Task<ActionResult<object>> SaveSpace(
        [FromBody] CozeSaveSpaceRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        var workspaces = await _workspacePortalService.ListWorkspacesAsync(
            tenantId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        var targetSpaceId = request?.space_id;
        var matched = !string.IsNullOrWhiteSpace(targetSpaceId)
            ? workspaces.FirstOrDefault(item => string.Equals(item.Id, targetSpaceId, StringComparison.OrdinalIgnoreCase))
            : workspaces.FirstOrDefault();

        if (matched is null && !string.IsNullOrWhiteSpace(request?.name))
        {
            var createdWorkspaceId = await _workspacePortalService.CreateWorkspaceAsync(
                tenantId,
                currentUser.UserId,
                new WorkspaceCreateRequest(
                    request.name.Trim(),
                    string.IsNullOrWhiteSpace(request.description) ? null : request.description.Trim(),
                    null),
                cancellationToken);

            matched = (await _workspacePortalService.ListWorkspacesAsync(
                    tenantId,
                    currentUser.UserId,
                    currentUser.IsPlatformAdmin,
                    cancellationToken))
                .FirstOrDefault(item => string.Equals(item.Id, createdWorkspaceId.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase));
        }

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            id = matched?.Id ?? targetSpaceId ?? string.Empty,
            check_not_pass = false
        }));
    }

    [HttpPost("space/info")]
    [HttpPost("/api/playground_api/space/info")]
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
                space_type = string.Equals(match.RoleCode, "Owner", StringComparison.OrdinalIgnoreCase) ? 1 : 2,
                role_type = string.Equals(match.RoleCode, "Owner", StringComparison.OrdinalIgnoreCase) ? 1 : string.Equals(match.RoleCode, "Admin", StringComparison.OrdinalIgnoreCase) ? 2 : 3,
                space_mode = 0
            }
        }));
    }

    [HttpPost("get_type_list")]
    [HttpPost("/api/playground_api/get_type_list")]
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

    [HttpPost("delete_prompt_resource")]
    [HttpPost("/api/playground_api/delete_prompt_resource")]
    public async Task<ActionResult<object>> DeletePromptResource(
        [FromBody] CozeDeletePromptResourceRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.prompt_resource_id, out var promptId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("prompt_resource_id is invalid"));
        }

        var promptService = HttpContext.RequestServices.GetService<IAiPromptService>();
        if (promptService is null)
        {
            return Ok(CozeCompatGatewaySupport.SuccessWithoutData());
        }

        await promptService.DeleteAsync(_tenantProvider.GetTenantId(), promptId, cancellationToken);
        return Ok(CozeCompatGatewaySupport.SuccessWithoutData());
    }

    [HttpPost("draftbot/get_draft_bot_info")]
    [HttpPost("/api/playground_api/draftbot/get_draft_bot_info")]
    public async Task<ActionResult<object>> GetDraftBotInfoAgw(
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

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            bot_info = new
            {
                bot_id = detail.Id.ToString(CultureInfo.InvariantCulture),
                name = detail.Name,
                description = detail.Description ?? string.Empty,
                icon_uri = detail.AvatarUrl ?? string.Empty,
                icon_url = detail.AvatarUrl ?? string.Empty,
                creator_id = detail.CreatorId.ToString(CultureInfo.InvariantCulture),
                create_time = CozeCompatGatewaySupport.ToUnixMilliseconds(detail.CreatedAt).ToString(CultureInfo.InvariantCulture),
                update_time = detail.UpdatedAt is null
                    ? CozeCompatGatewaySupport.ToUnixMilliseconds(detail.CreatedAt).ToString(CultureInfo.InvariantCulture)
                    : CozeCompatGatewaySupport.ToUnixMilliseconds(detail.UpdatedAt.Value).ToString(CultureInfo.InvariantCulture),
                prompt_info = new
                {
                    prompt = detail.SystemPrompt ?? string.Empty
                },
                business_type = 0,
                bot_mode = 0
            },
            bot_option_data = new
            {
                plugin_api_detail_map = new Dictionary<string, object>()
            },
            has_unpublished_change = false,
            in_collaboration = false,
            same_with_online = detail.PublishVersion > 0,
            editable = true,
            deletable = true,
            has_publish = detail.PublishVersion > 0,
            space_id = request?.space_id ?? detail.WorkspaceId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            connectors = Array.Empty<object>(),
            branch = "PersonalDraft",
            commit_version = detail.PublishVersion.ToString(CultureInfo.InvariantCulture)
        }));
    }

    [HttpPost("report_user_behavior")]
    [HttpPost("/api/playground_api/report_user_behavior")]
    public ActionResult<object> ReportUserBehavior([FromBody] object? _request)
    {
        return Ok(new
        {
            code = 0,
            msg = "success"
        });
    }

    [HttpPost("get_imagex_url")]
    [HttpPost("/api/playground_api/get_imagex_url")]
    public async Task<ActionResult<object>> GetImagexShortUrl(
        [FromBody] CozeGetImagexShortUrlCompatRequest? request,
        CancellationToken cancellationToken)
    {
        var fileStorageService = HttpContext.RequestServices.GetService<IFileStorageService>();
        if (fileStorageService is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("file storage service unavailable"));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var urlInfo = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var uri in request?.uris ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                continue;
            }

            if (TryParseAtlasFileUri(uri, out var fileId))
            {
                var signedUrl = await fileStorageService.GenerateSignedUrlAsync(
                    tenantId,
                    fileId,
                    expiresInSeconds: 600,
                    cancellationToken);
                urlInfo[uri] = new
                {
                    url = signedUrl.Url,
                    review_status = true
                };
                continue;
            }

            urlInfo[uri] = new
            {
                url = uri,
                review_status = Uri.TryCreate(uri, UriKind.Absolute, out _)
            };
        }

        return Ok(new
        {
            code = 0,
            msg = "success",
            data = new
            {
                url_info = urlInfo
            }
        });
    }

    [HttpPost("draftbot/generate_store_category")]
    [HttpPost("/api/playground_api/draftbot/generate_store_category")]
    public async Task<ActionResult<object>> GenerateStoreCategory(
        [FromBody] CozeGenerateStoreCategoryCompatRequest? request,
        CancellationToken cancellationToken)
    {
        var marketplaceService = HttpContext.RequestServices.GetService<IAiMarketplaceService>();
        if (marketplaceService is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("marketplace service unavailable"));
        }

        var categories = await marketplaceService.GetCategoriesAsync(_tenantProvider.GetTenantId(), cancellationToken);
        var selectedCategory = SelectDefaultMarketplaceCategory(categories, request);

        return Ok(new
        {
            code = 0,
            msg = "success",
            data = new
            {
                category_id = selectedCategory?.Id.ToString(CultureInfo.InvariantCulture) ?? string.Empty
            }
        });
    }

    [HttpGet("draftbot/get_user_query_collect_option")]
    [HttpGet("/api/playground_api/draftbot/get_user_query_collect_option")]
    public ActionResult<object> GetUserQueryCollectOption()
    {
        return Ok(new
        {
            code = 0,
            msg = "success",
            data = new
            {
                support_connectors = new object[]
                {
                    new
                    {
                        id = "1001",
                        name = "Web SDK",
                        icon = string.Empty,
                        connector_status = 0,
                        share_link = string.Empty
                    },
                    new
                    {
                        id = "3001",
                        name = "OAuth Demo",
                        icon = string.Empty,
                        connector_status = 0,
                        share_link = string.Empty
                    }
                },
                private_policy_template = "/open/policy/bot/{bot_id}"
            }
        });
    }

    [HttpPost("draftbot/generate_user_query_collect_policy")]
    [HttpPost("/api/playground_api/draftbot/generate_user_query_collect_policy")]
    public ActionResult<object> GenerateUserQueryCollectPolicy(
        [FromBody] CozeGenerateUserQueryCollectPolicyCompatRequest? request)
    {
        var botId = string.IsNullOrWhiteSpace(request?.bot_id) ? "0" : request!.bot_id!;
        return Ok(new
        {
            code = 0,
            msg = "success",
            data = new
            {
                policy_link = $"/open/policy/bot/{Uri.EscapeDataString(botId)}?developer_name={Uri.EscapeDataString(request?.developer_name ?? string.Empty)}"
            }
        });
    }

    [HttpPost("move_draft_bot")]
    [HttpPost("/api/playground_api/move_draft_bot")]
    public async Task<ActionResult<object>> MoveDraftBot(
        [FromBody] CozeMoveDraftBotCompatRequest? request,
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

        var tenantId = _tenantProvider.GetTenantId();
        var detail = await queryService.GetByIdAsync(tenantId, botId, cancellationToken);
        if (detail is null)
        {
            return Ok(CozeCompatGatewaySupport.Fail("bot not found"));
        }

        var targetSpaceId = TryParsePositiveId(request?.target_spaceId, out var parsedTargetSpaceId)
            ? parsedTargetSpaceId
            : detail.WorkspaceId ?? 0;
        var fromSpaceId = TryParsePositiveId(request?.from_spaceId, out var parsedFromSpaceId)
            ? parsedFromSpaceId
            : detail.WorkspaceId ?? 0;
        var moveAction = request?.move_action ?? 0;
        var asyncTask = BuildMoveDraftBotAsyncTaskPayload(botId, fromSpaceId, targetSpaceId);

        if (moveAction is 3 or 4)
        {
            return Ok(new
            {
                bot_status = 1,
                async_task = asyncTask,
                forbid_move = false
            });
        }

        if (moveAction == 5)
        {
            return Ok(new
            {
                bot_status = 1,
                async_task = asyncTask,
                forbid_move = false
            });
        }

        if (targetSpaceId <= 0)
        {
            return Ok(new
            {
                bot_status = 3,
                async_task = asyncTask,
                forbid_move = true
            });
        }

        var updateRequest = new AgentUpdateRequest(
            detail.Name,
            detail.Description,
            detail.AvatarUrl,
            detail.SystemPrompt,
            detail.PersonaMarkdown,
            detail.Goals,
            detail.ReplyLogic,
            detail.OutputFormat,
            detail.Constraints,
            detail.OpeningMessage,
            detail.PresetQuestions ?? Array.Empty<string>(),
            detail.KnowledgeBindings?.Select(binding => new AgentKnowledgeBindingInput(
                binding.KnowledgeBaseId,
                binding.IsEnabled,
                binding.InvokeMode,
                binding.TopK,
                binding.ScoreThreshold,
                binding.EnabledContentTypes,
                binding.RewriteQueryTemplate)).ToArray() ?? Array.Empty<AgentKnowledgeBindingInput>(),
            detail.DatabaseBindings?.Select(binding => new AgentDatabaseBindingInput(
                binding.DatabaseId,
                binding.Alias,
                binding.AccessMode,
                binding.TableAllowlist,
                binding.IsDefault)).ToArray() ?? Array.Empty<AgentDatabaseBindingInput>(),
            detail.VariableBindings?.Select(binding => new AgentVariableBindingInput(
                binding.VariableId,
                binding.Alias,
                binding.IsRequired,
                binding.DefaultValueOverride)).ToArray() ?? Array.Empty<AgentVariableBindingInput>(),
            detail.DatabaseBindingIds ?? Array.Empty<long>(),
            detail.VariableBindingIds ?? Array.Empty<long>(),
            detail.ModelConfigId,
            detail.ModelName,
            detail.Temperature,
            detail.MaxTokens,
            detail.DefaultWorkflowId,
            detail.DefaultWorkflowName,
            detail.EnableMemory,
            detail.EnableShortTermMemory,
            detail.EnableLongTermMemory,
            detail.LongTermMemoryTopK,
            detail.KnowledgeBaseIds ?? Array.Empty<long>(),
            detail.PluginBindings?.Select(binding => new AgentPluginBindingInput(
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
            targetSpaceId);

        await commandService.UpdateAsync(tenantId, botId, updateRequest, cancellationToken);

        return Ok(new
        {
            bot_status = 1,
            async_task = asyncTask,
            forbid_move = false
        });
    }

    [HttpGet("marketplace/product/favorite/list")]
    public ActionResult<object> GetMarketplaceFavoriteList()
    {
        return Ok(new
        {
            code = 0,
            message = "success",
            data = new
            {
                favorite_products = Array.Empty<object>(),
                has_more = false
            }
        });
    }

    [HttpGet("marketplace/product/favorite/list.v2")]
    public ActionResult<object> GetMarketplaceFavoriteListV2()
    {
        return Ok(new
        {
            code = 0,
            message = "success",
            data = new
            {
                favorite_entities = Array.Empty<object>(),
                cursor_id = string.Empty,
                has_more = false,
                entity_user_trigger_config = new Dictionary<string, object>()
            }
        });
    }

    [HttpGet("open/workspaces")]
    [HttpGet("/v1/workspaces")]
    public async Task<ActionResult<object>> OpenSpaceList(
        [FromQuery(Name = "page_num")] int? pageNum,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "enterprise_id")] string? enterpriseId,
        [FromQuery(Name = "coze_account_id")] string? cozeAccountId,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        var workspaces = await _workspacePortalService.ListWorkspacesAsync(
            tenantId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        var filtered = workspaces
            .Where(item =>
                (string.IsNullOrWhiteSpace(enterpriseId) || string.Equals(item.OrgId, enterpriseId.Trim(), StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(cozeAccountId) || string.Equals(item.OrgId, cozeAccountId.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var normalizedPage = pageNum is > 0 ? pageNum.Value : 1;
        var normalizedSize = pageSize is > 0 ? Math.Min(pageSize.Value, 50) : 20;
        var skipped = Math.Max(0, (normalizedPage - 1) * normalizedSize);
        var pagedItems = filtered
            .Skip(skipped)
            .Take(normalizedSize)
            .Select(CozeCompatGatewaySupport.MapOpenWorkspace)
            .ToArray();

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            workspaces = pagedItems,
            total_count = filtered.Length
        }));
    }

    [HttpPost("open/workspaces")]
    [HttpPost("/v1/workspaces")]
    public async Task<ActionResult<object>> OpenCreateSpace(
        [FromBody] CozeOpenCreateSpaceRequest? request,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        var normalizedName = string.IsNullOrWhiteSpace(request?.Name)
            ? "未命名工作空间"
            : request.Name.Trim();
        var workspaceId = await _workspacePortalService.CreateWorkspaceAsync(
            tenantId,
            currentUser.UserId,
            new WorkspaceCreateRequest(
                normalizedName,
                request?.Description?.Trim(),
                null),
            cancellationToken);

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            id = workspaceId.ToString(CultureInfo.InvariantCulture)
        }));
    }

    [HttpGet("open/workspaces/{workspaceId}/members")]
    [HttpGet("/v1/workspaces/{workspaceId}/members")]
    public async Task<ActionResult<object>> OpenSpaceMemberList(
        [FromRoute] string workspaceId,
        [FromQuery(Name = "page_num")] int? pageNum,
        [FromQuery(Name = "page_size")] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("workspace_id is invalid"));
        }

        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        var members = await _workspacePortalService.GetMembersAsync(
            tenantId,
            parsedWorkspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        var normalizedPage = pageNum is > 0 ? pageNum.Value : 1;
        var normalizedSize = pageSize is > 0 ? Math.Min(pageSize.Value, 50) : 20;
        var skipped = Math.Max(0, (normalizedPage - 1) * normalizedSize);
        var pagedItems = members
            .Skip(skipped)
            .Take(normalizedSize)
            .Select(CozeCompatGatewaySupport.MapOpenSpaceMember)
            .ToArray();

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            items = pagedItems,
            total_count = members.Count
        }));
    }

    [HttpPost("open/workspaces/{workspaceId}/members")]
    [HttpPost("/v1/workspaces/{workspaceId}/members")]
    public async Task<ActionResult<object>> OpenAddSpaceMember(
        [FromRoute] string workspaceId,
        [FromBody] CozeOpenAddSpaceMemberRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("workspace_id is invalid"));
        }

        var candidateUserId = request?.Users?
            .Select(item => item.UserId?.Trim())
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (string.IsNullOrWhiteSpace(candidateUserId))
        {
            return Ok(CozeCompatGatewaySupport.Success(new
            {
                added_success_user_ids = Array.Empty<string>(),
                invited_success_user_ids = Array.Empty<string>(),
                not_exist_user_ids = Array.Empty<string>(),
                already_joined_user_ids = Array.Empty<string>(),
                already_invited_user_ids = Array.Empty<string>()
            }));
        }

        if (!TryParsePositiveId(candidateUserId, out var parsedUserId))
        {
            return Ok(CozeCompatGatewaySupport.Success(new
            {
                added_success_user_ids = Array.Empty<string>(),
                invited_success_user_ids = Array.Empty<string>(),
                not_exist_user_ids = new[] { candidateUserId },
                already_joined_user_ids = Array.Empty<string>(),
                already_invited_user_ids = Array.Empty<string>()
            }));
        }

        var target = request?.Users?.FirstOrDefault(item => string.Equals(item.UserId, candidateUserId, StringComparison.OrdinalIgnoreCase));
        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        await _workspacePortalService.AddMemberAsync(
            tenantId,
            parsedWorkspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            new WorkspaceMemberCreateRequest(
                parsedUserId.ToString(CultureInfo.InvariantCulture),
                CozeCompatGatewaySupport.ToWorkspaceRoleCode(target?.RoleType)),
            cancellationToken);

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            added_success_user_ids = new[] { parsedUserId.ToString(CultureInfo.InvariantCulture) },
            invited_success_user_ids = Array.Empty<string>(),
            not_exist_user_ids = Array.Empty<string>(),
            already_joined_user_ids = Array.Empty<string>(),
            already_invited_user_ids = Array.Empty<string>()
        }));
    }

    [HttpDelete("open/workspaces/{workspaceId}/members")]
    [HttpDelete("/v1/workspaces/{workspaceId}/members")]
    public async Task<ActionResult<object>> OpenRemoveSpaceMember(
        [FromRoute] string workspaceId,
        [FromBody] CozeOpenRemoveSpaceMemberRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("workspace_id is invalid"));
        }

        var candidateUserId = request?.UserIds?
            .Select(item => item?.Trim())
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (string.IsNullOrWhiteSpace(candidateUserId))
        {
            return Ok(CozeCompatGatewaySupport.Success(new
            {
                removed_success_user_ids = Array.Empty<string>(),
                not_in_workspace_user_ids = Array.Empty<string>(),
                owner_not_support_remove_user_ids = Array.Empty<string>()
            }));
        }

        if (!TryParsePositiveId(candidateUserId, out var parsedUserId))
        {
            return Ok(CozeCompatGatewaySupport.Success(new
            {
                removed_success_user_ids = Array.Empty<string>(),
                not_in_workspace_user_ids = new[] { candidateUserId },
                owner_not_support_remove_user_ids = Array.Empty<string>()
            }));
        }

        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        await _workspacePortalService.RemoveMemberAsync(
            tenantId,
            parsedWorkspaceId,
            parsedUserId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            removed_success_user_ids = new[] { parsedUserId.ToString(CultureInfo.InvariantCulture) },
            not_in_workspace_user_ids = Array.Empty<string>(),
            owner_not_support_remove_user_ids = Array.Empty<string>()
        }));
    }

    [HttpPost("open/workspaces/{workspaceId}/members/apply")]
    [HttpPost("/v1/workspaces/{workspaceId}/members/apply")]
    public ActionResult<object> OpenApplyJoinSpace(
        [FromRoute] string workspaceId,
        [FromBody] CozeOpenApplyJoinSpaceRequest? request)
    {
        if (!TryParsePositiveId(workspaceId, out _))
        {
            return Ok(CozeCompatGatewaySupport.Fail("workspace_id is invalid"));
        }

        var appliedUserIds = request?.UserIds?
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();

        return Ok(CozeCompatGatewaySupport.Success(new
        {
            applied_success_user_ids = appliedUserIds,
            applied_failed_user_ids = Array.Empty<string>()
        }));
    }

    [HttpPut("open/workspaces/{workspaceId}/members/{userId}")]
    [HttpPut("/v1/workspaces/{workspaceId}/members/{userId}")]
    public async Task<ActionResult<object>> OpenUpdateSpaceMember(
        [FromRoute] string workspaceId,
        [FromRoute] string userId,
        [FromBody] CozeOpenUpdateSpaceMemberRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("workspace_id is invalid"));
        }

        if (!TryParsePositiveId(userId, out var parsedUserId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("user_id is invalid"));
        }

        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        await _workspacePortalService.UpdateMemberRoleAsync(
            tenantId,
            parsedWorkspaceId,
            parsedUserId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            new WorkspaceMemberRoleUpdateRequest(CozeCompatGatewaySupport.ToWorkspaceRoleCode(request?.RoleType)),
            cancellationToken);

        return Ok(CozeCompatGatewaySupport.SuccessWithoutData());
    }

    [HttpDelete("open/workspaces/{workspaceId}")]
    [HttpDelete("/v1/workspaces/{workspaceId}")]
    public async Task<ActionResult<object>> OpenRemoveSpace(
        [FromRoute] string workspaceId,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(CozeCompatGatewaySupport.Fail("workspace_id is invalid"));
        }

        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        await _workspacePortalService.DeleteWorkspaceAsync(
            tenantId,
            parsedWorkspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        return Ok(CozeCompatGatewaySupport.SuccessWithoutData());
    }

    [HttpGet("open/bots/{botId}")]
    [HttpGet("/v1/bots/{botId}")]
    public ActionResult<object> OpenGetBotInfo([FromRoute] string botId)
    {
        return Ok(CozeCompatGatewaySupport.Success(new
        {
            id = botId,
            bot_id = botId,
            name = $"bot-{botId}",
            description = string.Empty,
            icon_url = string.Empty,
            publish_status = "draft"
        }));
    }

    [HttpGet("open/bots/{botId}/versions")]
    [HttpGet("/v1/bots/{botId}/versions")]
    public ActionResult<object> OpenListBotVersions([FromRoute] string botId)
    {
        return Ok(CozeCompatGatewaySupport.Success(new
        {
            items = Array.Empty<object>(),
            has_more = false
        }));
    }

    [HttpPost("open/bots/{botId}/collaboration_mode")]
    [HttpPost("/v1/bots/{botId}/collaboration_mode")]
    public ActionResult<object> OpenSwitchBotDevelopMode(
        [FromRoute] string botId,
        [FromBody] CozeOpenSwitchBotDevelopModeRequest? request)
    {
        return Ok(CozeCompatGatewaySupport.SuccessWithoutData());
    }

    [HttpPost("{**path}")]
    public ActionResult<object> PostFallback([FromRoute] string? path)
    {
        return Ok(CozeCompatGatewaySupport.Success(CozeCompatGatewaySupport.BuildPlaygroundFallbackData(path)));
    }

    [HttpGet("{**path}")]
    public ActionResult<object> GetFallback([FromRoute] string? path)
    {
        return Ok(CozeCompatGatewaySupport.Success(CozeCompatGatewaySupport.BuildPlaygroundFallbackData(path)));
    }

    private (TenantId TenantId, CurrentUserInfo CurrentUser) ResolveOpenWorkspaceContext()
    {
        return (_tenantProvider.GetTenantId(), _currentUserAccessor.GetCurrentUserOrThrow());
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

    private static bool TryParseAtlasFileUri(string? raw, out long fileId)
    {
        fileId = 0;
        const string prefix = "atlas-file:";
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(
                   raw[prefix.Length..],
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out fileId)
               && fileId > 0;
    }

    private static AiProductCategoryItem? SelectDefaultMarketplaceCategory(
        IReadOnlyList<AiProductCategoryItem> categories,
        CozeGenerateStoreCategoryCompatRequest? request)
    {
        if (categories.Count == 0)
        {
            return null;
        }

        var keywords = $"{request?.bot_name} {request?.bot_description} {request?.prompt}";
        var matched = categories.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Name)
            && keywords.Contains(item.Name, StringComparison.OrdinalIgnoreCase));
        return matched ?? categories
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .FirstOrDefault();
    }

    private static object BuildMoveDraftBotAsyncTaskPayload(long botId, long fromSpaceId, long targetSpaceId)
    {
        return new
        {
            transfer_resource_plugin_list = Array.Empty<object>(),
            transfer_resource_workflow_list = Array.Empty<object>(),
            transfer_resource_knowledge_list = Array.Empty<object>(),
            task_info = new
            {
                TargetSpaceId = targetSpaceId.ToString(CultureInfo.InvariantCulture),
                OriSpaceId = fromSpaceId.ToString(CultureInfo.InvariantCulture),
                BotIds = new[] { botId.ToString(CultureInfo.InvariantCulture) }
            }
        };
    }

    private static object MapSpaceItem(WorkspaceListItem item)
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

public sealed record CozeMoveDraftBotCompatRequest(
    string? bot_id,
    string? target_spaceId,
    string? from_spaceId,
    int? move_action);

public sealed record CozeGenerateStoreCategoryCompatRequest(
    string? bot_name,
    string? bot_description,
    string? prompt);

public sealed record CozeGenerateUserQueryCollectPolicyCompatRequest(
    string? bot_id,
    string? developer_name,
    string? contact_information);

public sealed record CozeGetImagexShortUrlCompatRequest(
    IReadOnlyList<string>? uris,
    int? scene);
