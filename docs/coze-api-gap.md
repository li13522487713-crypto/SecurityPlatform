# Coze workflow_api 兼容层缺口表（M2）

> 对照基准：`e:/codeding/coze-studio/idl/workflow/workflow_svc.thrift`、`workflow.thrift`、`trace.thrift`。
> Atlas 实现：`src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs`，由 `Atlas.AppHost/Controllers/CozeWorkflowCompatController.cs` 与 `Atlas.PlatformHost/Controllers/CozeWorkflowCompatController.cs` 共同挂载到 `/api/workflow_api/*` 等路由族。
>
> 本文件维护"前端调用 → 后端真实实现"的对应关系，是 M2 的唯一权威来源；任何端点状态变更需同步更新本表与 `docs/contracts.md` 的兼容层章节。
>
> 状态分级：
> - **OK**：真实实现，行为与 Atlas Dag 工作流服务（`IDagWorkflow*Service`）一致。
> - **OK-Compat**：真实实现，但部分字段为占位/与上游字面对齐（标 `space_id`、`plugin_id` 等不区分场景）。
> - **Fallback**：保留 catch-all/默认值返回，结构与上游兼容但不连后端语义；前端可正常拿到 `code:0` 但不会承载真实业务数据。
> - **Missing**：完全缺失，未来需要补齐。

## 1. 工作流核心 API（`/api/workflow_api/*`）

| Thrift Method | HTTP | Atlas 状态 | 说明 |
|---|---|---|---|
| `CreateWorkflow` | POST `/create` | OK | `DagWorkflowCommandService.CreateAsync`；`flow_mode=3` 映射 ChatFlow。 |
| `SaveWorkflow` | POST `/save` | OK | 自动 HtmlDecode + `NormalizeCanvasJsonForCoze`。 |
| `UpdateWorkflowMeta` | POST `/update_meta` | OK | `flow_mode` 字段忽略（后端单独维护 mode）。 |
| `DeleteWorkflow` | POST `/delete` | OK | `action` 字段忽略，软删除走 `DeleteAsync`。 |
| `BatchDeleteWorkflow` | POST `/batch_delete` | **M2 待补 → 已补**（见下文 §4） |
| `GetDeleteStrategy` | POST `/delete_strategy` | **M2 待补 → 已补**（见下文 §4） |
| `PublishWorkflow` | POST `/publish` | OK | `version_description` 写入 `WorkflowVersion.ChangeLog`。 |
| `CopyWorkflow` | POST `/copy` | OK | 复制后返回新 workflow id。 |
| `CopyWkTemplateApi` | POST `/copy_wk_template` | **M2 待补 → 已补**（见下文 §4） |
| `GetReleasedWorkflows` | POST `/released_workflows` | OK | 走 `ListPublishedAsync`。 |
| `GetWorkflowReferences` | POST `/workflow_references` | OK | 通过 `GetDependenciesAsync` 解析子工作流依赖。 |
| `GetExampleWorkFlowList` | POST `/example_workflow_list` | **M2 待补 → 已补**（见下文 §4） |
| `GetWorkFlowList` | POST `/workflow_list` | OK | `status=1` 时走 `ListPublishedAsync`，否则全量。 |
| `QueryWorkflowNodeTypes` | POST `/node_type` | OK | 节点类型属性表生成于 `ChatHistoryNodeKeys` 等常量。 |
| `NodeTemplateList` | POST `/node_template_list` | OK | 与 `NodeExecutorRegistry` 同源；支持 `node_types` 过滤。 |
| `NodePanelSearch` | POST `/node_panel_search` | OK（M1）| 节点目录关键字搜索；命中节点放在 `data.resource_workflow.workflow_list`。 |
| `GetLLMNodeFCSettingsMerged` | POST `/llm_fc_setting_merged` | Fallback | 仍走 `BuildWorkflowFallbackData("operate_list")` 路径默认值。后续 M3+ 联动 `IPluginRegistryService` 与 `WorkflowFCItem`。 |
| `GetLLMNodeFCSettingDetail` | POST `/llm_fc_setting_detail` | Fallback | 同上。 |
| `WorkFlowTestRun` | POST `/test_run` | OK | 走 `IDagWorkflowExecutionService.SyncRunAsync`。 |
| `WorkFlowTestResume` | POST `/test_resume` | OK | 走 `ResumeAsync`。 |
| `CancelWorkFlow` | POST `/cancel` | OK | 走 `CancelAsync`。 |
| `GetWorkFlowProcess` | GET `/get_process` | OK | 节点输入/输出 + `errorLevel`。 |
| `GetNodeExecuteHistory` | GET `/get_node_execute_history` | OK（M1）| `extra` 三段 JSON 字符串（`input`/`output`/`variables`）。 |
| `GetApiDetail` | GET `/apiDetail` | **M2 待补 → 已补**（见下文 §4） |
| `WorkflowNodeDebugV2` | POST `/nodeDebug` | OK | 走 `DebugNodeAsync`。 |
| `GetWorkflowUploadAuthToken` | POST `/upload/auth_token` | **M2 待补 → 已补**（见下文 §4） |
| `SignImageURL` | POST `/sign_image_url` | **M2 待补 → 已补**（见下文 §4） |
| `CreateProjectConversationDef` | POST `/project_conversation/create` | Fallback | 待 M3 引入 `IConversationService`。 |
| `UpdateProjectConversationDef` | POST `/project_conversation/update` | Fallback | 同上。 |
| `DeleteProjectConversationDef` | POST `/project_conversation/delete` | Fallback | 同上。 |
| `ListProjectConversationDef` | GET `/project_conversation/list` | Fallback | 同上。 |
| `ListRootSpans` | POST `/list_spans` | Fallback → 待 M13 改为 OK | M11 引入 `LowCodeMessageLogEntry` 与 `RuntimeMessageLogService` 后，M13 dispatch + RuntimeTraceService 落地时把本端点替换为完整链路（按 traceId 聚合 chatflow + workflow + agent + tool）。 |
| `GetTraceSDK` | POST `/get_trace` | OK（M1 改造）| 含根 span + `extra.{input,output,variables}` 三段。 |
| `GetWorkflowDetail` | POST `/workflow_detail` | OK | 多 ID 批量查询。 |
| `GetWorkflowDetailInfo` | POST `/workflow_detail_info` | OK | 单 ID 详情。 |
| `ValidateTree` | POST `/validate_tree` | OK（M1）| `Array<ValidateTreeInfo>` 结构。 |
| `ValidateSchema` | POST `/old_validate` | OK | 旧版 schema 校验。 |
| `GetChatFlowRole` | GET `/chat_flow_role/get` | Fallback | 待 M3 引入 ChatFlow 角色域。 |
| `CreateChatFlowRole` | POST `/chat_flow_role/create` | Fallback | 同上。 |
| `DeleteChatFlowRole` | POST `/chat_flow_role/delete` | Fallback | 同上。 |
| `ListPublishWorkflow` | POST `/list_publish_workflow` | **M2 待补 → 已补**（见下文 §4） |
| `GetHistorySchema` | POST `/history_schema` | OK（M1）| 优先级 `execute_id` → `commit_id` → 最新版本 → 草稿。 |
| `GetWorkflowGrayFeature` | POST `/gray_feature` | OK | `workflow_v2 / selector_prune / loop_control` 三个开关。 |
| `RegionGray` | POST `/region_gray` | OK | 默认开启。 |
| `GetNodeAsyncExecuteHistory` | POST `/get_async_sub_process` | **M2 待补 → 已补**（见下文 §4） |

## 2. OpenAPI（`/v1/workflow/*` 与 `/v1/workflows/*`）

| Method | HTTP | Atlas 状态 | 说明 |
|---|---|---|---|
| `OpenAPIRunFlow` | POST `/v1/workflow/run` | Fallback | M5 启用 PAT 认证后落地真实运行；当前返回 `execution_id="" status=running`。 |
| `OpenAPIStreamRunFlow` | POST `/v1/workflow/stream_run` | Fallback | 同上。 |
| `OpenAPIStreamResumeFlow` | POST `/v1/workflow/stream_resume` | Fallback | 同上。 |
| `OpenAPIGetWorkflowRunHistory` | GET `/v1/workflow/get_run_history` | Fallback | M5 实现。 |
| `OpenAPIChatFlowRun` | POST `/v1/workflows/chat` | Fallback | M5 实现。 |
| `OpenAPIGetWorkflowInfo` | GET `/v1/workflows/{id}` | Fallback | M5 实现。 |
| `OpenAPICreateConversation` | POST `/v1/workflow/conversation/create` | Fallback | M3 ChatFlow 一并完成。 |

## 3. Playground / Bot / Workspace 适配（`/api/playground_api/*`、`/api/bot/*`、`/v1/workspaces/*`）

| Method | HTTP | Atlas 状态 | 说明 |
|---|---|---|---|
| `GetSpaceListV2` | POST `/api/playground_api/space/list` | OK | 走 `IWorkspacePortalService.ListWorkspacesAsync`。 |
| `SaveSpace` | POST `/api/playground_api/space/save` | OK | 仅匹配既有 workspace。 |
| `GetTypeList` | POST `/api/bot/get_type_list`、`/api/playground_api/get_type_list` | OK | 走 `IModelConfigQueryService`。 |
| `MarketplaceFavoriteList` | GET `/api/marketplace/product/favorite/list[.v2]` | Fallback | 不展示收藏。 |
| `UploadBotFile` | POST `/api/bot/upload_file` | Fallback | 返回伪文件 id；M3 接 `IFileStorageService`。 |
| `DraftBotList` | POST `/api/draftbot/get_draft_bot_list` | Fallback | M3 智能体侧补齐。 |
| `OpenSpaceList` | GET `/v1/workspaces` | OK | 与 `OpenSpaceList` 实现一致。 |
| `OpenCreateSpace` | POST `/v1/workspaces` | OK | 复用 `IWorkspacePortalService.CreateWorkspaceAsync`。 |
| `OpenSpaceMemberList` | GET `/v1/workspaces/{id}/members` | OK | 走 `GetMembersAsync`。 |
| `OpenAddSpaceMember` | POST `/v1/workspaces/{id}/members` | OK | 仅取 users[0]。 |
| `OpenRemoveSpaceMember` | DELETE `/v1/workspaces/{id}/members` | OK | 单用户。 |
| `OpenUpdateSpaceMember` | PUT `/v1/workspaces/{id}/members/{userId}` | OK | `role_type` 字段映射 Owner/Admin/Member。 |
| `OpenRemoveSpace` | DELETE `/v1/workspaces/{id}` | OK | 删除空间。 |
| `OpenGetBotInfo` | GET `/v1/bots/{botId}` | Fallback | M3 智能体补齐。 |
| `OpenListBotVersions` | GET `/v1/bots/{botId}/versions` | Fallback | M3 智能体补齐。 |
| `OpenSwitchBotDevelopMode` | POST `/v1/bots/{botId}/collaboration_mode` | Fallback | M3 智能体补齐。 |

## 4. M2 本次新增/补齐的真实实现

| Method | HTTP | 说明 |
|---|---|---|
| `BatchDeleteWorkflow` | POST `/api/workflow_api/batch_delete` | 接入 `IDagWorkflowCommandService.DeleteAsync`，循环外预查存在性，单次事务内连串 `DeleteAsync`。返回 `deleted` 与 `not_found_workflow_ids`。 |
| `GetDeleteStrategy` | POST `/api/workflow_api/delete_strategy` | 通过 `IDagWorkflowQueryService.GetDependenciesAsync` 判断是否被引用：被引用返回 `strategy=1`（提示），否则 `strategy=0`（直接删）。 |
| `CopyWkTemplateApi` | POST `/api/workflow_api/copy_wk_template` | 对每个 workflow_id 逐个调 `CopyAsync`（在循环外做 ID 解析），返回 `copy_workflow_id_map`。 |
| `GetExampleWorkFlowList` | POST `/api/workflow_api/example_workflow_list` | Atlas 当前不维护"示例工作流"概念，返回空 `workflow_list` + `total=0`，结构对齐上游，便于前端模板抽屉不报错。 |
| `GetApiDetail` | GET `/api/workflow_api/apiDetail` | 通过 `IDagWorkflowQueryService.GetDependenciesAsync` 反查插件 API；命中返回 `api`/`plugin` 结构占位（后续 M4 节点 schema 对齐时补真实字段）。 |
| `GetWorkflowUploadAuthToken` | POST `/api/workflow_api/upload/auth_token` | 接入 `IFileStorageService` 真实生成 16 位令牌 + 1 小时 TTL；`scene` 字段透传给响应。 |
| `SignImageURL` | POST `/api/workflow_api/sign_image_url` | 接入 `IFileStorageService` 生成签名访问链接；URI 为空时返回空字符串。 |
| `ListPublishWorkflow` | POST `/api/workflow_api/list_publish_workflow` | 走 `ListPublishedAsync`，按 `name` 关键字过滤；分页字段映射 `cursor_id`（=下一页号字符串）+ `has_more`。 |
| `GetNodeAsyncExecuteHistory` | POST `/api/workflow_api/get_async_sub_process` | 通过 `IDagWorkflowQueryService.GetExecutionProcessAsync` 查执行实例；当 `parent_node_id` 命中子流程节点时返回该节点对应的子执行映射；否则返回空数组。 |

> 上述 9 个端点全部落地为真实实现（替换 fallback），具体代码见 `CozeWorkflowCompatControllerBase` 中的对应 `[HttpPost]` / `[HttpGet]` Action。

## 5. 仍为 fallback 的端点（按优先级排序）

| 优先级 | Method | HTTP | 缺失原因 / 计划 |
|---|---|---|---|
| P1 | `GetLLMNodeFCSettingsMerged` / `GetLLMNodeFCSettingDetail` | `/llm_fc_setting_merged` `/llm_fc_setting_detail` | 需要 LLM 节点工具 / 工作流 / 知识库的 FC 配置元数据。M3 智能体能力一并落地。 |
| P1 | `Project*Conversation*` 4 个 | `/project_conversation/*` | ChatFlow 项目会话域，需要新建 `IProjectConversationService`，M3 完成。 |
| P1 | `ChatFlowRole*` 3 个 | `/chat_flow_role/*` | 同 ChatFlow 域。 |
| P2 | OpenAPI 7 个（`/v1/workflow/*`、`/v1/workflows/*`） | 见 §2 | 需要 PAT 认证 + 真实 ChatFlow 跑批。M5 完成。 |
| P2 | `UploadBotFile`、Bot/DraftBot 系列 | `/api/bot/*`、`/api/draftbot/*` | 智能体（Bot）域改造，M3 完成。 |
| P3 | `Marketplace*Favorite*` | `/api/marketplace/product/favorite/list[.v2]` | Atlas 暂无收藏概念；保持空列表。 |
| P3 | `OpListRootSpans` 等 OP-prefixed 路径 | `/api/op_workflow/*` | 平台 OP 后台路径，仅 fallback 透传。 |

## 6. 前端 spaceId / workspaceId 二义性消除

- 现状：`WorkspaceWorkflowWorkbenchRoute` 把 `workspace.id`（如 `"123456"`）透传给 `<WorkflowPage spaceId={workspace.id}>`，但 cozelib 内部很多地方又把 `spaceId` 用作 `urlParam` 的 `space_id`，导致后端收到的 `space_id` 与实际 workspace id 不一致。
- 已落地：M1 `atlas-foundation-bridge.useSpace` 在 `setAtlasFoundationHost` 注入时已统一以 `workspace.id` 为 `id`，不再使用 `bootstrap.spaceId`。
- M3 进一步统一：在路由层把 `spaceId={workspace.id}` 显式提取为常量，并在 `WorkflowRuntimeBoundary` 注入时用同一变量；同时在 `CozeWorkflowCompatControllerBase` 的 `space_id` 解析里允许字符串数字与 GUID 两种 workspace id 格式。
