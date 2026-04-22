# Coze app-web gateway 能力缺口表

> 对照基准：`e:/codeding/coze-studio/idl/workflow/workflow_svc.thrift`、`workflow.thrift`、`trace.thrift`。
> Atlas 现实现状：
> - workflow：`src/backend/Atlas.AppHost/Controllers/AppWebWorkflowGatewayController.cs`
> - developer：`src/backend/Atlas.AppHost/Controllers/AppWebCozeDeveloperGatewayController.cs`
> - playground：`src/backend/Atlas.AppHost/Controllers/AppWebCozePlaygroundGatewayController.cs`
> - 旧 `CozeWorkflowCompatControllerBase` / `CozeWorkflowCompatController` 运行期入口已删除。
>
> 本文件维护"前端调用 → 后端真实实现"的对应关系，是 M2 的唯一权威来源；任何端点状态变更需同步更新本表与 `docs/contracts.md` 的兼容层章节。
>
> 自 2026-04-23 起，工作流之外的原生 developer / playground / passport / workspace 直连覆盖以 [`docs/coze-interface-coverage-matrix.md`](coze-interface-coverage-matrix.md) 为权威清单；本文件聚焦 `workflow_api` 缺口，不再单独承载所有 Coze 路由面。
>
> 状态分级：
> - **OK**：真实实现，行为与当前 Coze workflow 服务（`ICozeWorkflow*Service`）一致。
> - **OK-Compat**：真实实现，但部分字段为占位/与上游字面对齐（标 `space_id`、`plugin_id` 等不区分场景）。
> - **Fallback**：保留 catch-all/默认值返回，结构与上游兼容但不连后端语义；前端可正常拿到 `code:0` 但不会承载真实业务数据。
> - **Missing**：完全缺失，未来需要补齐。

## 1. 工作流核心 API（`/api/app-web/workflow-sdk/*`）

| Thrift Method | HTTP | Atlas 状态 | 说明 |
|---|---|---|---|
| `CreateWorkflow` | POST `/create` | OK | `DagWorkflowCommandService.CreateAsync`；`flow_mode=3` 映射 ChatFlow。 |
| `SaveWorkflow` | POST `/save` | OK | 自动 HtmlDecode + 后端标准画布归一化（Coze 原生 `id/meta/data + edges` 也会落成 Atlas 标准 `key/config/layout + connections`）。 |
| `UpdateWorkflowMeta` | POST `/update_meta` | OK | `flow_mode` 字段忽略（后端单独维护 mode）。 |
| `DeleteWorkflow` | POST `/delete` | OK | `action` 字段忽略，软删除走 `DeleteAsync`。 |
| `BatchDeleteWorkflow` | POST `/batch_delete` | Fallback | 当前 gateway 仅返回兼容成功结构，未接真实批删服务。 |
| `GetDeleteStrategy` | POST `/delete_strategy` | Fallback | 当前 gateway 返回兼容删除策略结构，未接真实引用判定。 |
| `PublishWorkflow` | POST `/publish` | OK | `version_description` 写入 `WorkflowVersion.ChangeLog`。 |
| `CopyWorkflow` | POST `/copy` | OK | 复制后返回新 workflow id。 |
| `CopyWkTemplateApi` | POST `/copy_wk_template` | Fallback | 当前 gateway 返回兼容成功结构。 |
| `GetReleasedWorkflows` | POST `/released_workflows` | OK | 走 `ListPublishedAsync`。 |
| `GetWorkflowReferences` | POST `/workflow_references` | OK | 通过 `GetDependenciesAsync` 解析子工作流依赖。 |
| `GetExampleWorkFlowList` | POST `/example_workflow_list` | Fallback | 当前返回空 `workflow_list`。 |
| `GetWorkFlowList` | POST `/workflow_list` | OK | `status=1` 时走 `ListPublishedAsync`，否则全量。 |
| `QueryWorkflowNodeTypes` | POST `/node_type` | OK | 节点类型属性表生成于 `ChatHistoryNodeKeys` 等常量。 |
| `NodeTemplateList` | POST `/node_template_list` | OK | 与 `NodeExecutorRegistry` 同源；支持 `node_types` 过滤。 |
| `NodePanelSearch` | POST `/node_panel_search` | OK（M1）| 节点目录关键字搜索；命中节点放在 `data.resource_workflow.workflow_list`。 |
| `GetLLMNodeFCSettingsMerged` | POST `/llm_fc_setting_merged` | Fallback | 仍走 `BuildWorkflowFallbackData("operate_list")` 路径默认值。后续 M3+ 联动 `IPluginRegistryService` 与 `WorkflowFCItem`。 |
| `GetLLMNodeFCSettingDetail` | POST `/llm_fc_setting_detail` | Fallback | 同上。 |
| `WorkFlowTestRun` | POST `/test_run` | OK | 走 `ICozeWorkflowExecutionService.SyncRunAsync`。 |
| `WorkFlowTestResume` | POST `/test_resume` | OK | 走 `ResumeAsync`。 |
| `CancelWorkFlow` | POST `/cancel` | OK | 走 `CancelAsync`。 |
| `GetWorkFlowProcess` | GET `/get_process` | OK | 节点输入/输出 + `errorLevel`。 |
| `GetNodeExecuteHistory` | GET `/get_node_execute_history` | OK（M1）| `extra` 三段 JSON 字符串（`input`/`output`/`variables`）。 |
| `GetApiDetail` | GET `/apiDetail` | Fallback | 当前返回 `api = null` 的兼容结构。 |
| `WorkflowNodeDebugV2` | POST `/nodeDebug` | OK | 走 `DebugNodeAsync`。 |
| `GetWorkflowUploadAuthToken` | POST `/upload/auth_token` | Fallback | 当前 gateway 返回空 token 兼容结构。 |
| `SignImageURL` | POST `/sign_image_url` | Fallback | 当前 gateway 返回空 url 兼容结构。 |
| `CreateProjectConversationDef` | POST `/project_conversation/create` | Fallback | 待 M3 引入 `IConversationService`。 |
| `UpdateProjectConversationDef` | POST `/project_conversation/update` | Fallback | 同上。 |
| `DeleteProjectConversationDef` | POST `/project_conversation/delete` | Fallback | 同上。 |
| `ListProjectConversationDef` | GET `/project_conversation/list` | Fallback | 同上。 |
| `ListRootSpans` | POST `/list_spans` | **OK-via-runtime** | M13 已落地：`/api/runtime/traces` + `/api/runtime/traces/{traceId}` 提供完整 spans 树（基于 `RuntimeTrace`/`RuntimeSpan` 持久化），按 traceId / appId / page / component / 时间 / errorType / userId 6 维查询；`buildSpanTree` 客户端工具构造时间线视图。Coze 兼容层 `/list_spans` 转交给 `IRuntimeTraceService`，前端可同时通过两套路径访问。 |
| `GetTraceSDK` | POST `/get_trace` | OK（M1 改造）| 含根 span + `extra.{input,output,variables}` 三段。 |
| `GetWorkflowDetail` | POST `/workflow_detail` | OK | 多 ID 批量查询。 |
| `GetWorkflowDetailInfo` | POST `/workflow_detail_info` | OK | 单 ID 详情。 |
| `ValidateTree` | POST `/validate_tree` | OK（M1）| `Array<ValidateTreeInfo>` 结构。 |
| `ValidateSchema` | POST `/old_validate` | OK | 旧版 schema 校验。 |
| `GetChatFlowRole` | GET `/chat_flow_role/get` | Fallback | 待 M3 引入 ChatFlow 角色域。 |
| `CreateChatFlowRole` | POST `/chat_flow_role/create` | Fallback | 同上。 |
| `DeleteChatFlowRole` | POST `/chat_flow_role/delete` | Fallback | 同上。 |
| `ListPublishWorkflow` | POST `/list_publish_workflow` | Fallback | 当前 gateway 返回空 `workflow_list`。 |
| `GetHistorySchema` | POST `/history_schema` | OK（M1）| 优先级 `execute_id` → `commit_id` → 最新版本 → 草稿。 |
| `GetWorkflowGrayFeature` | POST `/gray_feature` | OK | `workflow_v2 / selector_prune / loop_control` 三个开关。 |
| `RegionGray` | POST `/region_gray` | OK | 默认开启。 |
| `GetNodeAsyncExecuteHistory` | POST `/get_async_sub_process` | Fallback | 当前 gateway 返回空列表。 |

## 2. OpenAPI（`/v1/workflow/*` 与 `/v1/workflows/*`）

| Method | HTTP | Atlas 状态 | 说明 |
|---|---|---|---|
| `OpenAPIRunFlow` | POST `/v1/workflow/run` | Fallback | M5 启用 PAT 认证后落地真实运行；当前返回 `execution_id="" status=running`。 |
| `OpenAPIStreamRunFlow` | POST `/v1/workflow/stream_run` | Fallback | 同上。 |
| `OpenAPIStreamResumeFlow` | POST `/v1/workflow/stream_resume` | Fallback | 同上。 |
| `OpenAPIGetWorkflowRunHistory` | GET `/v1/workflow/get_run_history` | Fallback | M5 实现。 |
| `OpenAPIChatFlowRun` | POST `/v1/workflows/chat` | **OK-via-runtime** | M11 已落地 `/api/runtime/chatflows/{id}:invoke`（SSE 4 类事件 + 中断/恢复/插入 + 多会话）。当前运行时已桥接到 Coze workflow 执行服务；chatflowId 是合法 long 时走真实执行，非 long 时返回明确错误，不再使用 mock pipeline。 |
| `OpenAPIGetWorkflowInfo` | GET `/v1/workflows/{id}` | Fallback | M5 实现。 |
| `OpenAPICreateConversation` | POST `/v1/workflow/conversation/create` | Fallback | M3 ChatFlow 一并完成。 |

## 3. Developer / Playground / Workspace 适配（`/api/app-web/coze-developer/*`、`/api/app-web/coze-playground/*`）

| Method | HTTP | Atlas 状态 | 说明 |
|---|---|---|---|
| `GetSpaceListV2` | POST `/api/app-web/coze-playground/space/list` | OK | 走 `IWorkspacePortalService.ListWorkspacesAsync`。 |
| `SaveSpace` | POST `/api/app-web/coze-playground/space/save` | OK | 仅匹配既有 workspace。 |
| `GetSpaceInfo` | POST `/api/app-web/coze-playground/space/info` | OK | 走 `IWorkspacePortalService.ListWorkspacesAsync`。 |
| `GetTypeList` | POST `/api/app-web/coze-developer/bot/get_type_list`、`/api/app-web/coze-playground/get_type_list` | OK | 走 `IModelConfigQueryService`。 |
| `DraftBotList` | POST `/api/app-web/coze-developer/draftbot/get_draft_bot_list` | OK | 走 `ITeamAgentService.GetPagedAsync`。 |
| `DraftBotDisplayInfo` | POST `/api/app-web/coze-developer/draftbot/get_display_info` | OK | 走 `ITeamAgentService.GetByIdAsync`。 |
| `MarketplaceFavoriteList` | GET `/api/app-web/coze-playground/marketplace/product/favorite/list[.v2]` | Fallback | 当前返回空收藏列表。 |
| `UploadBotFile` | POST `/api/app-web/coze-developer/bot/upload_file` | Fallback | 返回伪文件 id。 |
| `OpenSpaceList` | GET `/api/app-web/coze-playground/open/workspaces` | OK | 与 `OpenSpaceList` 实现一致。 |
| `OpenCreateSpace` | POST `/api/app-web/coze-playground/open/workspaces` | OK | 复用 `IWorkspacePortalService.CreateWorkspaceAsync`。 |
| `OpenSpaceMemberList` | GET `/api/app-web/coze-playground/open/workspaces/{id}/members` | OK | 走 `GetMembersAsync`。 |
| `OpenAddSpaceMember` | POST `/api/app-web/coze-playground/open/workspaces/{id}/members` | OK | 仅取 users[0]。 |
| `OpenRemoveSpaceMember` | DELETE `/api/app-web/coze-playground/open/workspaces/{id}/members` | OK | 单用户。 |
| `OpenUpdateSpaceMember` | PUT `/api/app-web/coze-playground/open/workspaces/{id}/members/{userId}` | OK | `role_type` 字段映射 Owner/Admin/Member。 |
| `OpenRemoveSpace` | DELETE `/api/app-web/coze-playground/open/workspaces/{id}` | OK | 删除空间。 |
| `OpenGetBotInfo` | GET `/api/app-web/coze-playground/open/bots/{botId}` | Fallback | 返回占位 bot 信息。 |
| `OpenListBotVersions` | GET `/api/app-web/coze-playground/open/bots/{botId}/versions` | Fallback | 返回空版本列表。 |
| `OpenSwitchBotDevelopMode` | POST `/api/app-web/coze-playground/open/bots/{botId}/collaboration_mode` | Fallback | 返回兼容成功结构。 |

## 4. 仍为 fallback 的端点（按优先级排序）

| 优先级 | Method | HTTP | 缺失原因 / 计划 |
|---|---|---|---|
| P1 | `GetLLMNodeFCSettingsMerged` / `GetLLMNodeFCSettingDetail` | `/api/app-web/workflow-sdk/llm_fc_setting_merged` `/api/app-web/workflow-sdk/llm_fc_setting_detail` | 需要 LLM 节点工具 / 工作流 / 知识库的 FC 配置元数据。 |
| P1 | `Project*Conversation*` 4 个 | `/api/app-web/workflow-sdk/project_conversation/*` | ChatFlow 项目会话域，需要新建 `IProjectConversationService`。 |
| P1 | `ChatFlowRole*` 3 个 | `/api/app-web/workflow-sdk/chat_flow_role/*` | 同 ChatFlow 域。 |
| P2 | OpenAPI 7 个（`/v1/workflow/*`、`/v1/workflows/*`） | 见 §2 | 需要 PAT 认证 + 真实 ChatFlow 跑批。M5 完成。 |
| P2 | `UploadBotFile`、Bot/DraftBot 系列 | `/api/app-web/coze-developer/*`、`/api/app-web/coze-playground/open/bots/*` | 仍有一部分是 fallback 占位，需继续接智能体（Bot）真服务。 |
| P3 | `Marketplace*Favorite*` | `/api/app-web/coze-playground/marketplace/product/favorite/list[.v2]` | Atlas 暂无收藏概念；保持空列表。 |
| P3 | developer/playground catch-all | `/api/app-web/coze-developer/{**path}`、`/api/app-web/coze-playground/{**path}` | 当前仍承接长尾页面所需的兼容成功结构，不应视作真实服务。 |

## 6. 前端 spaceId / workspaceId 二义性消除

- 现状：`WorkspaceWorkflowWorkbenchRoute` 把 `workspace.id`（如 `"123456"`）透传给 `<WorkflowPage spaceId={workspace.id}>`，但 cozelib 内部很多地方又把 `spaceId` 用作 `urlParam` 的 `space_id`，导致后端收到的 `space_id` 与实际 workspace id 不一致。
- 已落地：M1 `atlas-foundation-bridge.useSpace` 在 `setAtlasFoundationHost` 注入时已统一以 `workspace.id` 为 `id`，不再使用 `bootstrap.spaceId`。
- M3 进一步统一：在路由层把 `spaceId={workspace.id}` 显式提取为常量，并在 `WorkflowRuntimeBoundary` 注入时用同一变量；当前 app-web gateway 已接受字符串数字与 GUID 两种 workspace id 表达。
