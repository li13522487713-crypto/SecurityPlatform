# Publish / Version / References 联调说明

## 范围

本轮覆盖 PublishMicroflowModal、MicroflowVersionsDrawer、MicroflowVersionDetailDrawer、MicroflowReferencesDrawer、ResourceCard / ResourceTable / EditorHeader 发布状态，以及 HTTP ResourceAdapter 的 publish / version / references / impact 方法。

依赖后端已完成第 38 轮 Version / Publish Snapshot 与第 41 轮 References / Impact Analysis。前端必须配置 `adapterMode=http`，`app-web` 只传 `adapterConfig`，由 `MicroflowApiClient` 解析 `MicroflowApiResponse<T>`。

## 联调步骤

1. 配置 `apiBaseUrl=/api` 或 `http://localhost:5002/api`，打开微流编辑器。
2. 保存 AuthoringSchema，不保存 FlowGram JSON。
3. 打开 PublishModal，前端调用 ValidationAdapter `mode=publish`，再调用 `GET /api/microflows/{id}/impact?version=&includeBreakingChanges=true&includeReferences=true`。
4. validation error 禁止发布；warning 允许发布；high impact 必须勾选确认。
5. 点击发布调用 `POST /api/microflows/{id}/publish`，成功后刷新 resource、版本列表和发布状态。
6. VersionsDrawer 调用 versions/detail/compare-current，rollback 只创建新的 current schema snapshot，不修改历史 snapshot。
7. Duplicate version 创建新 draft resource，`publishStatus=neverPublished`，schema 来源于历史 snapshot。
8. ReferencesDrawer 调用 references API，并下推 `includeInactive/sourceType/impactLevel`；搜索只在当前结果内做 sourceName 过滤。

## 规则

- high impact breaking change 以后端最终裁决为准，未确认返回 `MICROFLOW_PUBLISH_BLOCKED`。
- 参数删除、参数类型变更、returnType 变更为 high；exposed url path 变更为 medium。
- `changedAfterPublish` 由已发布资源保存 schema 后触发；发布成功重置为 `published`。
- 引用重建基于 AuthoringSchema；不会修改 schema。

## 不覆盖

本联调不覆盖 TestRun / Debug、真实 Runtime、真实数据库 CRUD 执行、真实 REST 调用、完整页面/工作流引用系统和权限系统深度逻辑。

## 验证

- 手工：`src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` 的 Publish / Version / References 段。
- 自动：`scripts/verify-microflow-publish-version-references-testrun-debug-integration.ts`。

## 第 60 轮回归补充

- Round60 总控脚本继续调用 `scripts/verify-microflow-publish-version-references-testrun-debug-integration.ts`，并把结果纳入 blocker/critical 门禁。
- `.http` 顶部 Round60 区域提供 Publish / Version / References / Impact 的最小可执行段落。
- 失败时需区分 `MICROFLOW_VERSION_CONFLICT`、`MICROFLOW_PUBLISH_BLOCKED`、`MICROFLOW_REFERENCE_BLOCKED` 与服务不可用，不能用 409 默认文案掩盖具体语义。
