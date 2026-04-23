using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Presentation.Shared.Controllers.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/app-web/workflow-sdk")]
public sealed class AppWebWorkflowGatewayController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICozeWorkflowCommandService _commandService;
    private readonly ICozeWorkflowQueryService _queryService;
    private readonly ICozeWorkflowExecutionService _executionService;
    private readonly ICozeWorkflowPlanCompiler _planCompiler;
    private readonly ICanvasValidator _canvasValidator;
    private readonly IWorkflowTraceService _traceService;
    private readonly IWorkflowTriggerService _triggerService;
    private readonly IWorkflowCollaboratorService _collaboratorService;
    private readonly IChatFlowRoleService _chatFlowRoleService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AppWebWorkflowGatewayController(
        ICozeWorkflowCommandService commandService,
        ICozeWorkflowQueryService queryService,
        ICozeWorkflowExecutionService executionService,
        ICozeWorkflowPlanCompiler planCompiler,
        ICanvasValidator canvasValidator,
        IWorkflowTraceService traceService,
        IWorkflowTriggerService triggerService,
        IWorkflowCollaboratorService collaboratorService,
        IChatFlowRoleService chatFlowRoleService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _commandService = commandService;
        _queryService = queryService;
        _executionService = executionService;
        _planCompiler = planCompiler;
        _canvasValidator = canvasValidator;
        _traceService = traceService;
        _triggerService = triggerService;
        _collaboratorService = collaboratorService;
        _chatFlowRoleService = chatFlowRoleService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<ActionResult<object>> CreateWorkflow(
        [FromBody] CozeCreateWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(request.name) ? "未命名工作流" : request.name.Trim();
        var mode = request.flow_mode == 3 ? WorkflowMode.ChatFlow : WorkflowMode.Standard;
        var workspaceId = TryParsePositiveId(request.space_id, out var parsedWorkspaceId)
            ? (long?)parsedWorkspaceId
            : null;

        var workflowId = await _commandService.CreateAsync(
            _tenantProvider.GetTenantId(),
            _currentUserAccessor.GetCurrentUserOrThrow().UserId,
            new CozeWorkflowCreateCommand(name, request.desc, mode, workspaceId),
            cancellationToken);

        return Ok(Success(new
        {
            workflow_id = workflowId.ToString(),
            name,
            url = $"/work_flow/{workflowId}/editor",
            status = 2,
            type = 0,
            node_list = Array.Empty<object>(),
            external_flow_info = string.Empty,
            submit_commit_id = workflowId.ToString()
        }));
    }

    [HttpPost("canvas")]
    [HttpPost("/api/workflow_api/canvas")]
    [Authorize]
    public async Task<ActionResult<object>> GetCanvasInfo(
        [FromBody] CozeGetCanvasInfoRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var result = await _queryService.GetAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
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
                schema_json = result.SchemaJson,
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

    [HttpPost("node_template_list")]
    [HttpPost("/api/workflow_api/node_template_list")]
    [Authorize]
    public async Task<ActionResult<object>> NodeTemplateList(
        [FromBody] CozeNodeTemplateListRequest? request,
        CancellationToken cancellationToken)
    {
        var templates = await _queryService.GetNodeTemplatesAsync(cancellationToken);
        var nodeTypes = await _queryService.GetNodeTypesAsync(cancellationToken);
        var metadataMap = nodeTypes.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var allowedNodeTypes = ResolveAllowedNodeTypes(request);

        var filteredTemplates = templates
            .Where(template =>
            {
                if (allowedNodeTypes is null)
                {
                    return true;
                }

                var nodeTypeCode = ToCozeNodeTypeCode(template.Key);
                return allowedNodeTypes.Contains(template.Key) || allowedNodeTypes.Contains(nodeTypeCode);
            })
            .Select(template =>
            {
                metadataMap.TryGetValue(template.Key, out var metadata);
                var nodeTypeCode = ToCozeNodeTypeCode(template.Key);
                var templateType = int.TryParse(nodeTypeCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedType)
                    ? parsedType
                    : (int?)null;

                return new
                {
                    id = template.Key,
                    type = templateType,
                    name = template.Name,
                    desc = metadata?.Description ?? string.Empty,
                    icon_url = metadata?.UiMeta?.Icon ?? string.Empty,
                    support_batch = metadata?.UiMeta?.SupportsBatch == true ? 2 : 1,
                    node_type = nodeTypeCode,
                    color = metadata?.UiMeta?.Color ?? string.Empty,
                    commercial_node = false,
                    plugin_list = Array.Empty<string>(),
                    volc_res_list = Array.Empty<string>()
                };
            })
            .ToArray();

        var cateList = filteredTemplates
            .GroupBy(
                item => metadataMap.TryGetValue(item.id, out var metadata) ? metadata.Category : string.Empty,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                name = group.Key,
                node_type_list = group.Select(item => item.node_type).ToArray(),
                plugin_api_id_list = Array.Empty<string>(),
                plugin_category_id_list = Array.Empty<string>()
            })
            .ToArray();

        return Ok(Success(new
        {
            template_list = filteredTemplates,
            cate_list = cateList,
            plugin_api_list = Array.Empty<object>(),
            plugin_category_list = Array.Empty<object>()
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

        await _commandService.SaveDraftAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            new CozeWorkflowSaveDraftCommand(request.schema ?? "{}", request.submit_commit_id),
            cancellationToken);

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

        await _commandService.PublishAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            _currentUserAccessor.GetCurrentUserOrThrow().UserId,
            new CozeWorkflowPublishCommand(request.version_description),
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

    [HttpPost("submit")]
    [Authorize]
    public async Task<ActionResult<object>> SubmitWorkflow(
        [FromBody] CozeSubmitWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var detail = await _queryService.GetAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
        var commitId = detail?.CommitId ?? workflowId.ToString();
        return Ok(Success(new
        {
            need_merge = false,
            submit_commit_id = commitId
        }));
    }

    [HttpPost("latest")]
    [Authorize]
    public async Task<ActionResult<object>> CheckLatestSubmitVersion(
        [FromBody] CozeCheckLatestSubmitVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var detail = await _queryService.GetAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
        var commitId = detail?.CommitId ?? workflowId.ToString();
        return Ok(Success(new
        {
            is_latest = true,
            submit_commit_id = commitId,
            latest_commit_id = commitId
        }));
    }

    [HttpPost("delete")]
    [Authorize]
    public async Task<ActionResult<object>> DeleteWorkflow(
        [FromBody] CozeDeleteWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        await _commandService.DeleteAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
        return Ok(SuccessWithoutData());
    }

    [HttpPost("workflow_list")]
    [Authorize]
    public async Task<ActionResult<object>> WorkflowList(
        [FromBody] CozeGetWorkflowListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queryService.ListAsync(
            _tenantProvider.GetTenantId(),
            string.IsNullOrWhiteSpace(request.name) ? null : request.name.Trim(),
            request.page > 0 ? request.page : 1,
            request.size > 0 ? request.size : 20,
            cancellationToken);

        var requestedMode = request.flow_mode;
        var filtered = result.Items
            .Where(item =>
                requestedMode is null
                || requestedMode == 3 && item.Mode == WorkflowMode.ChatFlow
                || requestedMode != 3 && item.Mode == WorkflowMode.Standard)
            .ToArray();

        return Ok(Success(new
        {
            workflow_list = filtered.Select(item => new
            {
                workflow_id = item.Id.ToString(),
                name = item.Name,
                desc = item.Description ?? string.Empty,
                space_id = request.space_id ?? string.Empty,
                status = ToCozeWorkflowStatus(item.Status),
                flow_mode = ToCozeWorkflowMode(item.Mode),
                version = item.LatestVersionNumber.ToString(CultureInfo.InvariantCulture),
                create_time = ToUnixMilliseconds(item.CreatedAt),
                update_time = ToUnixMilliseconds(item.UpdatedAt),
                publish_time = item.PublishedAt is null ? 0 : ToUnixMilliseconds(item.PublishedAt.Value),
                plugin_id = item.Status == WorkflowLifecycleStatus.Published ? item.Id.ToString() : "0"
            }).ToArray(),
            auth_list = Array.Empty<object>(),
            total = filtered.Length
        }));
    }

    [HttpPost("delete_strategy")]
    [Authorize]
    public async Task<ActionResult<object>> GetDeleteStrategy(
        [FromBody] CozeDeleteWorkflowRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request?.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var deps = await _queryService.GetDependenciesAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
        var hasUpstream = deps?.SubWorkflows is { Count: > 0 };
        return Ok(Success(new
        {
            workflow_id = workflowId.ToString(CultureInfo.InvariantCulture),
            strategy = hasUpstream ? 1 : 0,
            referenced_workflow_count = deps?.SubWorkflows.Count ?? 0
        }));
    }

    [HttpPost("batch_delete")]
    [Authorize]
    public async Task<ActionResult<object>> BatchDeleteWorkflow(
        [FromBody] CozeBatchDeleteWorkflowRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var rawIds = request?.workflow_id_list ?? Array.Empty<string>();
        var parsedIds = rawIds
            .Where(id => TryParseWorkflowId(id, out _))
            .Select(id => long.Parse(id, NumberStyles.Integer, CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();

        if (parsedIds.Length == 0)
        {
            return Ok(Success(new { deleted = 0, not_found_workflow_ids = rawIds }));
        }

        var deleted = 0;
        foreach (var workflowId in parsedIds)
        {
            var detail = await _queryService.GetAsync(tenantId, workflowId, cancellationToken);
            if (detail is null)
            {
                continue;
            }

            await _commandService.DeleteAsync(tenantId, workflowId, cancellationToken);
            deleted++;
        }

        return Ok(Success(new
        {
            deleted,
            not_found_workflow_ids = rawIds.Except(parsedIds.Select(x => x.ToString(CultureInfo.InvariantCulture))).ToArray()
        }));
    }

    [HttpPost("workflow_detail")]
    [Authorize]
    public async Task<ActionResult<object>> GetWorkflowDetail(
        [FromBody] CozeGetWorkflowDetailRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<object>();
        foreach (var workflowIdRaw in request.workflow_ids ?? Array.Empty<string>())
        {
            if (!TryParseWorkflowId(workflowIdRaw, out var workflowId))
            {
                continue;
            }

            var detail = await _queryService.GetAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
            if (detail is null)
            {
                continue;
            }

            results.Add(new
            {
                workflow_id = detail.Id.ToString(),
                space_id = request.space_id ?? string.Empty,
                name = detail.Name,
                desc = detail.Description,
                icon = string.Empty,
                inputs = (object?)null,
                outputs = (object?)null,
                version = detail.LatestVersionNumber.ToString(CultureInfo.InvariantCulture),
                create_time = ToUnixMilliseconds(detail.CreatedAt),
                update_time = ToUnixMilliseconds(detail.UpdatedAt),
                project_id = string.Empty,
                end_type = 0,
                icon_uri = string.Empty,
                flow_mode = ToCozeWorkflowMode(detail.Mode),
                output_nodes = Array.Empty<object>()
            });
        }

        return Ok(Success(results));
    }

    [HttpPost("workflow_detail_info")]
    [Authorize]
    public async Task<ActionResult<object>> GetWorkflowDetailInfo(
        [FromBody] CozeWorkflowDetailInfoRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var detail = await _queryService.GetAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
        if (detail is null)
        {
            return Ok(Fail("workflow not found"));
        }

        return Ok(Success(new
        {
            workflow = new
            {
                workflow_id = detail.Id.ToString(),
                name = detail.Name,
                desc = detail.Description,
                space_id = request.space_id ?? string.Empty,
                flow_mode = ToCozeWorkflowMode(detail.Mode),
                status = ToCozeWorkflowStatus(detail.Status),
                schema_json = detail.SchemaJson,
                workflow_version = detail.LatestVersionNumber.ToString(CultureInfo.InvariantCulture)
            },
            operation_info = new
            {
                operator_id = _currentUserAccessor.GetCurrentUserOrThrow().UserId.ToString(CultureInfo.InvariantCulture),
                update_time = ToUnixMilliseconds(detail.UpdatedAt)
            }
        }));
    }

    [HttpPost("update_meta")]
    [Authorize]
    public async Task<ActionResult<object>> UpdateWorkflowMeta(
        [FromBody] CozeUpdateWorkflowMetaRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var name = string.IsNullOrWhiteSpace(request.name) ? "未命名工作流" : request.name.Trim();
        await _commandService.UpdateMetaAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            new CozeWorkflowUpdateMetaCommand(name, request.desc),
            cancellationToken);

        return Ok(SuccessWithoutData());
    }

    [HttpPost("node_panel_search")]
    [Authorize]
    public async Task<ActionResult<object>> NodePanelSearch(
        [FromBody] CozeNodePanelSearchRequest? request,
        CancellationToken cancellationToken)
    {
        var keyword = request?.search_key?.Trim();
        var pageIndex = ResolvePageIndex(request?.page_or_cursor);
        var pageSize = request?.page_size is > 0 ? request.page_size!.Value : 20;

        var templates = await _queryService.SearchNodeTemplatesAsync(keyword, null, pageIndex, pageSize, cancellationToken);
        var nodeTypes = await _queryService.GetNodeTypesAsync(cancellationToken);
        var metadataMap = nodeTypes.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

        var workflowList = templates.Select(item =>
        {
            metadataMap.TryGetValue(item.Key, out var metadata);
            var nodeTypeCode = ToCozeNodeTypeCode(item.Key);
            return new
            {
                workflow_id = nodeTypeCode,
                name = item.Name,
                desc = metadata?.Description ?? string.Empty,
                url = metadata?.UiMeta?.Icon ?? string.Empty,
                plugin_id = "0",
                space_id = request?.space_id ?? string.Empty,
                flow_mode = 0,
                schema_type = 0,
                node_type = nodeTypeCode,
                category = item.Category
            };
        }).ToArray();

        return Ok(Success(new
        {
            resource_workflow = new
            {
                workflow_list = workflowList,
                next_page_or_cursor = (pageIndex + 1).ToString(CultureInfo.InvariantCulture),
                has_more = workflowList.Length >= pageSize
            },
            project_workflow = new { workflow_list = Array.Empty<object>(), next_page_or_cursor = string.Empty, has_more = false }
        }));
    }

    [HttpPost("version_list")]
    [Authorize]
    public async Task<ActionResult<object>> VersionHistoryList(
        [FromBody] CozeVersionHistoryListRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var versions = await _queryService.ListVersionsAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
        var page = request.page > 0 ? request.page : 1;
        var size = request.size > 0 ? request.size : versions.Count;
        var paged = versions.Skip(Math.Max(0, (page - 1) * size)).Take(size).ToArray();

        return Ok(Success(new
        {
            version_list = paged.Select(item => new
            {
                id = item.Id.ToString(),
                version = item.VersionNumber.ToString(CultureInfo.InvariantCulture),
                workflow_id = item.WorkflowId.ToString(CultureInfo.InvariantCulture),
                publish_time = ToUnixMilliseconds(item.PublishedAt),
                change_log = item.ChangeLog ?? string.Empty
            }).ToArray(),
            total = versions.Count
        }));
    }

    [HttpPost("revert")]
    [Authorize]
    public ActionResult<object> RevertDraft([FromBody] CozeRevertDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.workflow_id))
        {
            return Ok(Fail("workflow_id is required"));
        }

        return Ok(Success(new
        {
            workflow_id = request.workflow_id,
            reverted = true
        }));
    }

    [HttpPost("gray_feature")]
    [Authorize]
    public ActionResult<object> GetWorkflowGrayFeature()
    {
        return Ok(Success(new
        {
            enabled = true,
            features = new
            {
                workflow_v2 = true,
                selector_prune = true,
                loop_control = true
            }
        }));
    }

    [HttpPost("history_schema")]
    [Authorize]
    public async Task<ActionResult<object>> GetHistorySchema(
        [FromBody] CozeGetHistorySchemaRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request?.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        long? executionId = null;
        if (TryParseExecutionId(request?.execute_id, out var parsedExecutionId))
        {
            executionId = parsedExecutionId;
        }

        var snapshot = await _queryService.GetHistorySchemaAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            request?.commit_id,
            executionId,
            cancellationToken);

        if (snapshot is null)
        {
            return Ok(Fail("workflow not found"));
        }

        return Ok(Success(new
        {
            name = snapshot.Name,
            describe = snapshot.Description ?? string.Empty,
            url = string.Empty,
            schema = snapshot.SchemaJson,
            flow_mode = 0,
            workflow_id = snapshot.WorkflowId,
            commit_id = snapshot.CommitId ?? string.Empty,
            workflow_version = snapshot.CommitId ?? string.Empty,
            project_version = string.Empty,
            project_id = string.Empty,
            execute_id = request?.execute_id ?? string.Empty,
            sub_execute_id = request?.sub_execute_id ?? string.Empty,
            log_id = request?.log_id ?? string.Empty
        }));
    }

    [HttpGet("get_node_execute_history")]
    [Authorize]
    public async Task<ActionResult<object>> GetNodeExecuteHistory(
        [FromQuery] CozeGetNodeExecuteHistoryRequest request,
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

        long? executionId = null;
        if (TryParseExecutionId(request.execute_id, out var parsedExecutionId))
        {
            executionId = parsedExecutionId;
        }

        var snapshot = await _queryService.GetNodeExecuteHistoryAsync(
            _tenantProvider.GetTenantId(),
            workflowId,
            executionId,
            request.node_id,
            cancellationToken);

        if (snapshot is null)
        {
            return Ok(Success(new
            {
                nodeId = request.node_id,
                NodeType = request.node_type ?? string.Empty,
                NodeName = request.node_id,
                nodeStatus = 1,
                input = (string?)null,
                output = (string?)null,
                extra = (string?)null,
                errorInfo = (string?)null,
                errorLevel = (string?)null,
                executeId = request.execute_id ?? string.Empty,
                isBatch = request.is_batch ?? false,
                batch = (string?)null,
                index = request.batch_index ?? 0
            }));
        }

        var extra = JsonSerializer.Serialize(new
        {
            input = snapshot.InputJson,
            output = snapshot.OutputJson,
            variables = snapshot.ContextVariablesJson
        }, JsonOptions);

        return Ok(Success(new
        {
            nodeId = snapshot.NodeKey,
            NodeType = snapshot.NodeType,
            NodeName = snapshot.NodeKey,
            nodeStatus = ToNodeExeStatusCode(snapshot.Status),
            input = snapshot.InputJson,
            output = snapshot.OutputJson,
            extra,
            errorInfo = snapshot.ErrorMessage,
            errorLevel = string.IsNullOrEmpty(snapshot.ErrorMessage) ? null : "error",
            executeId = snapshot.ExecutionId,
            isBatch = request.is_batch ?? false,
            batch = (string?)null,
            index = request.batch_index ?? 0,
            nodeExeCost = snapshot.DurationMs is null ? null : $"{snapshot.DurationMs}ms"
        }));
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
            new CozeWorkflowRunCommand(inputsJson, "draft"),
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

        await _executionService.ResumeAsync(_tenantProvider.GetTenantId(), executionId, request.data, cancellationToken);
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
            new CozeWorkflowNodeDebugCommand(
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

    [HttpPost("dependency_tree")]
    [HttpPost("/api/workflow_api/dependency_tree")]
    [Authorize]
    public async Task<ActionResult<object>> DependencyTree(
        [FromBody] CozeDependencyTreeRequest request,
        CancellationToken cancellationToken)
    {
        var workflowIdRaw = request.type == 2
            ? request.project_info?.workflow_id
            : request.library_info?.workflow_id;
        if (!TryParseWorkflowId(workflowIdRaw, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        _ = await _queryService.GetDependenciesAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);

        var rootId = workflowId.ToString(CultureInfo.InvariantCulture);
        return Ok(Success(new
        {
            root_id = rootId,
            version = string.Empty,
            node_list = new object[]
            {
                new
                {
                    id = rootId,
                    name = string.Empty,
                    icon = string.Empty,
                    is_product = false,
                    is_root = true,
                    is_library = request.type != 2,
                    with_version = false,
                    workflow_version = string.Empty,
                    dependency = new
                    {
                        start_id = rootId,
                        sub_workflow_ids = Array.Empty<string>(),
                        plugin_ids = Array.Empty<string>(),
                        tools_id_map = new Dictionary<string, string[]>(),
                        knowledge_list = Array.Empty<object>(),
                        model_ids = Array.Empty<string>(),
                        variable_names = Array.Empty<string>(),
                        table_list = Array.Empty<object>(),
                        voice_ids = Array.Empty<string>(),
                        workflow_version = Array.Empty<object>(),
                        plugin_version = Array.Empty<object>()
                    },
                    commit_id = string.Empty,
                    fdl_commit_id = string.Empty,
                    flowlang_release_id = string.Empty,
                    is_chatflow = false
                }
            },
            edge_list = Array.Empty<object>()
        }));
    }

    [HttpPost("workflow_references")]
    [HttpPost("/api/workflow_api/workflow_references")]
    [Authorize]
    public async Task<ActionResult<object>> GetWorkflowReferences(
        [FromBody] CozeWorkflowReferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var dependencies = await _queryService.GetDependenciesAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);

        return Ok(Success(new
        {
            workflow_list = (dependencies?.SubWorkflows ?? Array.Empty<DagWorkflowDependencyItemDto>())
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
        var result = await _queryService.ListPublishedAsync(
            _tenantProvider.GetTenantId(),
            request?.name,
            request?.page > 0 ? request.page!.Value : 1,
            request?.size > 0 ? request.size!.Value : 20,
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

    [HttpPost("old_validate")]
    [Authorize]
    public ActionResult<object> ValidateSchema([FromBody] CozeValidateSchemaRequest request)
    {
        var compileResult = _planCompiler.Compile(request.schema);
        var result = !compileResult.IsSuccess || compileResult.Canvas is null
            ? new CanvasValidationResult(false, compileResult.Errors)
            : _canvasValidator.ValidateCanvas(JsonSerializer.Serialize(compileResult.Canvas));

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
            }).ToArray()));
    }

    [HttpPost("validate_tree")]
    [Authorize]
    public async Task<ActionResult<object>> ValidateTree(
        [FromBody] CozeValidateTreeRequest? request,
        CancellationToken cancellationToken)
    {
        var schema = request?.schema;
        var workflowIdRaw = request?.workflow_id;
        var hasWorkflowId = TryParseWorkflowId(workflowIdRaw, out var workflowId);

        if (string.IsNullOrWhiteSpace(schema) && hasWorkflowId)
        {
            var detail = await _queryService.GetAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
            schema = detail?.SchemaJson;
        }

        if (string.IsNullOrWhiteSpace(schema))
        {
            return Ok(Success(Array.Empty<object>()));
        }

        var compileResult = _planCompiler.Compile(schema);
        var result = !compileResult.IsSuccess || compileResult.Canvas is null
            ? new CanvasValidationResult(false, compileResult.Errors)
            : _canvasValidator.ValidateCanvas(JsonSerializer.Serialize(compileResult.Canvas));

        var errors = (result.Errors ?? Array.Empty<CanvasValidationIssue>())
            .Select(error => new
            {
                node_error = error.NodeKey is null ? null : new { node_id = error.NodeKey },
                path_error = error.SourcePort is null && error.TargetPort is null
                    ? null
                    : new
                    {
                        start = error.SourcePort,
                        end = error.TargetPort,
                        path = error.NodeKey is null ? Array.Empty<string>() : new[] { error.NodeKey }
                    },
                message = error.Message,
                type = TryParseCozeValidateErrorType(error.Code)
            })
            .ToArray();

        return Ok(Success(new object[]
        {
            new
            {
                workflow_id = hasWorkflowId ? workflowId.ToString(CultureInfo.InvariantCulture) : (workflowIdRaw ?? string.Empty),
                name = string.Empty,
                errors
            }
        }));
    }

    [HttpPost("get_trace")]
    [Authorize]
    public async Task<ActionResult<object>> GetTraceSdk(
        [FromBody] CozeGetTraceRequest request,
        CancellationToken cancellationToken)
    {
        var executionIdRaw = request.log_id ?? request.execute_id;
        if (!TryParseExecutionId(executionIdRaw, out var executionId))
        {
            return Ok(Success(new { spans = Array.Empty<object>(), header = new { } }));
        }

        var trace = await _queryService.GetRunTraceAsync(_tenantProvider.GetTenantId(), executionId, cancellationToken);
        if (trace is null)
        {
            return Ok(Success(new { spans = Array.Empty<object>(), header = new { } }));
        }

        var steps = (trace.Steps ?? Array.Empty<DagWorkflowStepResultDto>()).ToArray();
        var spans = new List<object>
        {
            new
            {
                trace_id = trace.ExecutionId,
                log_id = trace.ExecutionId,
                span_id = trace.ExecutionId,
                parent_id = (string?)null,
                type = "Workflow",
                name = "workflow_run",
                alias_name = "Workflow",
                duration = trace.DurationMs ?? 0,
                start_time = trace.StartedAt is null ? 0 : ToUnixMilliseconds(trace.StartedAt.Value),
                status_code = ToCozeExecutionStatus(trace.Status),
                status = trace.Status.ToString(),
                tags = Array.Empty<object>(),
                summary = new { tags = Array.Empty<object>() },
                input = new { type = 1, content = string.Empty },
                output = new { type = 1, content = string.Empty },
                extra = new { input = (string?)null, output = (string?)null, variables = (string?)null, error = (string?)null },
                is_entry = true,
                is_key_span = true,
                product_line = "atlas-workflow"
            }
        };

        spans.AddRange(steps.Select(step => new
        {
            trace_id = trace.ExecutionId,
            log_id = trace.ExecutionId,
            span_id = $"{trace.ExecutionId}:{step.NodeKey}",
            parent_id = trace.ExecutionId,
            type = step.NodeType.ToString(),
            name = step.NodeKey,
            alias_name = step.NodeKey,
            duration = step.DurationMs ?? 0,
            start_time = step.StartedAt is null ? 0 : ToUnixMilliseconds(step.StartedAt.Value),
            status_code = ToCozeExecutionStatus(step.Status),
            status = step.Status.ToString(),
            tags = Array.Empty<object>(),
            summary = new { tags = Array.Empty<object>() },
            input = new { type = 1, content = step.Inputs is null ? string.Empty : JsonSerializer.Serialize(step.Inputs, JsonOptions) },
            output = new { type = 1, content = step.Outputs is null ? string.Empty : JsonSerializer.Serialize(step.Outputs, JsonOptions) },
            extra = new
            {
                input = step.Inputs is null ? null : JsonSerializer.Serialize(step.Inputs, JsonOptions),
                output = step.Outputs is null ? null : JsonSerializer.Serialize(step.Outputs, JsonOptions),
                variables = (string?)null,
                error = step.ErrorMessage
            },
            is_entry = false,
            is_key_span = step.NodeType is WorkflowNodeType.Llm or WorkflowNodeType.SubWorkflow,
            product_line = "atlas-workflow"
        }));

        return Ok(Success(new
        {
            spans = spans.ToArray(),
            header = new
            {
                duration = trace.DurationMs ?? 0,
                start_time = trace.StartedAt is null ? 0 : ToUnixMilliseconds(trace.StartedAt.Value),
                status_code = ToCozeExecutionStatus(trace.Status),
                status = trace.Status.ToString(),
                execution_id = trace.ExecutionId,
                tokens = 0,
                tags = Array.Empty<object>()
            }
        }));
    }

    [HttpPost("list_spans")]
    [Authorize]
    public ActionResult<object> ListRootSpans([FromBody] CozeListRootSpansRequest request)
    {
        return Ok(Success(new
        {
            spans = Array.Empty<object>(),
            total = 0,
            request = new { workflow_id = request.workflow_id }
        }));
    }

    [HttpPost("behavior_auth")]
    [Authorize]
    public ActionResult<object> UserBehaviorAuth()
    {
        return Ok(Success(new { allowed = true }));
    }

    [HttpPost("open_collaborator")]
    [Authorize]
    public async Task<ActionResult<object>> OpenCollaborator([FromBody] CozeGetCanvasInfoRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        await _collaboratorService.OpenAsync(_tenantProvider.GetTenantId(), workflowId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return Ok(Success(new { success = true }));
    }

    [HttpPost("close_collaborator")]
    [Authorize]
    public async Task<ActionResult<object>> CloseCollaborator([FromBody] CozeGetCanvasInfoRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        await _collaboratorService.CloseAsync(_tenantProvider.GetTenantId(), workflowId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return Ok(Success(new { success = true }));
    }

    [HttpPost("get_trigger")]
    [Authorize]
    public async Task<ActionResult<object>> GetTrigger([FromBody] CozeDeleteWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var triggers = await _triggerService.ListTriggersAsync(_tenantProvider.GetTenantId(), workflowId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        var trigger = triggers.FirstOrDefault();
        return Ok(Success(new
        {
            trigger = trigger is null ? null : new
            {
                trigger_id = trigger.Id,
                workflow_id = trigger.WorkflowId,
                name = trigger.Name,
                event_type = trigger.EventType,
                enabled = trigger.Enabled
            }
        }));
    }

    [HttpPost("list_triggers")]
    [Authorize]
    public async Task<ActionResult<object>> ListTriggers([FromBody] CozeDeleteWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var triggers = await _triggerService.ListTriggersAsync(_tenantProvider.GetTenantId(), workflowId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return Ok(Success(new
        {
            list = triggers.Select(trigger => new
            {
                trigger_id = trigger.Id,
                workflow_id = trigger.WorkflowId,
                name = trigger.Name,
                event_type = trigger.EventType,
                enabled = trigger.Enabled
            }).ToArray(),
            total = triggers.Count
        }));
    }

    [HttpPost("save_trigger")]
    [Authorize]
    public async Task<ActionResult<object>> SaveTrigger([FromBody] CozeSaveTriggerRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var triggerId = await _triggerService.SaveAsync(
            _tenantProvider.GetTenantId(),
            workflowId.ToString(CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(request.name) ? "trigger" : request.name!,
            string.IsNullOrWhiteSpace(request.event_type) ? "manual" : request.event_type!,
            cancellationToken);

        return Ok(Success(new { trigger_id = triggerId, success = true }));
    }

    [HttpPost("delete_trigger")]
    [Authorize]
    public ActionResult<object> DeleteTrigger()
    {
        return Ok(Success(new { success = true }));
    }

    [HttpPost("testrun_trigger")]
    [Authorize]
    public async Task<ActionResult<object>> TestRunTrigger([FromBody] CozeTestRunTriggerRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var ok = await _triggerService.TestRunAsync(
            _tenantProvider.GetTenantId(),
            workflowId.ToString(CultureInfo.InvariantCulture),
            request.trigger_id ?? string.Empty,
            cancellationToken);

        return Ok(Success(new { success = ok }));
    }

    [HttpPost("chat_flow_role/get")]
    [Authorize]
    public async Task<ActionResult<object>> GetChatFlowRole([FromBody] CozeGetChatFlowRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.role_id))
        {
            return Ok(Success(new { role = (object?)null }));
        }

        var role = await _chatFlowRoleService.GetAsync(_tenantProvider.GetTenantId(), request.role_id, cancellationToken);
        return Ok(Success(new
        {
            role = role is null ? null : new
            {
                role_id = role.Id,
                workflow_id = role.WorkflowId,
                name = role.Name,
                description = role.Description,
                avatar_uri = role.AvatarUri
            }
        }));
    }

    [HttpPost("chat_flow_role/create")]
    [Authorize]
    public async Task<ActionResult<object>> CreateChatFlowRole([FromBody] CozeCreateChatFlowRoleRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request.workflow_id, out var workflowId))
        {
            return Ok(Fail("workflow_id is required"));
        }

        var roleId = await _chatFlowRoleService.SaveAsync(
            _tenantProvider.GetTenantId(),
            workflowId.ToString(CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(request.name) ? "role" : request.name!,
            request.description ?? string.Empty,
            request.avatar_uri,
            cancellationToken);

        return Ok(Success(new { role_id = roleId }));
    }

    private static object Success(object data)
        => new { code = 0, msg = "success", data };

    private static object SuccessWithoutData()
        => new { code = 0, msg = "success", data = new { } };

    private static object Fail(string message)
        => new { code = -1, msg = message, data = new { } };

    private static bool TryParseWorkflowId(string? raw, out long workflowId)
        => long.TryParse(raw, out workflowId);

    private static bool TryParseExecutionId(string? raw, out long executionId)
        => long.TryParse(raw, out executionId);

    private static bool TryParsePositiveId(string? raw, out long value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(raw)
               && long.TryParse(raw.Trim(), out value)
               && value > 0;
    }

    private static long ToUnixMilliseconds(DateTime value)
        => new DateTimeOffset(value).ToUnixTimeMilliseconds();

    private static int ToCozeWorkflowMode(WorkflowMode mode)
        => mode == WorkflowMode.ChatFlow ? 3 : 0;

    private static int ToCozeWorkflowStatus(WorkflowLifecycleStatus status)
        => status == WorkflowLifecycleStatus.Published ? 3 : 2;

    private static int ToCozeExecutionStatus(ExecutionStatus status)
        => status switch
        {
            ExecutionStatus.Running => 1,
            ExecutionStatus.Completed => 2,
            ExecutionStatus.Failed => 3,
            ExecutionStatus.Cancelled or ExecutionStatus.Interrupted => 4,
            _ => 1
        };

    private static int ToNodeExeStatusCode(ExecutionStatus status)
        => status switch
        {
            ExecutionStatus.Running => 2,
            ExecutionStatus.Completed => 3,
            ExecutionStatus.Failed or ExecutionStatus.Blocked => 4,
            ExecutionStatus.Skipped or ExecutionStatus.Cancelled or ExecutionStatus.Interrupted => 4,
            _ => 1
        };

    private static int TryParseCozeValidateErrorType(string? code)
        => code switch
        {
            "DAG_PATH_CONFLICT" => 1,
            "DAG_PATH_INVALID" => 2,
            _ => 0
        };

    private static HashSet<string>? ResolveAllowedNodeTypes(CozeNodeTemplateListRequest? request)
    {
        var values = (request?.node_types ?? request?.need_types)
            ?.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();

        return values is { Length: > 0 }
            ? new HashSet<string>(values, StringComparer.OrdinalIgnoreCase)
            : null;
    }

    private static string ToCozeNodeTypeCode(string nodeTypeKey)
    {
        if (Enum.TryParse<WorkflowNodeType>(nodeTypeKey, true, out var nodeType))
        {
            return ((int)nodeType).ToString(CultureInfo.InvariantCulture);
        }

        return nodeTypeKey;
    }

    private static int ResolvePageIndex(string? pageOrCursor)
    {
        return int.TryParse(pageOrCursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageIndex) && pageIndex > 0
            ? pageIndex
            : 1;
    }
}

public sealed record CozeSaveTriggerRequest(
    string? workflow_id,
    string? trigger_id,
    string? name,
    string? event_type,
    string? space_id);

public sealed record CozeTestRunTriggerRequest(
    string? workflow_id,
    string? trigger_id,
    string? space_id);

public sealed record CozeGetChatFlowRoleRequest(
    string? role_id,
    string? workflow_id);

public sealed record CozeCreateChatFlowRoleRequest(
    string? workflow_id,
    string? name,
    string? description,
    string? avatar_uri);
