# Coze 第一阶段 Mock API 协议表

本文件是 `src/frontend/apps/app-web/src/services/mock/` 中 mock 服务的契约草案，**也是后端 C# Controller 第二/三阶段的实现依据**。前后端一切对象都以 `workspace_id` 维度组织。

## 0. 信封约定

- 所有响应统一使用 `ApiResponse<T>`：

```ts
interface ApiResponse<T> {
  success: boolean;
  code: "SUCCESS" | "VALIDATION_ERROR" | "UNAUTHORIZED" | "FORBIDDEN" | "NOT_FOUND" | "SERVER_ERROR";
  message: string;
  traceId: string;
  data: T | null;
}
```

- 分页统一使用 `PagedResult<T>`：

```ts
interface PagedRequest {
  pageIndex?: number; // 1-based，默认 1
  pageSize?: number;  // 默认 10，最大 100
  keyword?: string;
}

interface PagedResult<T> {
  pageIndex: number;
  pageSize: number;
  total: number;
  items: T[];
}
```

- 错误模型（mock 内部直接 `throw`，包含 `code`、`message`、可选 `details`）：

```ts
interface ApiError {
  code: "VALIDATION_ERROR" | "UNAUTHORIZED" | "FORBIDDEN" | "NOT_FOUND" | "SERVER_ERROR";
  message: string;
  traceId: string;
  details?: Record<string, string[]>;
}
```

- Headers：所有写接口必须携带 `Authorization: Bearer <token>` + `X-Tenant-Id: <guid>`；mock 阶段不强制校验。

## 1. 工作空间首页（PRD 01）

| Method | Path | 入参 | 返回 |
|---|---|---|---|
| GET | `/api/v1/workspaces/{workspaceId}/home/banner` | path | `HomeBanner` |
| GET | `/api/v1/workspaces/{workspaceId}/home/tutorials` | path | `TutorialCard[]` |
| GET | `/api/v1/workspaces/{workspaceId}/home/announcements` | path + `tab=all\|notice`、`pageIndex`、`pageSize`、`keyword` | `PagedResult<AnnouncementItem>` |
| GET | `/api/v1/workspaces/{workspaceId}/home/recommended-agents` | path | `RecommendedAgentItem[]` |
| GET | `/api/v1/workspaces/{workspaceId}/home/recent-activities` | path | `RecentActivityItem[]`（按 currentUser + workspaceId） |

DTO：见 [`api-home-content.mock.ts`](../src/frontend/apps/app-web/src/services/mock/api-home-content.mock.ts)。

## 2. 项目开发-文件夹（PRD 03）

| Method | Path | 入参 | 返回 |
|---|---|---|---|
| GET | `/api/v1/workspaces/{workspaceId}/folders` | path + `pageIndex`、`pageSize`、`keyword` | `PagedResult<FolderListItem>` |
| POST | `/api/v1/workspaces/{workspaceId}/folders` | body `FolderCreateRequest` | `{ folderId }` |
| PATCH | `/api/v1/workspaces/{workspaceId}/folders/{folderId}` | body `FolderUpdateRequest` | 204 |
| DELETE | `/api/v1/workspaces/{workspaceId}/folders/{folderId}` | path | 204 |
| POST | `/api/v1/workspaces/{workspaceId}/folders/{folderId}/items` | body `{ itemType, itemId }` | 204 |

校验：`name` 必填 1~40 字符；`description` 最多 800 字符。

## 3. 任务中心（PRD 02-7.4）

| Method | Path | 入参 | 返回 |
|---|---|---|---|
| GET | `/api/v1/workspaces/{workspaceId}/tasks` | path + `status`、`type`、`pageIndex`、`pageSize`、`keyword` | `PagedResult<TaskItem>` |
| GET | `/api/v1/workspaces/{workspaceId}/tasks/{taskId}` | path | `TaskDetail` |

`status` ∈ `pending | running | succeeded | failed`；`type` ∈ `workflow | batch | evaluation | publish`。

## 4. 效果评测（PRD 02-7.5 + PRD 05-4.8）

| Method | Path | 入参 | 返回 |
|---|---|---|---|
| GET | `/api/v1/workspaces/{workspaceId}/evaluations` | path + `pageIndex`、`pageSize` | `PagedResult<EvaluationItem>` |
| GET | `/api/v1/workspaces/{workspaceId}/evaluations/{evaluationId}` | path | `EvaluationDetail` |
| GET | `/api/v1/workspaces/{workspaceId}/testsets` | path + `pageIndex`、`pageSize`、`keyword` | `PagedResult<TestsetItem>` |
| POST | `/api/v1/workspaces/{workspaceId}/testsets` | body `TestsetCreateRequest` | `{ testsetId }` |

`TestsetCreateRequest.rows` 是按开始节点 schema 生成的二维表数据。

## 5. 发布渠道（PRD 04-4.6）

| Method | Path | 入参 | 返回 |
|---|---|---|---|
| GET | `/api/v1/workspaces/{workspaceId}/publish-channels` | path + `pageIndex`、`pageSize`、`keyword` | `PagedResult<PublishChannelItem>` |
| POST | `/api/v1/workspaces/{workspaceId}/publish-channels` | body `PublishChannelCreateRequest` | `{ channelId }` |
| PATCH | `/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}` | body `PublishChannelUpdateRequest` | 204 |
| POST | `/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/reauth` | path | 204 |
| DELETE | `/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}` | path | 204 |

`type` ∈ `web-sdk | open-api | wechat | feishu | lark | custom`。`supportedTargets` ⊂ `agent | app | workflow`。

发布管理-智能体/应用/工作流三个 Tab 的列表数据**复用**真实接口：
- 智能体 → `getAiAssistantPublications`
- 应用 → `getAiAppPublishRecords`
- 工作流 → `listWorkflows`（按已发布过滤）

## 6. 平台生态（PRD 02-7.7~7.12）

| Method | Path | 入参 | 返回 |
|---|---|---|---|
| GET | `/api/v1/market/templates/summary` | `pageIndex`、`pageSize`、`keyword` | `PagedResult<MarketCategorySummary>` |
| GET | `/api/v1/market/plugins/summary` | `pageIndex`、`pageSize`、`keyword` | `PagedResult<MarketCategorySummary>` |
| GET | `/api/v1/community/works` | `pageIndex`、`pageSize`、`keyword` | `PagedResult<CommunityWorkItem>` |
| GET | `/api/v1/open/api-keys` | — | `OpenApiKeyItem[]` |
| POST | `/api/v1/open/api-keys` | body `{ alias }` | `{ key, item: OpenApiKeyItem }` |
| DELETE | `/api/v1/open/api-keys/{keyId}` | path | 204 |
| GET | `/api/v1/platform/general/notices` | — | `PlatformNoticeItem[]` |
| GET | `/api/v1/platform/general/branding` | — | `PlatformBranding` |

## 7. 个人主页与设置（PRD 03 头像入口）

| Method | Path | 入参 | 返回 |
|---|---|---|---|
| GET | `/api/v1/me/profile` | — | 复用现有 `getProfile` |
| PATCH | `/api/v1/me/profile` | body | 复用 `saveProfile` |
| POST | `/api/v1/me/password` | body | 复用 `savePassword` |
| GET | `/api/v1/me/settings/general` | — | `MeGeneralSettings` |
| PATCH | `/api/v1/me/settings/general` | body | `MeGeneralSettings` |
| GET | `/api/v1/me/settings/publish-channels` | — | `MePublishChannelItem[]` |
| GET | `/api/v1/me/settings/datasources` | — | `MeDataSourceItem[]` |
| DELETE | `/api/v1/me/account` | — | 204 |

## 8. 第二批 Mock 预告（仅占位，本阶段不实现）

- 添加工作流弹窗：`GET /api/v1/workspaces/{workspaceId}/workflows?source=resource-library`（**复用** `listWorkflows`）
- 创建工作流 / 对话流：复用 `createWorkflow`
- 工作流编辑器骨架：复用 `@coze-workflow/playground-adapter`
- 测试集 UI：`POST /api/v1/workspaces/{workspaceId}/testsets`（已在 §4 定义）

## 9. 第三阶段后端落点（C# Controllers）

每个 mock 文件对应 1 个 Controller：

| Mock 文件 | 后端 Controller |
|---|---|
| `api-home-content.mock.ts` | `Atlas.AppHost/Controllers/HomeContentController.cs` |
| `api-folders.mock.ts` | `Atlas.AppHost/Controllers/WorkspaceFoldersController.cs` |
| `api-tasks.mock.ts` | `Atlas.AppHost/Controllers/WorkspaceTasksController.cs` |
| `api-evaluations.mock.ts` | `Atlas.AppHost/Controllers/EvaluationsController.cs` |
| `api-publish-channels.mock.ts` | `Atlas.AppHost/Controllers/PublishChannelsController.cs` |
| `api-templates-market.mock.ts` | 复用 `Atlas.PlatformHost/Controllers/MarketController.cs`（或新增） |
| `api-community.mock.ts` | `Atlas.PlatformHost/Controllers/CommunityController.cs` |
| `api-platform-general.mock.ts` | `Atlas.PlatformHost/Controllers/PlatformGeneralController.cs` |
| `api-me-settings.mock.ts` | `Atlas.PlatformHost/Controllers/MeSettingsController.cs` |

每个端点同步：
- `.http` 文件覆盖请求示例
- `docs/contracts.md` 增补对应章节
- DTO / Response 强类型定义
