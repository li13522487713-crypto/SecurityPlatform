using System.Text.Json.Serialization;

namespace Atlas.Presentation.Shared.Controllers.Ai;

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

public sealed record CozeValidateSchemaRequest(string? workflow_id, string? schema, string? bind_project_id, string? bind_bot_id);

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

public sealed record CozeDependencyTreeRequest(
    int type,
    CozeDependencyLibraryInfo? library_info,
    CozeDependencyProjectInfo? project_info);

public sealed record CozeDependencyLibraryInfo(
    string? workflow_id,
    string? space_id,
    bool? draft,
    string? workflow_version);

public sealed record CozeDependencyProjectInfo(
    string? workflow_id,
    string? space_id,
    string? project_id,
    bool? draft,
    string? project_version);

public sealed record CozePluginPricingByWorkflowRequest(
    string? workflow_id,
    string? space_id);

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

public sealed record CozeDeletePromptResourceRequest(string? prompt_resource_id);
