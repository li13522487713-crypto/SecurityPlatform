using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Presentation.Shared.Controllers.Ai;

public abstract class CozeWorkflowCompatControllerBase : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ChatHistoryNodeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Llm",
        "IntentDetector",
        "QuestionAnswer"
    };

    private static readonly HashSet<string> GlobalVariableNodeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssignVariable",
        "VariableAssignerWithinLoop",
        "VariableAggregator",
        "Ltm"
    };

    private readonly IWorkflowV2CommandService _commandService;
    private readonly IWorkflowV2QueryService _queryService;
    private readonly IWorkflowV2ExecutionService _executionService;
    private readonly ICanvasValidator _canvasValidator;
    private readonly IWorkspacePortalService _workspacePortalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    protected CozeWorkflowCompatControllerBase(
        IWorkflowV2CommandService commandService,
        IWorkflowV2QueryService queryService,
        IWorkflowV2ExecutionService executionService,
        ICanvasValidator canvasValidator,
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _commandService = commandService;
        _queryService = queryService;
        _executionService = executionService;
        _canvasValidator = canvasValidator;
        _workspacePortalService = workspacePortalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("/api/playground_api/space/list")]
    [Authorize]
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

        return Ok(Success(new
        {
            bot_space_list = botSpaces,
            recently_used_space_list = botSpaces,
            has_personal_space = false,
            total = filtered.Count,
            has_more = skipped + botSpaces.Length < filtered.Count
        }));
    }

    [HttpPost("canvas")]
    [Authorize]
    public async Task<ActionResult<object>> GetCanvasInfo(
        [FromBody] CozeGetCanvasInfoRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var result = await _queryService.GetAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            cancellationToken);

        if (result is null)
        {
            return Ok(Fail("workflow not found"));
        }

        return Ok(Success(new
        {
            workflow = new
            {
                workflow_id = result.Id.ToString(),
                name = result.Name,
                desc = result.Description,
                icon_uri = string.Empty,
                status = ToCozeWorkflowStatus(result.Status),
                plugin_id = result.Status == WorkflowLifecycleStatus.Published ? result.Id.ToString() : "0",
                create_time = ToUnixMilliseconds(result.CreatedAt),
                update_time = ToUnixMilliseconds(result.UpdatedAt),
                space_id = request.space_id ?? string.Empty,
                schema_json = result.CanvasJson,
                flow_mode = ToCozeWorkflowMode(result.Mode),
                workflow_version = result.LatestVersionNumber.ToString()
            },
            vcs_data = new
            {
                submit_commit_id = result.CommitId,
                draft_commit_id = result.CommitId,
                publish_commit_id = result.CommitId,
                can_edit = true
            },
            db_data = new
            {
                status = ToCozeWorkflowStatus(result.Status)
            },
            operation_info = new { },
            is_bind_agent = false,
            bind_biz_id = string.Empty,
            bind_biz_type = 0,
            workflow_version = result.LatestVersionNumber.ToString()
        }));
    }

    [HttpPost("save")]
    [Authorize]
    public async Task<ActionResult<object>> SaveWorkflow(
        [FromBody] CozeSaveWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var saveRequest = new WorkflowV2SaveDraftRequest(request.schema ?? "{\"nodes\":[],\"connections\":[]}", request.submit_commit_id);
        await _commandService.SaveDraftAsync(_tenantProvider.GetTenantId(), workflowId, saveRequest, cancellationToken);

        return Ok(Success(new
        {
            name = string.Empty,
            url = string.Empty,
            status = 2,
            workflow_status = 2,
            is_version_gray = false
        }));
    }

    [HttpPost("publish")]
    [Authorize]
    public async Task<ActionResult<object>> PublishWorkflow(
        [FromBody] CozePublishWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _commandService.PublishAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            userId,
            new WorkflowV2PublishRequest(request.version_description),
            cancellationToken);

        return Ok(Success(new
        {
            workflow_id = request.workflow_id,
            publish_commit_id = request.commit_id ?? string.Empty,
            success = true,
            vcs_submit_commit_id = request.commit_id ?? string.Empty,
            vcs_publish_commit_id = request.commit_id ?? string.Empty
        }));
    }

    [HttpPost("node_type")]
    [Authorize]
    public async Task<ActionResult<object>> QueryWorkflowNodeTypes(CancellationToken cancellationToken)
    {
        var nodeTypes = await _queryService.GetNodeTypesAsync(cancellationToken);
        return Ok(Success(new
        {
            node_types = nodeTypes.Select(item => item.Key).ToArray(),
            sub_workflow_node_types = Array.Empty<string>(),
            nodes_properties = nodeTypes.Select(item => new
            {
                id = item.Key,
                type = item.Key,
                is_enable_chat_history = ChatHistoryNodeKeys.Contains(item.Key),
                is_enable_user_query = false,
                is_ref_global_variable = GlobalVariableNodeKeys.Contains(item.Key)
            }).ToArray(),
            sub_workflow_nodes_properties = Array.Empty<object>()
        }));
    }

    [HttpPost("node_template_list")]
    [Authorize]
    public async Task<ActionResult<object>> NodeTemplateList(
        [FromBody] CozeNodeTemplateListRequest? request,
        CancellationToken cancellationToken)
    {
        var templates = await _queryService.GetNodeTemplatesAsync(cancellationToken);
        var nodeTypes = await _queryService.GetNodeTypesAsync(cancellationToken);
        var metadataMap = nodeTypes.ToDictionary(item => item.Key, item => item, StringComparer.OrdinalIgnoreCase);
        var allowedNodeTypes = request?.node_types is { Length: > 0 }
            ? new HashSet<string>(request.node_types, StringComparer.OrdinalIgnoreCase)
            : null;

        var filteredTemplates = templates
            .Where(item => allowedNodeTypes is null || allowedNodeTypes.Contains(item.Key))
            .ToArray();

        var categories = filteredTemplates
            .GroupBy(item => metadataMap.TryGetValue(item.Key, out var metadata) ? metadata.Category : "General", StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                name = group.Key,
                node_type_list = group.Select(item => item.Key).ToArray(),
                plugin_api_id_list = Array.Empty<string>(),
                plugin_category_id_list = Array.Empty<string>()
            })
            .ToArray();

        return Ok(Success(new
        {
            template_list = filteredTemplates.Select(item =>
            {
                metadataMap.TryGetValue(item.Key, out var metadata);
                return new
                {
                    id = item.Key,
                    type = 0,
                    name = item.Name,
                    desc = metadata?.Description ?? string.Empty,
                    icon_url = metadata?.UiMeta?.Icon ?? string.Empty,
                    support_batch = metadata?.UiMeta?.SupportsBatch == true ? 2 : 1,
                    node_type = item.Key,
                    color = metadata?.UiMeta?.Color ?? string.Empty,
                    commercial_node = false,
                    plugin_list = Array.Empty<string>(),
                    volc_res_list = Array.Empty<string>()
                };
            }).ToArray(),
            cate_list = categories,
            plugin_api_list = Array.Empty<object>(),
            plugin_category_list = Array.Empty<object>()
        }));
    }

    [HttpPost("old_validate")]
    [Authorize]
    public async Task<ActionResult<object>> ValidateSchema(
        [FromBody] CozeValidateSchemaRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var result = _canvasValidator.ValidateCanvas(request.schema);

        return Ok(Success((result.Errors ?? Array.Empty<CanvasValidationIssue>())
            .Select(error => new
            {
                message = error.Message,
                type = error.Code,
                node_error = error.NodeKey is null ? null : new { node_id = error.NodeKey },
                path_error = error.SourcePort is null && error.TargetPort is null
                    ? null
                    : new
                    {
                        start = error.SourcePort,
                        end = error.TargetPort,
                        path = error.NodeKey is null ? Array.Empty<string>() : new[] { error.NodeKey }
                    }
            })
            .ToArray()));
    }

    [HttpPost("test_run")]
    [Authorize]
    public async Task<ActionResult<object>> WorkFlowTestRun(
        [FromBody] CozeWorkFlowTestRunRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var inputsJson = request.input is null ? null : JsonSerializer.Serialize(request.input, JsonOptions);
        var result = await _executionService.SyncRunAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            _currentUserAccessor.GetCurrentUserOrThrow().UserId,
            new WorkflowV2RunRequest(inputsJson, "draft"),
            cancellationToken);

        return Ok(Success(new
        {
            workflow_id = request.workflow_id,
            execute_id = result.ExecutionId,
            session_id = string.Empty
        }));
    }

    [HttpGet("get_process")]
    [Authorize]
    public async Task<ActionResult<object>> GetWorkFlowProcess(
        [FromQuery] CozeGetWorkflowProcessRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseExecutionId(request.execute_id, out var executionId))
        {
            return Ok(Fail("execute_id is required"));
        }

        var result = await _queryService.GetExecutionProcessAsync(
            _tenantProvider.GetTenantId(),
            executionId,
            cancellationToken);

        if (result is null)
        {
            return Ok(Fail("execution not found"));
        }

        return Ok(Success(new
        {
            workFlowId = result.WorkflowId.ToString(),
            executeId = result.Id.ToString(),
            executeStatus = ToCozeExecutionStatus(result.Status),
            nodeResults = result.NodeExecutions.Select(node => new
            {
                nodeId = node.NodeKey,
                NodeType = node.NodeType.ToString(),
                NodeName = node.NodeKey,
                nodeStatus = ToCozeExecutionStatus(node.Status),
                errorInfo = node.ErrorMessage,
                input = node.InputsJson,
                output = node.OutputsJson,
                nodeExeCost = node.DurationMs is null ? null : $"{node.DurationMs}ms",
                errorLevel = string.IsNullOrWhiteSpace(node.ErrorMessage) ? null : "error",
                executeId = node.ExecutionId.ToString()
            }).ToArray(),
            reason = result.ErrorMessage,
            logID = result.Id.ToString(),
            projectId = string.Empty
        }));
    }

    [HttpPost("test_resume")]
    [Authorize]
    public async Task<ActionResult<object>> WorkFlowTestResume(
        [FromBody] CozeWorkflowTestResumeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseExecutionId(request.execute_id, out var executionId))
        {
            return Ok(Fail("execute_id is required"));
        }

        await _executionService.ResumeAsync(
            _tenantProvider.GetTenantId(),
            executionId,
            new WorkflowV2ResumeRequest(request.data, null),
            cancellationToken);

        return Ok(SuccessWithoutData());
    }

    [HttpPost("cancel")]
    [Authorize]
    public async Task<ActionResult<object>> CancelWorkflow(
        [FromBody] CozeCancelWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseExecutionId(request.execute_id, out var executionId))
        {
            return Ok(Fail("execute_id is required"));
        }

        await _executionService.CancelAsync(_tenantProvider.GetTenantId(), executionId, cancellationToken);
        return Ok(SuccessWithoutData());
    }

    [HttpPost("nodeDebug")]
    [Authorize]
    public async Task<ActionResult<object>> WorkflowNodeDebug(
        [FromBody] CozeWorkflowNodeDebugRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        if (string.IsNullOrWhiteSpace(request.node_id))
        {
            return Ok(Fail("node_id is required"));
        }

        var result = await _executionService.DebugNodeAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            _currentUserAccessor.GetCurrentUserOrThrow().UserId,
            new WorkflowV2NodeDebugRequest(
                request.node_id,
                request.input is null ? null : JsonSerializer.Serialize(request.input, JsonOptions),
                "draft"),
            cancellationToken);

        return Ok(Success(new
        {
            workflow_id = request.workflow_id,
            node_id = request.node_id,
            execute_id = result.ExecutionId,
            session_id = string.Empty
        }));
    }

    [HttpPost("workflow_references")]
    [Authorize]
    public async Task<ActionResult<object>> GetWorkflowReferences(
        [FromBody] CozeWorkflowReferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var dependencies = await _queryService.GetDependenciesAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            cancellationToken);

        return Ok(Success(new
        {
            workflow_list = (dependencies?.SubWorkflows ?? Array.Empty<WorkflowV2DependencyItemDto>())
                .Select(item => new
                {
                    workflow_id = item.ResourceId,
                    space_id = request.space_id ?? string.Empty,
                    name = item.Name,
                    desc = item.Description,
                    icon = string.Empty,
                    plugin_id = "0",
                    flow_mode = 0
                })
                .ToArray()
        }));
    }

    [HttpPost("released_workflows")]
    [Authorize]
    public async Task<ActionResult<object>> GetReleasedWorkflows(
        [FromBody] CozeReleasedWorkflowsRequest? request,
        CancellationToken cancellationToken)
    {
        var page = request?.page > 0 ? request.page!.Value : 1;
        var size = request?.size > 0 ? request.size!.Value : 20;
        var result = await _queryService.ListPublishedAsync(
            _tenantProvider.GetTenantId(),
            request?.name,
            page,
            size,
            cancellationToken);

        return Ok(Success(new
        {
            workflow_list = result.Items.Select(item => new
            {
                workflow_id = item.Id.ToString(),
                plugin_id = item.Status == WorkflowLifecycleStatus.Published ? item.Id.ToString() : "0",
                space_id = request?.space_id ?? string.Empty,
                name = item.Name,
                desc = item.Description,
                icon = string.Empty,
                inputs = (object?)null,
                outputs = (object?)null,
                flow_mode = ToCozeWorkflowMode(item.Mode)
            }).ToArray(),
            total = result.Total
        }));
    }

    [HttpPost("copy")]
    [Authorize]
    public async Task<ActionResult<object>> CopyWorkflow(
        [FromBody] CozeCopyWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var copiedId = await _commandService.CopyAsync(
            _tenantProvider.GetTenantId(),
            _currentUserAccessor.GetCurrentUserOrThrow().UserId,
            workflowId,
            cancellationToken);

        return Ok(Success(new
        {
            workflow_id = copiedId.ToString(),
            schema_type = 0
        }));
    }

    protected static object Success(object? data)
    {
        return new
        {
            code = 0,
            msg = "success",
            data,
            BaseResp = new { }
        };
    }

    protected static object SuccessWithoutData()
    {
        return new
        {
            code = 0,
            msg = "success",
            BaseResp = new { }
        };
    }

    protected static object Fail(string message)
    {
        return new
        {
            code = 400,
            msg = message,
            BaseResp = new { }
        };
    }

    private static bool TryParseWorkflowId(string? raw, out long workflowId)
    {
        return long.TryParse(raw, out workflowId);
    }

    private static bool TryParseExecutionId(string? raw, out long executionId)
    {
        return long.TryParse(raw, out executionId);
    }

    private static long ToUnixMilliseconds(DateTime value)
    {
        return new DateTimeOffset(value).ToUnixTimeMilliseconds();
    }

    private static int ToCozeWorkflowMode(WorkflowMode mode)
    {
        return mode == WorkflowMode.ChatFlow ? 3 : 0;
    }

    private static int ToCozeWorkflowStatus(WorkflowLifecycleStatus status)
    {
        return status == WorkflowLifecycleStatus.Published ? 3 : 2;
    }

    private static int ToCozeExecutionStatus(ExecutionStatus status)
    {
        return status switch
        {
            ExecutionStatus.Running => 1,
            ExecutionStatus.Completed => 2,
            ExecutionStatus.Failed => 3,
            ExecutionStatus.Cancelled or ExecutionStatus.Interrupted => 4,
            _ => 1
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

public sealed record CozeGetCanvasInfoRequest(string? workflow_id, string? space_id);

public sealed record CozeSaveWorkflowRequest(
    string workflow_id,
    string? schema,
    string? space_id,
    string? name,
    string? desc,
    string? icon_uri,
    string? submit_commit_id,
    bool? ignore_status_transfer,
    string? save_version);

public sealed record CozePublishWorkflowRequest(
    string workflow_id,
    string? space_id,
    bool has_collaborator,
    string? env,
    string? commit_id,
    bool? force,
    string? workflow_version,
    string? version_description);

public sealed record CozeNodeTemplateListRequest(string[]? need_types, string[]? node_types);

public sealed record CozeValidateSchemaRequest(string workflow_id, string schema, string? bind_project_id, string? bind_bot_id);

public sealed record CozeWorkFlowTestRunRequest(string workflow_id, Dictionary<string, string>? input, string? space_id, string? bot_id, string? commit_id, string? project_id);

public sealed record CozeGetWorkflowProcessRequest(string workflow_id, string? execute_id, string? sub_execute_id, bool? need_async, string? log_id, string? node_id);

public sealed record CozeWorkflowTestResumeRequest(string workflow_id, string execute_id, string? event_id, string? data, string? space_id);

public sealed record CozeCancelWorkflowRequest(string execute_id, string? space_id, string? workflow_id, bool? async_subflow);

public sealed record CozeWorkflowNodeDebugRequest(
    string? workflow_id,
    string? node_id,
    Dictionary<string, string>? input,
    Dictionary<string, string>? batch,
    string? space_id,
    string? bot_id,
    string? project_id,
    Dictionary<string, string>? setting);

public sealed record CozeWorkflowReferencesRequest(string workflow_id, string? space_id);

public sealed record CozeReleasedWorkflowsRequest(
    int? page,
    int? size,
    int? type,
    string? name,
    string[]? workflow_ids,
    string? space_id,
    int? flow_mode);

public sealed record CozeCopyWorkflowRequest(string workflow_id, string? space_id);

public sealed record CozeGetSpaceListRequest(
    string? search_word,
    string? enterprise_id,
    string? organization_id,
    int? scope_type,
    int? page,
    int? size);
