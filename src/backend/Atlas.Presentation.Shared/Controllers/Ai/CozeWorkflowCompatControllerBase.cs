using System.Text.Json;
using System.Net;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.Audit.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text.Json.Serialization;

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

    private readonly ICozeWorkflowCommandService _commandService;
    private readonly ICozeWorkflowQueryService _queryService;
    private readonly ICozeWorkflowExecutionService _executionService;
    private readonly ICanvasValidator _canvasValidator;
    private readonly IWorkspacePortalService _workspacePortalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    protected CozeWorkflowCompatControllerBase(
        ICozeWorkflowCommandService commandService,
        ICozeWorkflowQueryService queryService,
        ICozeWorkflowExecutionService executionService,
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

    [HttpPost("/api/playground_api/space/save")]
    [Authorize]
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

        return Ok(Success(new
        {
            id = matched?.Id ?? targetSpaceId ?? string.Empty,
            check_not_pass = false
        }));
    }

    [HttpPost("/api/bot/get_type_list")]
    [Authorize]
    public async Task<ActionResult<object>> GetTypeList(
        [FromBody] CozeGetTypeListRequest? request,
        CancellationToken cancellationToken)
    {
        var payload = await BuildTypeListPayloadAsync(request, cancellationToken);
        return Ok(Success(payload));
    }

    [HttpGet("/api/marketplace/product/favorite/list")]
    [Authorize]
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

    [HttpGet("/api/marketplace/product/favorite/list.v2")]
    [Authorize]
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

        var saveRequest = new CozeWorkflowSaveDraftCommand(request.schema ?? "{}", request.submit_commit_id);
        await _commandService.SaveDraftAsync(_tenantProvider.GetTenantId(), workflowId, saveRequest, cancellationToken);
        await TryWriteAuditAsync("save_draft", workflowId.ToString(CultureInfo.InvariantCulture), true, cancellationToken);

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
            new CozeWorkflowPublishCommand(request.version_description),
            cancellationToken);
        await TryWriteAuditAsync("publish", workflowId.ToString(CultureInfo.InvariantCulture), true, cancellationToken);

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
        var nodeTypeCodes = nodeTypes
            .Select(item => ToCozeNodeTypeCode(item.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(Success(new
        {
            node_types = nodeTypeCodes,
            sub_workflow_node_types = Array.Empty<string>(),
            nodes_properties = nodeTypes.Select(item => new
            {
                id = ToCozeNodeTypeCode(item.Key),
                type = ToCozeNodeTypeCode(item.Key),
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
        var allowedNodeTypes = ResolveAllowedNodeTypes(request);

        var projectedTemplates = templates
            .Select(item =>
            {
                metadataMap.TryGetValue(item.Key, out var metadata);
                var nodeTypeCode = ToCozeNodeTypeCode(item.Key);
                return new
                {
                    Template = item,
                    Metadata = metadata,
                    NodeTypeCode = nodeTypeCode
                };
            })
            .ToArray();

        var filteredTemplates = projectedTemplates
            .Where(item =>
                allowedNodeTypes is null
                || allowedNodeTypes.Contains(item.NodeTypeCode)
                || allowedNodeTypes.Contains(item.Template.Key))
            .ToArray();

        var categories = filteredTemplates
            .GroupBy(item => item.Metadata?.Category ?? "General", StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                name = group.Key,
                node_type_list = group.Select(item => item.NodeTypeCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                plugin_api_id_list = Array.Empty<string>(),
                plugin_category_id_list = Array.Empty<string>()
            })
            .ToArray();

        return Ok(Success(new
        {
            template_list = filteredTemplates.Select(item => new
            {
                id = item.NodeTypeCode,
                type = 0,
                name = item.Template.Name,
                desc = item.Metadata?.Description ?? string.Empty,
                icon_url = item.Metadata?.UiMeta?.Icon ?? string.Empty,
                support_batch = item.Metadata?.UiMeta?.SupportsBatch == true ? 2 : 1,
                node_type = item.NodeTypeCode,
                color = item.Metadata?.UiMeta?.Color ?? string.Empty,
                commercial_node = false,
                plugin_list = Array.Empty<string>(),
                volc_res_list = Array.Empty<string>()
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

        var compileResult = _commandService is not null
            ? HttpContext.RequestServices.GetRequiredService<ICozeWorkflowPlanCompiler>().Compile(request.schema)
            : new CozeWorkflowCompileResult(false, null, [new CanvasValidationIssue("COZE_COMPILER_UNAVAILABLE", "Coze 编译器不可用。")]);
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
            })
            .ToArray()));
    }

    [HttpPost("validate_tree")]
    [Authorize]
    public async Task<ActionResult<object>> ValidateTree(
        [FromBody] CozeValidateTreeRequest? request,
        CancellationToken cancellationToken)
    {
        var schema = NormalizeIncomingSchema(request?.schema);
        var workflowIdRaw = request?.workflow_id;
        var hasWorkflowId = TryParseWorkflowId(workflowIdRaw, out var workflowId);

        // 没有 schema 但有 workflow_id 时，从草稿读取（与上游 ValidateTree 行为一致）。
        if (string.IsNullOrWhiteSpace(schema) && hasWorkflowId)
        {
            var detail = await _queryService.GetAsync(_tenantProvider.GetTenantId(), workflowId, cancellationToken);
            schema = NormalizeIncomingSchema(detail?.SchemaJson);
        }

        if (string.IsNullOrWhiteSpace(schema))
        {
            return Ok(Success(Array.Empty<object>()));
        }

        var compileResult = HttpContext.RequestServices.GetRequiredService<ICozeWorkflowPlanCompiler>().Compile(schema);
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

        // 上游响应是 Array<ValidateTreeInfo>，每条对应一个工作流。
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

    [HttpPost("node_panel_search")]
    [Authorize]
    public async Task<ActionResult<object>> NodePanelSearch(
        [FromBody] CozeNodePanelSearchRequest? request,
        CancellationToken cancellationToken)
    {
        var keyword = request?.search_key?.Trim();
        var pageIndex = ResolvePageIndex(request?.page_or_cursor);
        var pageSize = request?.page_size is > 0 ? request.page_size!.Value : 20;

        var templates = await _queryService.SearchNodeTemplatesAsync(
            keyword,
            null,
            pageIndex,
            pageSize,
            cancellationToken);
        var nodeTypes = await _queryService.GetNodeTypesAsync(cancellationToken);
        var metadataMap = nodeTypes.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

        // Coze NodePanelSearchData 是按数据源分组返回，但当前 Atlas 仅有节点目录可搜，
        // 因此把命中的节点模板放在 resource_workflow.workflow_list（前端会渲染为可拖拽节点条目）。
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
            project_workflow = new { workflow_list = Array.Empty<object>(), next_page_or_cursor = string.Empty, has_more = false },
            favorite_plugin = new { plugin_list = Array.Empty<object>(), next_page_or_cursor = string.Empty, has_more = false },
            resource_plugin = new { plugin_list = Array.Empty<object>(), next_page_or_cursor = string.Empty, has_more = false },
            project_plugin = new { plugin_list = Array.Empty<object>(), next_page_or_cursor = string.Empty, has_more = false },
            store_plugin = new { plugin_list = Array.Empty<object>(), next_page_or_cursor = string.Empty, has_more = false }
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
                nodeStatus = (int)NodeExeStatusCode.Waiting,
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
        await TryWriteAuditAsync("test_run", workflowId.ToString(CultureInfo.InvariantCulture), true, cancellationToken);

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
            request.data,
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
        await TryWriteAuditAsync("cancel", executionId.ToString(CultureInfo.InvariantCulture), true, cancellationToken);
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
        await TryWriteAuditAsync(
            "node_debug",
            $"{workflowId.ToString(CultureInfo.InvariantCulture)}#{request.node_id}",
            true,
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
        await TryWriteAuditAsync(
            "copy",
            $"{workflowId.ToString(CultureInfo.InvariantCulture)}->{copiedId.ToString(CultureInfo.InvariantCulture)}",
            true,
            cancellationToken);

        return Ok(Success(new
        {
            workflow_id = copiedId.ToString(),
            schema_type = 0
        }));
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
        var creatorId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var workflowId = await _commandService.CreateAsync(
            _tenantProvider.GetTenantId(),
            creatorId,
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
        await TryWriteAuditAsync("delete", workflowId.ToString(CultureInfo.InvariantCulture), true, cancellationToken);
        return Ok(SuccessWithoutData());
    }

    [HttpPost("workflow_list")]
    [Authorize]
    public async Task<ActionResult<object>> GetWorkFlowList(
        [FromBody] CozeGetWorkflowListRequest request,
        CancellationToken cancellationToken)
    {
        var page = request.page > 0 ? request.page : 1;
        var size = request.size > 0 ? request.size : 20;
        var query = request.status == 1
            ? await _queryService.ListPublishedAsync(_tenantProvider.GetTenantId(), request.name, page, size, cancellationToken)
            : await _queryService.ListAsync(_tenantProvider.GetTenantId(), request.name, page, size, cancellationToken);

        var data = query.Items.Select(item => new
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
        }).ToArray();

        return Ok(Success(new
        {
            workflow_list = data,
            auth_list = Array.Empty<object>(),
            total = query.Total
        }));
    }

    [HttpPost("workflow_detail")]
    [Authorize]
    public async Task<ActionResult<object>> GetWorkflowDetail(
        [FromBody] CozeGetWorkflowDetailRequest request,
        CancellationToken cancellationToken)
    {
        var workflowIds = request.workflow_ids ?? Array.Empty<string>();
        var results = new List<object>(workflowIds.Length);
        foreach (var workflowIdRaw in workflowIds)
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
        var skipped = Math.Max(0, (page - 1) * size);
        var paged = versions.Skip(skipped).Take(size).ToArray();

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

    [HttpPost("region_gray")]
    [Authorize]
    public ActionResult<object> RegionGray()
    {
        return Ok(Success(new
        {
            enabled = true,
            region = "default"
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
            .ToArray();

        if (parsedIds.Length == 0)
        {
            return Ok(Success(new { deleted = 0, not_found_workflow_ids = rawIds }));
        }

        // 循环外预查存在性，循环内只调一次仓储 DeleteAsync，符合 dbrun 规则。
        var existingMetas = await _queryService.ListAsync(tenantId, null, 1, parsedIds.Length, cancellationToken);
        var existingIds = existingMetas.Items
            .Where(item => parsedIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToHashSet();

        var notFound = parsedIds
            .Where(id => !existingIds.Contains(id))
            .Select(id => id.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        var deletedCount = 0;
        foreach (var id in existingIds)
        {
            await _commandService.DeleteAsync(tenantId, id, cancellationToken);
            deletedCount++;
        }

        await TryWriteAuditAsync(
            "batch_delete",
            string.Join(",", existingIds.Select(id => id.ToString(CultureInfo.InvariantCulture))),
            true,
            cancellationToken);
        return Ok(Success(new
        {
            deleted = deletedCount,
            not_found_workflow_ids = notFound
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
            // 0=直接删除 / 1=被引用，需提示
            strategy = hasUpstream ? 1 : 0,
            referenced_workflow_count = deps?.SubWorkflows.Count ?? 0
        }));
    }

    [HttpPost("copy_wk_template")]
    [Authorize]
    public async Task<ActionResult<object>> CopyWkTemplateApi(
        [FromBody] CozeCopyWkTemplateRequest? request,
        CancellationToken cancellationToken)
    {
        var rawIds = request?.workflow_ids ?? Array.Empty<string>();
        var parsedIds = rawIds
            .Where(id => TryParseWorkflowId(id, out _))
            .Select(id => long.Parse(id, NumberStyles.Integer, CultureInfo.InvariantCulture))
            .ToArray();

        var tenantId = _tenantProvider.GetTenantId();
        var creatorId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var copyMap = new Dictionary<string, string>(parsedIds.Length);
        foreach (var sourceId in parsedIds)
        {
            var copiedId = await _commandService.CopyAsync(tenantId, creatorId, sourceId, cancellationToken);
            copyMap[sourceId.ToString(CultureInfo.InvariantCulture)] = copiedId.ToString(CultureInfo.InvariantCulture);
        }

        await TryWriteAuditAsync(
            "copy_template",
            string.Join(",", copyMap.Keys),
            true,
            cancellationToken);
        return Ok(Success(new
        {
            copy_workflow_id_map = copyMap
        }));
    }

    [HttpPost("example_workflow_list")]
    [Authorize]
    public ActionResult<object> GetExampleWorkflowList([FromBody] CozeExampleWorkflowListRequest? request)
    {
        // Atlas 不维护"示例工作流"独立目录，返回空集合保证前端模板抽屉不报错。
        return Ok(Success(new
        {
            workflow_list = Array.Empty<object>(),
            total = 0,
            has_more = false,
            page = request?.page ?? 1,
            size = request?.size ?? 10
        }));
    }

    [HttpGet("apiDetail")]
    [Authorize]
    public async Task<ActionResult<object>> GetApiDetail(
        [FromQuery] CozeGetApiDetailRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (TryParsePositiveId(request.api_id, out var apiId) && apiId > 0)
        {
            // Atlas 当前没有插件 API 元数据查询服务，先返回结构占位但保持 strong-typed 字段。
            return Ok(Success(new
            {
                api = new
                {
                    api_id = apiId.ToString(CultureInfo.InvariantCulture),
                    api_name = request.apiName ?? string.Empty,
                    plugin_id = request.pluginID ?? request.plugin_version ?? string.Empty,
                    space_id = request.space_id ?? string.Empty,
                    project_id = request.project_id ?? string.Empty,
                    plugin_version = request.plugin_version ?? string.Empty,
                    plugin_from = request.plugin_from ?? string.Empty
                }
            }));
        }

        return Ok(Success(new
        {
            api = (object?)null,
            workflow_id = request.api_id ?? string.Empty
        }));
    }

    [HttpPost("upload/auth_token")]
    [Authorize]
    public ActionResult<object> GetUploadAuthToken([FromBody] CozeUploadAuthTokenRequest? request)
    {
        // Atlas 文件上传统一走 PlatformHost FileStorage，其授权由 JWT 完成；
        // 这里只为满足 Coze playground 的字段契约返回短期 token + ttl。
        var token = Guid.NewGuid().ToString("N");
        var ttlSeconds = 3600;
        return Ok(Success(new
        {
            auth_token = token,
            access_key_id = token,
            secret_access_key = string.Empty,
            session_token = token,
            current_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            expired_time = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeSeconds(),
            scene = request?.scene ?? string.Empty
        }));
    }

    [HttpPost("sign_image_url")]
    [Authorize]
    public ActionResult<object> SignImageUrl([FromBody] CozeSignImageUrlRequest? request)
    {
        // Atlas 图片资源 URI 与 Coze 不同格式：直接回显 URI 作为可访问 URL，避免阻塞 playground 渲染。
        var uri = request?.uri?.Trim() ?? string.Empty;
        return Ok(Success(new
        {
            url = uri,
            uri,
            scene = request?.Scene ?? string.Empty
        }));
    }

    [HttpPost("list_publish_workflow")]
    [Authorize]
    public async Task<ActionResult<object>> ListPublishWorkflow(
        [FromBody] CozeListPublishWorkflowRequest? request,
        CancellationToken cancellationToken)
    {
        var pageSize = request?.size is > 0 ? request.size!.Value : 20;
        var cursorRaw = request?.cursor_id;
        var pageIndex = ResolvePageIndex(cursorRaw);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.ListPublishedAsync(
            tenantId,
            request?.name,
            pageIndex,
            pageSize,
            cancellationToken);

        var items = result.Items.Select(item => new
        {
            workflow_id = item.Id.ToString(CultureInfo.InvariantCulture),
            name = item.Name,
            desc = item.Description ?? string.Empty,
            owner_id = item.CreatorId.ToString(CultureInfo.InvariantCulture),
            space_id = request?.space_id ?? string.Empty,
            last_publish_time = item.PublishedAt is null ? 0 : ToUnixMilliseconds(item.PublishedAt.Value),
            create_time = ToUnixMilliseconds(item.CreatedAt),
            update_time = ToUnixMilliseconds(item.UpdatedAt),
            total_token = 0
        }).ToArray();

        var consumed = (long)pageIndex * pageSize;
        return Ok(Success(new
        {
            workflow_list = items,
            cursor_id = (pageIndex + 1).ToString(CultureInfo.InvariantCulture),
            has_more = consumed < result.Total,
            total = result.Total
        }));
    }

    [HttpPost("get_async_sub_process")]
    [Authorize]
    public async Task<ActionResult<object>> GetNodeAsyncExecuteHistory(
        [FromBody] CozeGetNodeAsyncExecuteHistoryRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkflowId(request?.parent_workflow_id, out var parentWorkflowId)
            || string.IsNullOrWhiteSpace(request?.parent_node_id))
        {
            return Ok(Success(Array.Empty<object>()));
        }

        // 通过最近的执行实例反查父节点对应的子执行；当前 Atlas 不保留显式子执行索引，
        // 仅在 NodeExecution 中按 nodeKey 关联。
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetAsync(tenantId, parentWorkflowId, cancellationToken);
        if (detail is null)
        {
            return Ok(Success(Array.Empty<object>()));
        }

        return Ok(Success(Array.Empty<object>()));
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
            return Ok(Success(new
            {
                spans = Array.Empty<object>(),
                header = new { }
            }));
        }

        var trace = await _queryService.GetRunTraceAsync(_tenantProvider.GetTenantId(), executionId, cancellationToken);
        if (trace is null)
        {
            return Ok(Success(new
            {
                spans = Array.Empty<object>(),
                header = new { }
            }));
        }

        var steps = (trace.Steps ?? Array.Empty<DagWorkflowStepResultDto>()).ToArray();
        var spans = new List<object>(steps.Length + 1)
        {
            BuildRootTraceSpan(trace)
        };

        var orderedSteps = steps
            .Select((step, index) => new { Step = step, Index = index })
            .OrderBy(item => item.Step.StartedAt ?? DateTime.MaxValue)
            .ToArray();

        var contextSnapshot = new SortedDictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in orderedSteps)
        {
            var step = item.Step;
            var stepInputs = step.Inputs;
            var stepOutputs = step.Outputs;
            var inputJson = stepInputs is null ? null : JsonSerializer.Serialize(stepInputs, JsonOptions);
            var outputJson = stepOutputs is null ? null : JsonSerializer.Serialize(stepOutputs, JsonOptions);
            var contextSnapshotJson = contextSnapshot.Count == 0
                ? null
                : JsonSerializer.Serialize(contextSnapshot, JsonOptions);

            spans.Add(new
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
                input = new { type = 1, content = inputJson ?? string.Empty },
                output = new { type = 1, content = outputJson ?? string.Empty },
                extra = new
                {
                    input = inputJson,
                    output = outputJson,
                    variables = contextSnapshotJson,
                    error = step.ErrorMessage
                },
                is_entry = false,
                is_key_span = step.NodeType is WorkflowNodeType.Llm or WorkflowNodeType.SubWorkflow,
                product_line = "atlas-workflow"
            });

            if (stepOutputs is not null)
            {
                foreach (var (key, value) in stepOutputs)
                {
                    contextSnapshot[$"{step.NodeKey}.{key}"] = value;
                }
            }
        }

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

    private object BuildRootTraceSpan(DagWorkflowRunTraceDto trace)
    {
        return new
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
            extra = new
            {
                input = (string?)null,
                output = (string?)null,
                variables = (string?)null,
                error = (string?)null
            },
            is_entry = true,
            is_key_span = true,
            product_line = "atlas-workflow"
        };
    }

    [HttpPost("list_spans")]
    [Authorize]
    public ActionResult<object> ListRootSpans([FromBody] CozeListRootSpansRequest request)
    {
        return Ok(Success(new
        {
            spans = Array.Empty<object>(),
            total = 0,
            request = new
            {
                workflow_id = request.workflow_id
            }
        }));
    }

    [HttpPost("conflict_from")]
    [Authorize]
    public ActionResult<object> GetConflictFromContent([FromBody] CozeConflictFromContentRequest request)
    {
        return Ok(Success(new
        {
            workflow_id = request.workflow_id,
            has_conflict = false,
            conflicts = Array.Empty<object>()
        }));
    }

    [HttpPost("/api/op_workflow/{**path}")]
    [Authorize]
    public ActionResult<object> OpWorkflowPostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildWorkflowFallbackData(path)));
    }

    [HttpGet("/api/op_workflow/{**path}")]
    [Authorize]
    public ActionResult<object> OpWorkflowGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildWorkflowFallbackData(path)));
    }

    [HttpPost("/api/playground_api/get_type_list")]
    [Authorize]
    public async Task<ActionResult<object>> GetPlaygroundTypeList(
        [FromBody] CozeGetTypeListRequest? request,
        CancellationToken cancellationToken)
    {
        var payload = await BuildTypeListPayloadAsync(request, cancellationToken);
        return Ok(Success(payload));
    }

    [HttpPost("/api/playground_api/{**path}")]
    [Authorize]
    public ActionResult<object> PlaygroundPostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildPlaygroundFallbackData(path)));
    }

    [HttpGet("/api/playground_api/{**path}")]
    [Authorize]
    public ActionResult<object> PlaygroundGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildPlaygroundFallbackData(path)));
    }

    [HttpPost("/api/bot/upload_file")]
    [Authorize]
    public ActionResult<object> UploadBotFile()
    {
        return Ok(Success(new
        {
            file_id = Guid.NewGuid().ToString("N"),
            file_url = string.Empty
        }));
    }

    [HttpPost("/api/space/list")]
    [Authorize]
    public async Task<ActionResult<object>> DeveloperSpaceList(
        [FromBody] CozeGetSpaceListRequest? request,
        CancellationToken cancellationToken)
    {
        return await GetSpaceList(request, cancellationToken);
    }

    [HttpPost("/api/draftbot/get_draft_bot_list")]
    [Authorize]
    public async Task<ActionResult<object>> GetDraftBotList(
        [FromBody] CozeGetDraftBotListRequest? request,
        CancellationToken cancellationToken)
    {
        var teamAgentService = HttpContext.RequestServices.GetService<ITeamAgentService>();
        if (teamAgentService is null)
        {
            return Ok(Success(new
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
            create_time = ToUnixMilliseconds(item.CreatedAt),
            update_time = ToUnixMilliseconds(item.UpdatedAt),
            publish_time = 0,
            publish_status = item.Status.ToString().ToLowerInvariant(),
            agent_type = string.IsNullOrWhiteSpace(item.AgentType) ? "team_agent" : item.AgentType
        }).ToArray();

        return Ok(Success(new
        {
            total = paged.Total,
            list,
            has_more = pageIndex * pageSize < paged.Total
        }));
    }

    [HttpPost("/api/draftbot/get_display_info")]
    [Authorize]
    public async Task<ActionResult<object>> GetDraftBotDisplayInfo(
        [FromBody] CozeGetDraftBotDisplayInfoRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(request?.bot_id, out var botId))
        {
            return Ok(Fail("bot_id is required"));
        }

        var teamAgentService = HttpContext.RequestServices.GetService<ITeamAgentService>();
        if (teamAgentService is null)
        {
            return Ok(Success(new
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
            return Ok(Fail("bot not found"));
        }

        return Ok(Success(new
        {
            bot_id = detail.Id.ToString(CultureInfo.InvariantCulture),
            name = detail.Name,
            description = detail.Description ?? string.Empty,
            icon_url = string.Empty,
            agent_type = detail.AgentType,
            publish_status = detail.Status.ToString().ToLowerInvariant(),
            create_time = ToUnixMilliseconds(detail.CreatedAt),
            update_time = ToUnixMilliseconds(detail.UpdatedAt),
            schema_config_json = detail.SchemaConfigJson ?? string.Empty
        }));
    }

    [HttpPost("/api/playground_api/space/info")]
    [Authorize]
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
            return Ok(Success(new { data = (object?)null }));
        }

        return Ok(Success(new
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

    [HttpPost("/api/bot/{**path}")]
    [Authorize]
    public ActionResult<object> BotPostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpGet("/api/bot/{**path}")]
    [Authorize]
    public ActionResult<object> BotGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpPost("/api/space/{**path}")]
    [Authorize]
    public ActionResult<object> SpacePostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpGet("/api/space/{**path}")]
    [Authorize]
    public ActionResult<object> SpaceGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpPost("/api/draftbot/{**path}")]
    [Authorize]
    public ActionResult<object> DraftBotPostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpGet("/api/draftbot/{**path}")]
    [Authorize]
    public ActionResult<object> DraftBotGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpPost("/api/devops/{**path}")]
    [HttpGet("/api/devops/{**path}")]
    [Authorize]
    public ActionResult<object> DevOpsFallback([FromRoute] string? path)
    {
        var normalizedPath = NormalizeFallbackPath(path);
        return Ok(Success(normalizedPath switch
        {
            "debugger/v1/coze/testcase/casedata/mget" => new { cases = Array.Empty<object>(), hasNext = false, nextToken = string.Empty },
            "debugger/v1/coze/testcase/casedata/check" => new { isPass = true, failReason = string.Empty, failCode = 0 },
            "debugger/v1/coze/testcase/casedata/save" => new { caseDetail = (object?)null },
            _ => new { success = true }
        }));
    }

    [HttpPost("/api/plugin_api/{**path}")]
    [HttpGet("/api/plugin_api/{**path}")]
    [Authorize]
    public ActionResult<object> PluginApiFallback([FromRoute] string? path)
    {
        var normalizedPath = NormalizeFallbackPath(path);
        return Ok(Success(normalizedPath switch
        {
            "get_plugin_pricing_rules_by_workflow_id" => new
            {
                workflow_id = string.Empty,
                pricing_rules = Array.Empty<object>()
            },
            _ => new { success = true }
        }));
    }

    [HttpPost("/api/playground/{**path}")]
    [Authorize]
    public ActionResult<object> DeveloperPlaygroundPostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpGet("/api/playground/{**path}")]
    [Authorize]
    public ActionResult<object> DeveloperPlaygroundGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpPost("/api/workflow/{**path}")]
    [Authorize]
    public ActionResult<object> DeveloperWorkflowPostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpGet("/api/workflow/{**path}")]
    [Authorize]
    public ActionResult<object> DeveloperWorkflowGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpPost("/api/workflowV2/{**path}")]
    [Authorize]
    public ActionResult<object> DeveloperDagWorkflowPostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpGet("/api/workflowV2/{**path}")]
    [Authorize]
    public ActionResult<object> DeveloperDagWorkflowGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildDeveloperFallbackData(path)));
    }

    [HttpPost("/v1/workflow/{**path}")]
    [HttpGet("/v1/workflow/{**path}")]
    [Authorize]
    public ActionResult<object> OpenWorkflowFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildOpenApiFallbackData(path)));
    }

    [HttpPost("/v1/workflows/{**path}")]
    [HttpGet("/v1/workflows/{**path}")]
    [Authorize]
    public ActionResult<object> OpenWorkflowsFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildOpenApiFallbackData(path)));
    }

    [HttpGet("/v1/workspaces")]
    [HttpGet("/api/v1/workspaces")]
    [Authorize]
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
            .Select(MapOpenWorkspace)
            .ToArray();

        return Ok(Success(new
        {
            workspaces = pagedItems,
            total_count = filtered.Length
        }));
    }

    [HttpPost("/v1/workspaces")]
    [HttpPost("/api/v1/workspaces")]
    [Authorize]
    public async Task<ActionResult<object>> OpenCreateSpace(
        [FromBody] CozeOpenCreateSpaceRequest? request,
        CancellationToken cancellationToken)
    {
        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();

        // 1→N 模型：创建工作空间不再需要预先存在的应用实例。
        // Coze 兼容层在工作空间创建后，可由调用方再单独通过
        // POST /api/v1/organizations/{orgId}/workspaces/{id}/app-instances 创建应用实例。
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

        return Ok(Success(new
        {
            id = workspaceId.ToString(CultureInfo.InvariantCulture)
        }));
    }

    [HttpGet("/v1/workspaces/{workspaceId}/members")]
    [HttpGet("/api/v1/workspaces/{workspaceId}/members")]
    [Authorize]
    public async Task<ActionResult<object>> OpenSpaceMemberList(
        [FromRoute] string workspaceId,
        [FromQuery(Name = "page_num")] int? pageNum,
        [FromQuery(Name = "page_size")] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(Fail("workspace_id is invalid"));
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
            .Select(MapOpenSpaceMember)
            .ToArray();

        return Ok(Success(new
        {
            items = pagedItems,
            total_count = members.Count
        }));
    }

    [HttpPost("/v1/workspaces/{workspaceId}/members")]
    [HttpPost("/api/v1/workspaces/{workspaceId}/members")]
    [Authorize]
    public async Task<ActionResult<object>> OpenAddSpaceMember(
        [FromRoute] string workspaceId,
        [FromBody] CozeOpenAddSpaceMemberRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(Fail("workspace_id is invalid"));
        }

        var candidateUserId = request?.Users?
            .Select(item => item.UserId?.Trim())
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (string.IsNullOrWhiteSpace(candidateUserId))
        {
            return Ok(Success(new
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
            return Ok(Success(new
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
                ToWorkspaceRoleCode(target?.RoleType)),
            cancellationToken);

        return Ok(Success(new
        {
            added_success_user_ids = new[] { parsedUserId.ToString(CultureInfo.InvariantCulture) },
            invited_success_user_ids = Array.Empty<string>(),
            not_exist_user_ids = Array.Empty<string>(),
            already_joined_user_ids = Array.Empty<string>(),
            already_invited_user_ids = Array.Empty<string>()
        }));
    }

    [HttpDelete("/v1/workspaces/{workspaceId}/members")]
    [HttpDelete("/api/v1/workspaces/{workspaceId}/members")]
    [Authorize]
    public async Task<ActionResult<object>> OpenRemoveSpaceMember(
        [FromRoute] string workspaceId,
        [FromBody] CozeOpenRemoveSpaceMemberRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(Fail("workspace_id is invalid"));
        }

        var candidateUserId = request?.UserIds?
            .Select(item => item?.Trim())
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (string.IsNullOrWhiteSpace(candidateUserId))
        {
            return Ok(Success(new
            {
                removed_success_user_ids = Array.Empty<string>(),
                not_in_workspace_user_ids = Array.Empty<string>(),
                owner_not_support_remove_user_ids = Array.Empty<string>()
            }));
        }

        if (!TryParsePositiveId(candidateUserId, out var parsedUserId))
        {
            return Ok(Success(new
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

        return Ok(Success(new
        {
            removed_success_user_ids = new[] { parsedUserId.ToString(CultureInfo.InvariantCulture) },
            not_in_workspace_user_ids = Array.Empty<string>(),
            owner_not_support_remove_user_ids = Array.Empty<string>()
        }));
    }

    [HttpPost("/v1/workspaces/{workspaceId}/members/apply")]
    [HttpPost("/api/v1/workspaces/{workspaceId}/members/apply")]
    [Authorize]
    public ActionResult<object> OpenApplyJoinSpace(
        [FromRoute] string workspaceId,
        [FromBody] CozeOpenApplyJoinSpaceRequest? request)
    {
        if (!TryParsePositiveId(workspaceId, out _))
        {
            return Ok(Fail("workspace_id is invalid"));
        }

        var appliedUserIds = request?.UserIds?
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();

        return Ok(Success(new
        {
            applied_success_user_ids = appliedUserIds,
            applied_failed_user_ids = Array.Empty<string>()
        }));
    }

    [HttpPut("/v1/workspaces/{workspaceId}/members/{userId}")]
    [HttpPut("/api/v1/workspaces/{workspaceId}/members/{userId}")]
    [Authorize]
    public async Task<ActionResult<object>> OpenUpdateSpaceMember(
        [FromRoute] string workspaceId,
        [FromRoute] string userId,
        [FromBody] CozeOpenUpdateSpaceMemberRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(Fail("workspace_id is invalid"));
        }

        if (!TryParsePositiveId(userId, out var parsedUserId))
        {
            return Ok(Fail("user_id is invalid"));
        }

        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        await _workspacePortalService.UpdateMemberRoleAsync(
            tenantId,
            parsedWorkspaceId,
            parsedUserId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            new WorkspaceMemberRoleUpdateRequest(ToWorkspaceRoleCode(request?.RoleType)),
            cancellationToken);

        return Ok(SuccessWithoutData());
    }

    [HttpDelete("/v1/workspaces/{workspaceId}")]
    [HttpDelete("/api/v1/workspaces/{workspaceId}")]
    [Authorize]
    public async Task<ActionResult<object>> OpenRemoveSpace(
        [FromRoute] string workspaceId,
        CancellationToken cancellationToken)
    {
        if (!TryParsePositiveId(workspaceId, out var parsedWorkspaceId))
        {
            return Ok(Fail("workspace_id is invalid"));
        }

        var (tenantId, currentUser) = ResolveOpenWorkspaceContext();
        await _workspacePortalService.DeleteWorkspaceAsync(
            tenantId,
            parsedWorkspaceId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        return Ok(SuccessWithoutData());
    }

    [HttpGet("/v1/bots/{botId}")]
    [HttpGet("/api/v1/bots/{botId}")]
    [Authorize]
    public ActionResult<object> OpenGetBotInfo([FromRoute] string botId)
    {
        return Ok(Success(new
        {
            id = botId,
            bot_id = botId,
            name = $"bot-{botId}",
            description = string.Empty,
            icon_url = string.Empty,
            publish_status = "draft"
        }));
    }

    [HttpGet("/v1/bots/{botId}/versions")]
    [HttpGet("/api/v1/bots/{botId}/versions")]
    [Authorize]
    public ActionResult<object> OpenListBotVersions([FromRoute] string botId)
    {
        return Ok(Success(new
        {
            items = Array.Empty<object>(),
            has_more = false
        }));
    }

    [HttpPost("/v1/bots/{botId}/collaboration_mode")]
    [HttpPost("/api/v1/bots/{botId}/collaboration_mode")]
    [Authorize]
    public ActionResult<object> OpenSwitchBotDevelopMode(
        [FromRoute] string botId,
        [FromBody] CozeOpenSwitchBotDevelopModeRequest? request)
    {
        return Ok(SuccessWithoutData());
    }

    [HttpPost("{**path}")]
    [Authorize]
    public ActionResult<object> WorkflowPostFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildWorkflowFallbackData(path)));
    }

    [HttpGet("{**path}")]
    [Authorize]
    public ActionResult<object> WorkflowGetFallback([FromRoute] string? path)
    {
        return Ok(Success(BuildWorkflowFallbackData(path)));
    }

    private async Task<object> BuildTypeListPayloadAsync(CozeGetTypeListRequest? request, CancellationToken cancellationToken)
    {
        var queryService = HttpContext.RequestServices.GetService<IModelConfigQueryService>();
        if (queryService is null)
        {
            return new
            {
                model_list = Array.Empty<object>(),
                voice_list = Array.Empty<object>(),
                raw_model_list = Array.Empty<object>(),
                model_show_family_list = Array.Empty<object>(),
                default_model_id = 0,
                total = 0
            };
        }

        var models = await queryService.GetAllEnabledAsync(
            _tenantProvider.GetTenantId(),
            workspaceId: null,
            cancellationToken: cancellationToken);
        var modelList = models.Select(item => new
        {
            name = item.DefaultModel,
            model_type = item.Id,
            model_name = string.IsNullOrWhiteSpace(item.ModelId) ? item.DefaultModel : item.ModelId,
            endpoint_name = item.ProviderType,
            model_class_name = item.ProviderType,
            model_brief_desc = item.SystemPrompt ?? item.Name,
            model_desc = new[]
            {
                new
                {
                    group_name = item.ProviderType,
                    desc = new[] { item.SystemPrompt ?? item.Name }
                }
            },
            model_params = BuildModelParams(item),
            model_ability = new
            {
                cot_display = item.EnableReasoning,
                function_call = item.EnableTools,
                image_understanding = item.EnableVision,
                support_multi_modal = item.EnableVision
            },
            model_status_details = new
            {
                is_free_model = true
            }
        }).ToArray();

        return new
        {
            model_list = modelList,
            voice_list = Array.Empty<object>(),
            raw_model_list = modelList,
            model_show_family_list = Array.Empty<object>(),
            default_model_id = modelList.FirstOrDefault()?.model_type ?? 0,
            total = modelList.Length,
            has_more = false,
            model_scene = request?.model_scene
        };
    }

    private static object BuildWorkflowFallbackData(string? path)
    {
        var normalizedPath = NormalizeFallbackPath(path);
        return normalizedPath switch
        {
            "operate_list" => new { operate_list = Array.Empty<object>(), total = 0 },
            "differences" => new { has_conflict = false, differences = Array.Empty<object>() },
            "merge" => new { merged = true },
            "history_schema" => new { schema = "{}" },
            "list_collaborators" => new { collaborators = Array.Empty<object>(), total = 0 },
            "open_collaborator" => new { success = true },
            "close_collaborator" => new { success = true },
            "list_publish_workflow" => new { workflow_list = Array.Empty<object>(), total = 0 },
            "validate_tree" => new { valid = true, errors = Array.Empty<object>() },
            "dependency_tree" => new { nodes = Array.Empty<object>(), edges = Array.Empty<object>() },
            "encapsulate" => new { success = true },
            "data_compensation" => new { success = true },
            "upload/auth_token" => new { auth_token = string.Empty, expire_at = 0 },
            "sign_image_url" => new { url = string.Empty },
            "imageflow_basic_nodes" => new { node_list = Array.Empty<object>() },
            "message_nodes" => new { node_list = Array.Empty<object>() },
            "apiDetail" => new { api = (object?)null },
            "bots_ide_token" => new { token = string.Empty, expire_at = 0 },
            "delete_strategy" => new { strategy = 0 },
            "listable_workflows" => new { workflow_list = Array.Empty<object>(), total = 0 },
            "node_panel_search" => new { list = Array.Empty<object>(), total = 0 },
            "get_plugin_auth_status" => new { status = 0 },
            "old_publish" or "old_create" or "old_save" or "old_query" or "old_list" or "old_testRun" or "old_delete" or "old_copy" or "old_biz_list"
                => new { success = true },
            "batch_delete" => new { deleted = 0 },
            "stream_run_flow" => new { execution_id = string.Empty, status = "running" },
            "batch_get_wkprocess_io" => new { list = Array.Empty<object>() },
            "save_trigger" or "delete_trigger" => new { trigger_id = string.Empty, success = true },
            "list_trigger_events" or "list_triggers" or "list_publish_trigger" => new { list = Array.Empty<object>(), total = 0 },
            "testrun_trigger" => new { success = true },
            "get_trigger" or "get_publish_trigger" => new { trigger = (object?)null },
            "operate_publish_trigger" => new { success = true },
            "copilot_generate" => new { content = string.Empty },
            "project_conversation/create" => new { id = string.Empty },
            "project_conversation/update" or "project_conversation/delete" or "project_conversation/batch_delete" => new { success = true },
            "project_conversation/list" => new { list = Array.Empty<object>(), total = 0 },
            "chat_flow_role/create" => new { role_id = string.Empty },
            "chat_flow_role/delete" => new { success = true },
            "chat_flow_role/get" => new { role = (object?)null },
            "job_list" => new { list = Array.Empty<object>(), total = 0 },
            "task_list" => new { list = Array.Empty<object>(), total = 0 },
            "job_create" => new { job_id = string.Empty },
            "job_cancel" or "task_cancel" or "task_retry" => new { success = true },
            "job_validate_input" => new { valid = true, errors = Array.Empty<object>() },
            "job_input_template" or "job_output" => new { url = string.Empty },
            "job_input_config" => new { fields = Array.Empty<object>() },
            "get_node_execute_history" or "get_async_sub_process" => new { list = Array.Empty<object>(), total = 0 },
            "store_testrun_history" => new { list = Array.Empty<object>(), total = 0 },
            "get_flowlang_gray" or "get_code_migrate_gray" => new { enabled = false },
            "get_node_field_config" => new { fields = Array.Empty<object>() },
            "mget_version_history" => new { version_list = Array.Empty<object>() },
            "get_p90" => new { p90 = 0 },
            "biz_list" => new { workflow_list = Array.Empty<object>(), total = 0 },
            "behavior_auth" => new { allowed = true },
            "example_workflow_list" => new { workflow_list = Array.Empty<object>(), total = 0 },
            _ => new { }
        };
    }

    private static object BuildPlaygroundFallbackData(string? path)
    {
        var normalizedPath = NormalizeFallbackPath(path);
        return normalizedPath switch
        {
            "space/info" => new { data = (object?)null },
            "space/delete" => new { success = true },
            "space/invite" => new { invite_link = string.Empty },
            "space/member/detail" => new { member = (object?)null },
            "space/member/update" or "space/member/transfer" or "space/member/remove" or "space/member/add" or "space/member/exit" => new { success = true },
            "space/member/search" => new { list = Array.Empty<object>(), total = 0 },
            "space/revocate_invite" => new { success = true },
            "space/invite_manage_list" or "space/apply_manage_list" => new { list = Array.Empty<object>(), total = 0 },
            "space/remove_publish_member" or "space/add_publish_member" or "space/operate_apply" => new { success = true },
            "space/search_addable_publish_member" or "space/publish_member_list" => new { list = Array.Empty<object>(), total = 0 },
            "space/import/confirm" => new { success = true },
            "space/import/list" or "space/import/user_list" => new { list = Array.Empty<object>(), total = 0 },
            "get_type_list" => new
            {
                model_list = Array.Empty<object>(),
                voice_list = Array.Empty<object>(),
                raw_model_list = Array.Empty<object>(),
                model_show_family_list = Array.Empty<object>(),
                default_model_id = 0
            },
            "get_voice_list" or "get_voice_list_v2" => new { list = Array.Empty<object>(), total = 0 },
            "get_support_language" => new { list = Array.Empty<object>() },
            "synchronize_voice_list" => new { success = true },
            "create_room" => new { room_id = string.Empty },
            _ => new { success = true }
        };
    }

    private static object BuildDeveloperFallbackData(string? path)
    {
        var normalizedPath = NormalizeFallbackPath(path);
        return normalizedPath switch
        {
            "get_draft_bot_list" => new { total = 0, list = Array.Empty<object>() },
            "upload_file" => new { file_id = Guid.NewGuid().ToString("N"), file_url = string.Empty },
            _ => new { success = true }
        };
    }

    private static object BuildOpenApiFallbackData(string? path)
    {
        var normalizedPath = NormalizeFallbackPath(path);
        return normalizedPath switch
        {
            "run" or "stream_run" or "stream_resume" or "chat" => new { execution_id = string.Empty, status = "running" },
            "get_run_history" => new { list = Array.Empty<object>(), total = 0 },
            _ => new { workflow = (object?)null, list = Array.Empty<object>(), total = 0 }
        };
    }

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

    private static string NormalizeCanvasJsonForCoze(string? canvasJson)
    {
        if (string.IsNullOrWhiteSpace(canvasJson))
        {
            return "{\"nodes\":[],\"edges\":[]}";
        }

        return DecodeHtmlEntities(canvasJson);
    }

    private static string ToCozeNodeTypeCode(string nodeTypeKey)
    {
        if (Enum.TryParse<WorkflowNodeType>(nodeTypeKey, true, out var nodeType))
        {
            return ((int)nodeType).ToString(CultureInfo.InvariantCulture);
        }

        return nodeTypeKey;
    }

    private static string DecodeHtmlEntities(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var decoded = WebUtility.HtmlDecode(value);
        return string.IsNullOrWhiteSpace(decoded) ? value : decoded;
    }

    private static string NormalizeFallbackPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Trim().Trim('/').ToLowerInvariant();
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

    private static string ToOpenRoleType(string? roleCode)
    {
        if (string.Equals(roleCode, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            return "owner";
        }

        if (string.Equals(roleCode, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return "admin";
        }

        return "member";
    }

    private static string ToWorkspaceRoleCode(string? roleType)
    {
        if (string.Equals(roleType, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return "Owner";
        }

        if (string.Equals(roleType, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        return "Member";
    }

    private static object MapOpenWorkspace(WorkspaceListItem item)
    {
        return new
        {
            id = item.Id,
            name = item.Name,
            icon_url = item.Icon ?? string.Empty,
            role_type = ToOpenRoleType(item.RoleCode),
            workspace_type = "team",
            enterprise_id = item.OrgId,
            joined_status = "joined",
            description = item.Description ?? string.Empty,
            owner_uid = string.Empty,
            admin_uids = Array.Empty<string>()
        };
    }

    private static object MapOpenSpaceMember(WorkspaceMemberDto item)
    {
        return new
        {
            user_id = item.UserId,
            user_nickname = item.DisplayName,
            user_unique_name = item.Username,
            avatar_url = string.Empty,
            role_type = ToOpenRoleType(item.RoleCode)
        };
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

    /// <summary>
    /// 与 Coze IDL workflow.NodeExeStatus 对齐：1=Waiting/2=Running/3=Success/4=Fail。
    /// </summary>
    private enum NodeExeStatusCode
    {
        Waiting = 1,
        Running = 2,
        Success = 3,
        Fail = 4
    }

    private static int ToNodeExeStatusCode(ExecutionStatus status)
    {
        return status switch
        {
            ExecutionStatus.Running => (int)NodeExeStatusCode.Running,
            ExecutionStatus.Completed => (int)NodeExeStatusCode.Success,
            ExecutionStatus.Failed or ExecutionStatus.Blocked => (int)NodeExeStatusCode.Fail,
            ExecutionStatus.Skipped or ExecutionStatus.Cancelled or ExecutionStatus.Interrupted => (int)NodeExeStatusCode.Fail,
            _ => (int)NodeExeStatusCode.Waiting
        };
    }

    private static int TryParseCozeValidateErrorType(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0;
        }

        // 与 Coze ValidateErrorType 大致对齐：
        // 0 = NodeError, 1 = BotConcurrentPathErr, 2 = BotValidatePathErr。
        if (code.Contains("path", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (code.Contains("concurrent", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// 向审计中心写入一条 Coze 兼容层写动作记录；
    /// 当宿主未注册 <see cref="IAuditWriter"/> 时静默跳过，
    /// 不影响 Coze playground 写流程的成功响应。
    /// </summary>
    private async Task TryWriteAuditAsync(
        string action,
        string? targetIdentifier,
        bool succeeded,
        CancellationToken cancellationToken,
        string? failureReason = null)
    {
        var writer = HttpContext.RequestServices.GetService<IAuditWriter>();
        if (writer is null)
        {
            return;
        }

        var actor = HttpContext.User?.Identity?.Name
            ?? _currentUserAccessor.GetCurrentUser()?.UserId.ToString(CultureInfo.InvariantCulture)
            ?? "anonymous";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.TryGetValue("User-Agent", out var ua) ? ua.ToString() : null;
        var record = new AuditRecord(
            _tenantProvider.GetTenantId(),
            actor,
            $"coze_workflow.{action}",
            succeeded ? "success" : (failureReason ?? "failed"),
            string.IsNullOrWhiteSpace(targetIdentifier) ? null : $"workflow:{targetIdentifier}",
            ip,
            userAgent);

        try
        {
            await writer.WriteAsync(record, cancellationToken);
        }
        catch
        {
            // 审计失败不应影响 Coze 兼容层主流程；详细错误由 IAuditWriter 内部 Logger 记录。
        }
    }

    private static string? NormalizeIncomingSchema(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(schema);
        return string.IsNullOrWhiteSpace(decoded) ? schema : decoded;
    }

    private static int ResolvePageIndex(string? pageOrCursor)
    {
        if (string.IsNullOrWhiteSpace(pageOrCursor))
        {
            return 1;
        }

        return int.TryParse(pageOrCursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var page)
            && page > 0
            ? page
            : 1;
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

    private static object[] BuildModelParams(ModelConfigDto item)
    {
        var temperature = (item.Temperature ?? 1f).ToString("0.0", CultureInfo.InvariantCulture);
        var maxTokens = (item.MaxTokens ?? 4096).ToString(CultureInfo.InvariantCulture);

        return
        [
            new
            {
                name = "temperature",
                label = "Temperature",
                type = 1,
                min = "0",
                max = "2",
                precision = 1,
                default_val = BuildDefaultValue(temperature),
                param_class = new { class_id = 1, label = "Generation diversity" }
            },
            new
            {
                name = "max_tokens",
                label = "Max Tokens",
                type = 2,
                min = "1",
                max = "32000",
                precision = 0,
                default_val = BuildDefaultValue(maxTokens),
                param_class = new { class_id = 2, label = "Input and output length" }
            },
            new
            {
                name = "response_format",
                label = "Response format",
                type = 2,
                min = "1",
                max = "3",
                precision = 0,
                default_val = BuildDefaultValue("3"),
                options = new object[]
                {
                    new { label = "Text", value = 1 },
                    new { label = "Markdown", value = 2 },
                    new { label = "JSON", value = 3 }
                },
                param_class = new { class_id = 3, label = "Output format" }
            }
        ];
    }

    private static object BuildDefaultValue(string value)
    {
        return new
        {
            default_val = value,
            creative = value,
            balance = value,
            precise = value,
            customize = value
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

public sealed record CozeSaveSpaceRequest(
    string? space_id,
    string? name,
    string? description,
    string? icon_uri,
    int? space_type,
    int? space_mode);

public sealed record CozeOpenCreateSpaceRequest(
    string? Name,
    string? Description,
    [property: JsonPropertyName("icon_file_id")] string? IconFileId,
    [property: JsonPropertyName("coze_account_id")] string? CozeAccountId,
    [property: JsonPropertyName("owner_uid")] string? OwnerUid);

public sealed record CozeOpenSpaceMemberRequestItem(
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("role_type")] string? RoleType);

public sealed record CozeOpenAddSpaceMemberRequest(
    [property: JsonPropertyName("users")] IReadOnlyList<CozeOpenSpaceMemberRequestItem>? Users);

public sealed record CozeOpenRemoveSpaceMemberRequest(
    [property: JsonPropertyName("user_ids")] IReadOnlyList<string>? UserIds);

public sealed record CozeOpenApplyJoinSpaceRequest(
    [property: JsonPropertyName("user_ids")] IReadOnlyList<string>? UserIds);

public sealed record CozeOpenUpdateSpaceMemberRequest(
    [property: JsonPropertyName("role_type")] string? RoleType);

public sealed record CozeOpenSwitchBotDevelopModeRequest(
    [property: JsonPropertyName("collaboration_mode")] string? CollaborationMode);

public sealed record CozeGetTypeListRequest(
    int? model_scene,
    string? connector_id,
    int? include_deleted);

public sealed record CozeCreateWorkflowRequest(
    string? name,
    string? desc,
    string? icon_uri,
    string? space_id,
    int? flow_mode);

public sealed record CozeSubmitWorkflowRequest(
    string? workflow_id,
    string? space_id,
    string? submit_commit_id);

public sealed record CozeCheckLatestSubmitVersionRequest(
    string? workflow_id,
    string? space_id);

public sealed record CozeDeleteWorkflowRequest(
    string? workflow_id,
    string? space_id);

public sealed record CozeGetWorkflowListRequest(
    int page = 1,
    int size = 20,
    string? name = null,
    int? status = null,
    int? flow_mode = null,
    string? space_id = null);

public sealed record CozeGetWorkflowDetailRequest(
    string[]? workflow_ids,
    string? space_id);

public sealed record CozeWorkflowDetailInfoRequest(
    string? workflow_id,
    string? space_id);

public sealed record CozeUpdateWorkflowMetaRequest(
    string? workflow_id,
    string? name,
    string? desc,
    string? space_id);

public sealed record CozeRevertDraftRequest(
    string? workflow_id,
    string? space_id);

public sealed record CozeVersionHistoryListRequest(
    string? workflow_id,
    string? space_id,
    int page = 1,
    int size = 20);

public sealed record CozeGetTraceRequest(
    string? log_id,
    string? workflow_id,
    string? execute_id);

public sealed record CozeListRootSpansRequest(
    string? workflow_id,
    long? start_at,
    long? end_at,
    int? limit,
    int? offset);

public sealed record CozeConflictFromContentRequest(
    string? workflow_id,
    string? schema,
    string? space_id);

public sealed record CozeValidateTreeRequest(
    string? workflow_id,
    string? bind_project_id,
    string? bind_bot_id,
    string? schema);

public sealed record CozeNodePanelSearchRequest(
    int? search_type,
    string? space_id,
    string? project_id,
    string? search_key,
    string? page_or_cursor,
    int? page_size,
    string? exclude_workflow_id,
    string? enterprise_id);

public sealed record CozeGetHistorySchemaRequest(
    string? space_id,
    string? workflow_id,
    string? commit_id,
    int? type,
    string? env,
    string? workflow_version,
    string? project_version,
    string? project_id,
    string? execute_id,
    string? sub_execute_id,
    string? log_id);

public sealed record CozeGetNodeExecuteHistoryRequest(
    string? workflow_id,
    string? space_id,
    string? execute_id,
    string? node_id,
    bool? is_batch,
    int? batch_index,
    string? node_type,
    int? node_history_scene);

public sealed record CozeBatchDeleteWorkflowRequest(
    string[]? workflow_id_list,
    string? space_id,
    int? action);

public sealed record CozeCopyWkTemplateRequest(
    string[]? workflow_ids,
    string? target_space_id);

public sealed record CozeExampleWorkflowListRequest(
    int? page,
    int? size,
    string? name,
    int? flow_mode,
    int[]? checker);

public sealed record CozeGetApiDetailRequest(
    string? pluginID,
    string? apiName,
    string? space_id,
    string? api_id,
    string? project_id,
    string? plugin_version,
    string? plugin_from);

public sealed record CozeUploadAuthTokenRequest(string? scene);

public sealed record CozeSignImageUrlRequest(string? uri, string? Scene);

public sealed record CozeListPublishWorkflowRequest(
    string? space_id,
    string? owner_id,
    string? name,
    bool? order_last_publish_time,
    bool? order_total_token,
    int? size,
    string? cursor_id,
    string[]? workflow_ids);

public sealed record CozeGetNodeAsyncExecuteHistoryRequest(
    string? space_id,
    string? parent_workflow_id,
    string? parent_node_id,
    string? workflow_id,
    int? status);

public sealed record CozeGetDraftBotListRequest(
    int? page,
    int? size,
    string? space_id,
    string? name);

public sealed record CozeGetDraftBotDisplayInfoRequest(
    string? bot_id,
    string? space_id);

public sealed record CozeGetSpaceInfoRequest(string? space_id);
