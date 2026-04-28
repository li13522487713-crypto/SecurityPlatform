# Microflow Frontend Release Completeness

本文基于当前源码审查结果，记录 `/space/:workspaceId/mendix-studio/:appId` 发布路径的前端完备性。目标路径必须使用 HTTP adapter、真实 `MicroflowResource` API 与真实 `microflow-metadata`，不得通过孤立 demo、localStorage adapter 或 mock metadata 绕过。

## Summary

- 已具备：目标路由、workspace/appId 传递、HTTP adapter 默认装配、App Explorer 真实列表、loading/empty/error、搜索、CRUD 基础错误处理、多 tab、dirty/close/refresh guard、schema load/save、409 保存冲突、editor remount、validate/publish/test-run/trace/references UI 基础能力。
- 需补齐：`?microflowId=` 深链恢复、`canCreate/canRun` 权限态、缺省权限保守化、独立编辑器强制 HTTP、错误归一化字段扩展、Playwright E2E、包级 i18n、生产路径 mock/local 的验证脚本强化。
- 禁止项：正式 mendix-studio 路径不得使用 `createLocalMicroflowApiClient`、`localStorage` adapter、`mock-metadata`、固定 test-run 返回值或 `Sales.*` 默认值。

## Release Completeness Matrix

| 前端能力 | 当前实现 | 源码证据 | 是否发布可用 | 缺口 | 需要修改的文件 | 验收方式 |
|---|---|---|---|---|---|---|
| 1. Route context | 已注册 `/space/:workspaceId/mendix-studio/:appId` 子路由。 | `src/frontend/apps/app-web/src/app/app.tsx` | 是 | 无 | 无 | 打开目标路由进入 Studio |
| 2. Workspace / tenant / appId 传递 | `mendix-studio-route.tsx` 从 `useParams` 和 `useWorkspaceContext` 传入。 | `src/frontend/apps/app-web/src/app/pages/mendix-studio-route.tsx` | 是 | 独立 editor route 未传 requestHeaders | `src/frontend/apps/app-web/src/app/pages/microflow-editor-page.tsx` | Network header 含 workspace/tenant/token |
| 3. AdapterBundle 创建与注入 | 默认 `mode=http`，MSW 也保持 HTTP client。 | `src/frontend/apps/app-web/src/app/microflow-adapter-config.ts` | 是 | 需强化独立 editor HTTP guard | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorPage.tsx` | 正式路径非 HTTP 时显示错误 |
| 4. App Explorer Microflows 真实列表 | `listMicroflows` 走 resourceAdapter API。 | `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 是 | 无 | 无 | Network 看到 `/api/v1/microflows` |
| 5. App Explorer loading / empty / error | `createMicroflowStateChildren` 已实现。 | `src/frontend/packages/mendix/mendix-studio-core/src/components/microflow-tree-section.tsx` | 是 | 文案需 i18n | `src/frontend/packages/mendix/mendix-studio-core/src/i18n/copy.ts` | 三态可见 |
| 6. App Explorer 搜索过滤 | `matchesNode` 过滤 label/name/qualifiedName。 | `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer-tree.tsx` | 是 | 无 | 无 | 搜索返回匹配项 |
| 7. Microflows CRUD | create/rename/duplicate/delete 均存在。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/*Modal.tsx` | 是 | 权限字段不全 | `resource-types.ts`, `resource-utils.ts` | CRUD 成功刷新树 |
| 8. 创建失败错误处理 | Modal 内 `setSubmitError` + `Toast.error`，失败不关闭。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/CreateMicroflowModal.tsx` | 是 | 错误 category 缺失 | `microflow-api-error.ts` | 重名创建弹 inline error |
| 9. 重命名失败错误处理 | `Toast.error(getMicroflowErrorUserMessage(caught))`。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/RenameMicroflowModal.tsx` | 部分 | 缺 inline traceId | `RenameMicroflowModal.tsx` | 409/422 显示明确原因 |
| 10. 复制失败错误处理 | `DuplicateMicroflowModal` catch 显示 Toast。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/DuplicateMicroflowModal.tsx` | 部分 | 缺 inline traceId | `DuplicateMicroflowModal.tsx` | 重名复制不关闭且可修改 |
| 11. 删除失败错误处理 | 409 引用保护会打开 references。 | `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 是 | 文案需 i18n | `app-explorer.tsx` | 删除被引用微流被阻止 |
| 12. 右键菜单 | AppExplorerTree 接收 rename/duplicate/delete handler。 | `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer-tree.tsx` | 是 | 权限态需补齐 tooltip | `app-explorer-tree.tsx` | 无权限菜单项 disabled |
| 13. 查找引用 | References drawer/toolbar 已存在。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/__tests__/microflow-references.test.ts` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 右键可查看 inbound references |
| 14. Workbench Tabs | tab key 为 `microflow:${id}`，重复打开防止。 | `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 是 | 无 | 无 | A/B/C 可同时打开 |
| 15. 多微流隔离 | editor host/key 按 active tab remount。 | `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 是 | 需 E2E | `src/frontend/e2e/app/*` | A 修改不污染 B |
| 16. Dirty state | `saveStateByMicroflowId` + `dirtyByWorkbenchTabId`。 | `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 是 | 无 | 无 | 未保存 tab 有 dirty 标记 |
| 17. Close guard | 关闭 dirty tab 弹确认。 | `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 是 | 文案 i18n | `workbench-tabs.tsx` | 关闭未保存 tab 提示 |
| 18. Refresh guard | `beforeunload` 检查 dirty/saving/queued。 | `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 是 | 无 | 无 | 刷新前浏览器提示 |
| 19. Schema loading | `MicroflowResourceEditorHost` HTTP 加载 schema。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/MicroflowResourceEditorHost.tsx` | 是 | not-found 深链 UI 需补 | `MicroflowResourceEditorHost.tsx`, `index.tsx` | 刷新后加载 schema |
| 20. Schema save | `saveMicroflowSchema` 带 baseVersion/schemaId/version。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx` | 是 | 无 | 无 | 保存成功更新时间 |
| 21. Save conflict | 409 进入 conflict state + Modal。 | `MendixMicroflowEditorEntry.tsx` | 部分 | Cancel 按钮无实际关闭逻辑 | `MendixMicroflowEditorEntry.tsx` | 409 可 reload/keep/force/cancel |
| 22. Editor remount key | `key={activeMicroflowTabId}` 与 `key={id:schemaId:version}`。 | `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 是 | 无 | 无 | 切 tab 重挂编辑器 |
| 23. Local adapter 禁用 | Studio Host 拒绝非 HTTP。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/MicroflowResourceEditorHost.tsx` | 部分 | 独立 editor 未强制 | `MendixMicroflowEditorPage.tsx` | 生产路径 grep 无 local 保存 |
| 24. Mock metadata 禁用 | Host 要求 `metadataAdapter`，不回退 mock。 | `metadata-provider.tsx`, `MicroflowResourceEditorHost.tsx` | 部分 | 独立 editor 需一致 | `MendixMicroflowEditorPage.tsx` | Call selector 无 Sales.* |
| 25. Toolbox 分类 | 节点注册表已有分类。 | `src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts` | 是 | 去 mock 默认值复核 | `node-registry/*` | Toolbox 分类显示 |
| 26. Toolbox 搜索 | 节点面板支持搜索。 | `src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 搜索可过滤节点 |
| 27. Toolbox 去 mock 默认值 | 未发现 `MF_ValidateOrder` 默认。 | `action-registry.ts`, `mock-metadata.ts` | 部分 | 仍需 grep 保护 | `verify-microflow-no-production-mock.mjs` | 生产 path 不含 Sales.* 默认 |
| 28. Canvas drag/drop | FlowGram bridge 已存在。 | `FlowGramMicroflowCanvas.tsx` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 拖入节点成功 |
| 29. Canvas node position persistence | schema 保存包含 position/layout。 | `useFlowGramMicroflowBridge.ts` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 保存刷新节点坐标恢复 |
| 30. Canvas edge persistence | FlowGram bridge 维护 flows/edges。 | `useFlowGramMicroflowBridge.ts` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 保存刷新连线恢复 |
| 31. Canvas viewport persistence | schema viewport patch 已存在。 | `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 刷新视口恢复 |
| 32. Keyboard shortcuts | editor 支持快捷键与 undo/redo。 | `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 是 | 文档/E2E 缺失 | `docs/microflow-release-e2e-checklist.md` | Ctrl+S/Ctrl+Z 有效 |
| 33. Undo/redo per microflow | editor 本地 history 随 remount 隔离。 | `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | A undo 不影响 B |
| 34. Property panel for node | property panel forms 已存在。 | `src/frontend/packages/mendix/mendix-microflow/src/property-panel/index.tsx` | 是 | i18n 缺失 | `property-panel/*` | 节点属性保存 |
| 35. Property panel for edge | flow/edge property 编辑支持。 | `property-panel/index.tsx` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 分支 edge 保存 |
| 36. Property panel for microflow document | document-level schema 设置存在。 | `property-panel/index.tsx` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | return type 保存 |
| 37. Expression editor | 表达式编辑组件存在。 | `src/frontend/packages/mendix/mendix-microflow/src/expression/ExpressionEditor.tsx` | 是 | 后端 runtime 必须统一 evaluator | 后端 M5 | `amount > 100` 可运行 |
| 38. Variable selector | 变量 foundation/selector 存在。 | `src/frontend/packages/mendix/mendix-microflow/src/variables/*` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 可选参数变量 |
| 39. Entity selector | metadata provider 提供 entities。 | `metadata-provider.tsx` | 部分 | mock metadata 禁用后需真实 metadata 兜底 UI | `metadata-provider.tsx` | Entity selector 走 `/api/v1/microflow-metadata` |
| 40. Attribute selector | metadata catalog 支持 attributes。 | `metadata-types.ts` | 部分 | E2E 缺失 | `src/frontend/e2e/app/*` | 属性选择并保存 |
| 41. Call Microflow selector | Call Microflow form 使用 metadata microflows。 | `property-panel/forms/call-microflow-config.ts` | 是 | 确保无 Sales.* mock fallback | `metadata-adapter.ts` | selector 显示真实微流 |
| 42. Parameter mapping | CallMicroflow 参数映射测试存在。 | `property-panel/__tests__/call-microflow-config.test.ts` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | amount 映射保存 |
| 43. Return binding | CallMicroflow return binding 逻辑存在。 | `property-panel/forms/call-microflow-config.ts` | 是 | Runtime 后端需真实绑定 | 后端 M3/M5 | 运行后写入返回变量 |
| 44. Problems panel | `ProblemPanel` 消费 validation issues。 | `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 是 | 文案 i18n | `editor/index.tsx` | 点击 issue 定位节点 |
| 45. Validation issue location | `issue.objectId ?? issue.nodeId` 定位。 | `editor/index.tsx` | 是 | 后端 issue path 需稳定 | 后端 M10 | validate issue 可跳转 |
| 46. Publish UI | `PublishMicroflowModal` 存在。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/publish/PublishMicroflowModal.tsx` | 是 | 权限 canPublish/canRun 补齐 | `resource-types.ts` | 发布前校验 |
| 47. Test-run UI | `MicroflowTestRunModal` 调用 `onRun`。 | `src/frontend/packages/mendix/mendix-microflow/src/debug/MicroflowTestRunModal.tsx` | 是 | 后端去 mock 必须完成 | 后端 M2-M11 | Run 显示真实 output |
| 48. Trace panel | `MicroflowTracePanel` 展示 session.trace。 | `src/frontend/packages/mendix/mendix-microflow/src/debug/MicroflowTracePanel.tsx` | 是 | 后端 trace 字段补齐 | 后端 M10 | trace 点击定位 |
| 49. References panel | references drawer/service 已存在。 | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/*references*` | 是 | E2E 缺失 | `src/frontend/e2e/app/*` | 删除保护打开引用 |
| 50. Permission state | 部分使用 `canEdit/canPublish`。 | `resource-types.ts`, `resource-utils.ts` | 部分 | 缺 canCreate/canRun，默认过宽 | `resource-types.ts`, `resource-utils.ts`, 后端 DTO | 403 显示权限不足 |
| 51. Auth error UI | HTTP client 触发 unauthorized/forbidden event。 | `microflow-api-client.ts` | 部分 | 页面级 auth banner 需统一 | `microflow-api-error.ts`, UI 调用处 | 401/403 不崩溃 |
| 52. 401/403/409/422/500 错误区分 | helper 存在但字段不完整。 | `microflow-api-error.ts` | 部分 | 补 `category`/semantic helpers | `api-envelope.ts`, `microflow-api-error.ts` | 五类错误 UI 不混淆 |
| 53. Toast + inline error | create/save/delete 有覆盖。 | 多处 modal/editor | 部分 | rename/duplicate 缺 inline traceId | 相关 modal | traceId 可见 |
| 54. No uncaught promise | 多数 catch 已处理。 | grep 无 `console.error` | 部分 | Playwright 需监听 console/pageerror | `src/frontend/e2e/app/*` | console 无 uncaught |
| 55. E2E test coverage | 当前无 mendix/microflow E2E。 | `src/frontend/e2e/app` | 否 | 新增 4 个 spec | `src/frontend/e2e/app/*` | Playwright 通过 |
| 56. Accessibility | Semi 基础组件具备；自定义 Canvas 需测。 | Semi imports / canvas | 部分 | 补 aria-label/test ids | editor/property panel | 键盘可操作主要按钮 |
| 57. i18n 文案 | mendix 包 CJK 硬编码 400+。 | `packages/mendix/**/*` | 否 | copy.ts + baseline | `src/frontend/packages/mendix/**/i18n/copy.ts` | `pnpm i18n:check` |
| 58. Performance for 100+ nodes | FlowGram 画布具备虚拟/增量能力。 | `FlowGramMicroflowCanvas.tsx` | 部分 | 需 E2E/perf smoke | `src/frontend/e2e/app/*` | 100+ 节点可拖拽保存 |
| 59. Telemetry / logs / traceId | API error 包含 traceId，run trace 有 trace。 | `api-envelope.ts`, test-run response | 部分 | UI traceId 展示统一 | `microflow-api-error.ts`, UI | 错误 UI 可见 traceId |
| 60. Feature flag / rollout control | `VITE_MICROFLOW_ADAPTER_MODE` 与 MSW 开关存在。 | `microflow-adapter-config.ts`, `env.d.ts` | 部分 | 生产禁 mock/local 需验证脚本 | `verify-microflow-no-production-mock.mjs` | production build 不走 mock/local |

## Acceptance Gates

1. `/space/:workspaceId/mendix-studio/:appId?microflowId=xxx` 刷新后直接打开对应 tab。
2. App Explorer 列表、metadata、schema、validate、publish、references、test-run 均走真实 API。
3. 生产路径无法进入 local adapter、localStorage adapter 或 mock metadata。
4. 409/422/500/401/403 区分展示，错误 UI 可见 traceId。
5. A/B/C 多 tab 编辑互不污染，保存失败不丢 dirty。
6. Playwright 覆盖创建、保存刷新、Call Microflow、删除保护、trace、console no uncaught。
