# Coze 接口覆盖矩阵

本文件是 Coze 原生化阶段的权威覆盖清单。任何删除 `app-web` 兼容层、shim、bridge 的动作，都必须先对照本表确认所涉页面依赖接口已补齐。

状态说明：

- `OK`：已接真实后端能力，参数/返回/错误语义可作为当前主链使用
- `Partial`：路由存在，但仍有占位字段、弱语义或未覆盖全部场景
- `Missing`：当前未提供真实可用实现

验证列说明：

- `参数对齐`：请求字段名、层级、分页语义已与 Coze 调用方一致
- `返回对齐`：返回字段、类型、默认值已与 Coze 调用方一致
- `错误对齐`：401 / 资源缺失 / 参数错误等可被前端原生消费
- `实跑`：已通过至少一组真实页面或集成测试验证

## 第一批：Workflow / Foundation 最小原生闭环

| 页面 | 包 | 接口 | 方法 | 路由 | 必填参数 | 可选参数 | 关键返回字段 | 当前状态 | 参数对齐 | 返回对齐 | 错误对齐 | 实跑 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `CreateWorkflow` | `POST /api/workflow_api/create` | `name` | `desc`, `space_id`, `flow_mode` | `workflow_id`, `submit_commit_id` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `SaveWorkflow` | `POST /api/workflow_api/save` | `workflow_id`, `schema` | `space_id`, `submit_commit_id` | `workflow_status`, `is_version_gray` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `GetCanvasInfo` | `POST /api/workflow_api/canvas` | `workflow_id` | `space_id` | `workflow.schema_json`, `vcs_data` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `NodeTemplateList` | `POST /api/workflow_api/node_template_list` | 无 | `need_types`, `node_types` | `workflow_list[]` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `ValidateTree` | `POST /api/workflow_api/validate_tree` | `schema` 或 `workflow_id` | `bind_project_id`, `bind_bot_id` | `errors[]` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `WorkFlowTestRun` | `POST /api/workflow_api/test_run` | `workflow_id` | `input`, `space_id`, `bot_id`, `project_id` | `execute_id` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `GetWorkFlowProcess` | `GET /api/workflow_api/get_process` | `workflow_id`, `execute_id` | `sub_execute_id`, `log_id`, `node_id` | `executeStatus`, `nodeResults[]` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `GetTraceSDK` | `POST /api/workflow_api/get_trace` | `execute_id` 或 `log_id` | `workflow_id` | `spans[]`, `header` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `GetHistorySchema` | `POST /api/workflow_api/history_schema` | `workflow_id` | `commit_id`, `execute_id`, `log_id` | `schema` | OK | 是 | 是 | 是 | 部分 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `NodePanelSearch` | `POST /api/workflow_api/node_panel_search` | 无 | `search_key`, `space_id`, `page_or_cursor` | `data.resource_workflow.workflow_list[]` | OK | 是 | 是 | 是 | 部分 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `GetWorkflowReferences` | `POST /api/workflow_api/workflow_references` | `workflow_id` | `space_id` | `workflow_list[]` | OK | 是 | 是 | 是 | 是 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `PublishWorkflow` | `POST /api/workflow_api/publish` | `workflow_id` | `commit_id`, `version_description` | `publish_commit_id`, `success` | OK | 是 | 是 | 是 | 部分 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `GetWorkflowUploadAuthToken` | `POST /api/workflow_api/upload/auth_token` | 无 | `scene` | `token`, `expire_time` 等上传字段 | Partial | 是 | 部分 | 是 | 否 |
| workflow editor | `@coze-workflow/playground-adapter` | WorkflowApi | `SignImageURL` | `POST /api/workflow_api/sign_image_url` | `uri` | `Scene` | `signed_url`/兼容 data | Partial | 是 | 部分 | 是 | 否 |
| foundation / space | `@coze-foundation/space-store` | PlaygroundApi | `GetSpaceListV2` | `POST /api/playground_api/space/list` | 无 | `page`, `size`, `search_word` | `bot_space_list[]`, `recently_used_space_list[]`, `has_more` | OK | 是 | 是 | 是 | 是 |
| foundation / space | `@coze-foundation/space-store` | PlaygroundApi | `SaveSpaceV2` | `POST /api/playground_api/space/save` | `name` 或 `space_id` | `description`, `icon_uri`, `space_type` | `id`, `check_not_pass` | OK | 是 | 是 | 是 | 部分 |
| foundation / space | `@coze-foundation/space-store` | PlaygroundApi | `GetSpaceInfo` | `POST /api/playground_api/space/info` | 无 | `space_id` | `data.id`, `data.role_type`, `data.space_type` | OK | 是 | 是 | 是 | 是 |
| foundation / space | `@coze-foundation/space-store` | DeveloperApi | `GetSpaceList` | `POST /api/space/list` | 无 | `page`, `size`, `search_word` | `bot_space_list[]` | OK | 是 | 是 | 是 | 是 |
| foundation / space | `@coze-foundation/foundation-sdk` | DeveloperApi | `GetSpaceInfo` | `POST /api/space/info` | 无 | `space_id` | `data.id`, `data.role_type` | OK | 是 | 是 | 是 | 是 |
| workspace adapter | `@coze-studio/workspace-adapter` | PlaygroundApi | `OpenSpaceList` | `GET /v1/workspaces` | 无 | `page_num`, `page_size`, `enterprise_id` | `workspaces[]`, `total_count` | OK | 是 | 是 | 是 | 部分 |
| workspace adapter | `@coze-studio/workspace-adapter` | PlaygroundApi | `OpenCreateSpace` | `POST /v1/workspaces` | `Name` | `Description`, `coze_account_id` | `id` | OK | 是 | 是 | 是 | 部分 |
| workspace adapter | `@coze-studio/workspace-adapter` | PlaygroundApi | `OpenSpaceMemberList` | `GET /v1/workspaces/{workspaceId}/members` | `workspaceId` | `page_num`, `page_size` | `items[]`, `total_count` | OK | 是 | 是 | 是 | 部分 |
| workspace adapter | `@coze-studio/workspace-adapter` | PlaygroundApi | `OpenAddSpaceMember` | `POST /v1/workspaces/{workspaceId}/members` | `workspaceId`, `users[]` | 无 | `added_success_user_ids[]` | OK | 是 | 是 | 是 | 部分 |
| workspace adapter | `@coze-studio/workspace-adapter` | PlaygroundApi | `OpenUpdateSpaceMember` | `PUT /v1/workspaces/{workspaceId}/members/{userId}` | `workspaceId`, `userId` | `role_type` | `code/msg` | OK | 是 | 是 | 是 | 部分 |
| workspace adapter | `@coze-studio/workspace-adapter` | PlaygroundApi | `OpenRemoveSpace` | `DELETE /v1/workspaces/{workspaceId}` | `workspaceId` | 无 | `code/msg` | OK | 是 | 是 | 是 | 部分 |
| foundation / account | `@coze-foundation/account-adapter` | passport | `PassportAccountInfoV2` | `POST /api/passport/account/info/v2/` | 无 | 无 | `data.user_id_str`, `data.name`, `data.user_unique_name` | OK | 是 | 是 | 是 | 是 |
| foundation / account | `@coze-foundation/account-adapter` | passport | `PassportWebLogoutGet` | `GET /api/passport/web/logout/` | 无 | `next` | `redirect_url` | OK | 是 | 是 | 是 | 部分 |
| foundation / account | `@coze-foundation/account-adapter` | passport | `UserUpdateAvatar` | `POST /api/web/user/update/upload_avatar/` | `avatar(form-data)` | 无 | `data.web_uri` | OK | 是 | 是 | 是 | 否 |
| foundation / account | `@coze-foundation/account-adapter` | passport | `UserUpdateProfile` | `POST /api/user/update_profile` | 无 | `name`, `description`, `locale` | `code`, `msg` | OK | 是 | 是 | 是 | 否 |
| developer / bot list | `@coze-arch/bot-api` | DeveloperApi | `GetDraftBotList` | `POST /api/draftbot/get_draft_bot_list` | 无 | `page`, `size`, `space_id`, `name` | `total`, `list[]`, `has_more` | OK | 是 | 是 | 是 | 部分 |
| developer / bot list | `@coze-arch/bot-api` | DeveloperApi | `GetDraftBotDisplayInfo` | `POST /api/draftbot/get_display_info` | `bot_id` | `space_id` | `bot_id`, `name`, `publish_status` | OK | 是 | 是 | 是 | 部分 |
| developer / bot action | `@coze-arch/bot-api` | DeveloperApi | `DeleteDraftBot` | `POST /api/draftbot/delete` | `bot_id`, `space_id` | 无 | `code`, `msg`, `data` | OK | 是 | 是 | 是 | 否 |
| developer / bot action | `@coze-arch/bot-api` | DeveloperApi | `DuplicateDraftBot` | `POST /api/draftbot/duplicate` | `bot_id`, `space_id` | 无 | `data.bot_id`, `data.name`, `data.user_info` | OK | 是 | 是 | 是 | 否 |
| developer / upload | `@coze-arch/bot-api` | DeveloperApi | `UploadBotFile` | `POST /api/bot/upload_file` | 无 | 文件/上下文参数 | `file_id`, `file_url` | Partial | 部分 | 部分 | 是 | 否 |
| library | `@coze-arch/bot-api` | PluginDevelopApi | `LibraryResourceList` | `POST /api/plugin_api/library_resource_list` | `space_id` | `name`, `res_type_filter`, `publish_status_filter`, `size`, `cursor` | `resource_list[]`, `cursor`, `has_more` | Partial | 是 | 部分 | 是 | 否 |
| library | `@coze-arch/bot-api` | PluginDevelopApi | `DelPlugin` | `POST /api/plugin_api/del_plugin` | `plugin_id` | 无 | `code`, `msg` | OK | 是 | 是 | 是 | 否 |
| library | `@coze-arch/bot-api` | PlaygroundApi | `DeletePromptResource` | `POST /api/playground_api/delete_prompt_resource` | `prompt_resource_id` | 无 | `code`, `msg` | OK | 是 | 是 | 是 | 否 |

## 第二批及以后

以下页面/能力已纳入后续批次，但当前不以“兼容层可删除”视为完成：

- `@coze-studio/workspace-adapter` 的 `develop` / `library` 深层资源页
- `@coze-agent-ide/*` 智能体编辑与发布主链
- `@coze-community/explore` / `@coze-community/components`
- marketplace favorite / usage / plugin/tool 深层接口
- intelligence 域的真实项目模型与任务处理链路

在这些能力补齐前，不得把对应 Atlas 业务页整体删除。
