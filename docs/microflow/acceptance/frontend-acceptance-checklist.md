# 微流前端验收清单（手工）

在 `src/frontend` 执行 `pnpm dev:app-web`，按顺序勾选。

1. 工作区 → 资源库 → 切换到 **微流** Tab，列表/表格与搜索正常。
2. **新建微流**：填写名称/模块/标签，创建成功并可跳转编辑器（若已接路由）。
3. **参数**：在编辑器中打开微流设置/参数区，增删参数（与实现一致即可）。
4. **模板**：节点面板「模板」若占位，应显示「即将支持」类文案，无 `NotImplemented`。
5. **编辑器**：从资源库进入，画布、节点板、属性、问题、调试面板可打开。
6. **拖节点、连线、属性**：与现有 MicroflowEditor 能力一致。
7. **决策分支 / 变量 / 表达式**：配置后校验无异常崩溃。
8. **校验**：工具栏校验；ProblemPanel 显示 `MicroflowValidationIssue`。
9. **测试运行**：打开测试运行/调试，Mock 轨迹可显示（不崩）。
10. **保存**：保存后 Local Adapter 下刷新仍存在。
11. **发布**：HTTP 模式下调用 `POST /api/microflows/{id}/publish`；成功后状态标签变为 published，重复版本显示 `MICROFLOW_VERSION_CONFLICT`。
12. **版本 / 引用 / 回滚 / 复制版本**：VersionsDrawer / VersionDetailDrawer 走真实 versions API；rollback 后资源回到 draft，duplicate version 生成新草稿资源。
13. **大样例**：在开发环境调用 `createLargeMicroflowSample(120)` 或通过内部 API 打开，不白屏、明显卡顿可接受范围。
14. **缩放 150%**：浏览器缩放，主布局不重叠至不可用。
15. **快捷键**：与 microflow 包内快捷键表一致，无与全局热键严重冲突。
16. **app-web 边界**：除路由与 `workspaceId` 外，不手写 schema 校验/Runtime DTO（见代码检索）。
17. **其他 Tab / 工作流 / Coze**：不回归破坏资源库其他 Tab 与工作流/Coze 入口。
18. **Adapter Mode**：`VITE_MICROFLOW_ADAPTER_MODE=mock/local/http` 均可创建 bundle；http 模式缺少 `VITE_MICROFLOW_API_BASE_URL` 时应明确报配置错误。
19. **HTTP 错误态**：错误 API base url 时资源库显示“微流服务未连接”并可重试；编辑器 404/403/409/500 显示明确错误和返回资源库入口。
20. **边界检查脚本**：执行 `pnpm run verify:microflow-adapter-modes`，确认 app-web 不直接 fetch、不直接触碰微流 localStorage、不 import adapter 内部实现。
21. **生产禁 mock**：执行 `pnpm run verify:microflow-no-production-mock`，确认生产策略默认 http、禁止 mock/local/fallback，HTTP adapter 不 import mock/local。
22. **模式提示**：开发环境资源库工具栏和编辑器 header 可看到 `mock/local/http` 当前模式；mock/local 显示“本地模拟数据”，http 显示 base url。
23. **HTTP 错误处理**：执行 `pnpm run verify:microflow-http-error-handling`；401/403 回调触发，404/409/422/network 均有明确 UI。
24. **ProblemPanel 桥接**：后端返回 `error.validationIssues` 时，保存/校验/发布/test-run 能进入 ProblemPanel，不只 toast。
25. **抽屉错误态**：VersionsDrawer / ReferencesDrawer API 失败显示错误态和重试按钮，不显示假空数据。
26. **Resource / Schema 真实联调**：执行 `pnpm run verify:microflow-resource-schema-integration`，覆盖 health、list、create、detail、schema load/save、rename、favorite、duplicate、archive、restore、delete、404、schema invalid、version conflict、archived save blocked。
27. **HTTP 模式无回退**：`app-web` 微流资源库和编辑器默认 `mode=http`、`apiBaseUrl=/api`；断开后端时显示服务错误，不显示 mock sample 或 localStorage 数据。
26. **HTTP Metadata**：`VITE_MICROFLOW_ADAPTER_MODE=http` 时 MetadataProvider 调用 `/api/microflow-metadata`，EntitySelector / AttributeSelector / EnumerationSelector / MicroflowSelector 显示后端 seed/cache 与真实 resource 生成的数据。
27. **HTTP Validation**：ValidationAdapter 调用 `/api/microflows/{id}/validate`；制造 missing start、invalid metadata reference、missing action field 后，ProblemPanel 显示后端 `MicroflowValidationIssue`，字段错误按 `fieldPath` 定位。
28. **HTTP References / Impact**：创建 A/B 两个微流，A 的 CallMicroflowAction 指向 B；保存 A 后 `GET /api/microflows/{B}/references` 在 ReferencesDrawer 显示来自 A 的 `callMicroflow` 引用。发布 B 后修改参数或 returnType，PublishModal 的 impact API 显示 high breaking change；未确认发布被阻止，确认后发布成功。被引用的 B 删除/归档返回 `MICROFLOW_REFERENCE_BLOCKED`。
26. **P0 属性面板**：Retrieve/CreateObject/ChangeMembers/Commit/Delete/Rollback/CreateVariable/ChangeVariable/CallMicroflow/RestCall/LogMessage 均使用强类型字段，不出现 generic config 或 raw JSON dump。
27. **字段级错误**：清空输出变量、REST URL、CallMicroflow 参数或 Loop iterator 时，字段下方显示对应 `ValidationIssue.fieldPath`。
28. **变量联动**：修改 Retrieve/CreateObject/CreateVariable/CallMicroflow/RestCall 输出变量后，下游 VariableSelector 可见，重复名提示错误。
26. **FlowGram 同步**：修改 caption、REST method/url、Log level、CallMicroflow target、输出变量、Loop iterator 后，节点标题或副标题同步更新且视口不跳回原点。
27. **变量作用域 v2**：Retrieve 输出在节点前不可见、节点后可见；Decision 分支变量在 Merge 后显示 maybe；Loop 内可见 iterator 与 `$currentIndex`，Loop 外不可见。
28. **ErrorHandler 变量**：REST error handler 内可见 `$latestError` 与 `$latestHttpResponse`，主路径不可见；WebService error handler 内可见 `$latestSoapFault`。
29. **VariableSelector v2**：显示变量名、类型、来源、scope、visibility、readonly/unknown tag；ChangeVariable 不显示 readonly/system，Commit/Delete/Rollback 只显示 object/list。
30. **ExpressionEditor v2**：插入菜单使用同一 VariableIndex；object 可插属性，list<object> 提示需循环访问成员，maybe/unknown 有提示。
31. **Runtime 契约**：`toRuntimeDto().variables.all` 与 `toExecutionPlan().variableDeclarations` 非空且数量一致。
32. **Expression v2**：`$order/Status = Sales.OrderStatus.New`、`not empty($order)`、`if $order/TotalAmount > 100 then true else false` 可解析并显示 inferredType。
33. **Validator mode**：edit/save/publish/testRun 四种模式下 severity 差异符合 `validation-contract.md`，testRun 对 modeledOnly 报 error。
34. **P0 表达式字段**：Retrieve custom range、REST form body、LogMessage arguments、CallMicroflow mappings 均进入统一 `validateExpressions`。
35. **ProblemPanel 联动**：点击 issue 选中 object/flow；字段错误通过稳定 `fieldPath` 在属性面板显示。
36. **FlowGram port 协议**：保存/刷新后 edge 仍连接同一端口，`sourcePortID`/`targetPortID` 可由 `{objectId}:{portKind}:{connectionIndex}` 解析。
37. **Edge 协议**：Decision/ObjectType/ErrorHandler/AnnotationFlow 的 `caseValues`、`isErrorHandler`、`label`、`branchOrder` 在保存、校验、AutoLayout 后不丢失。
38. **Loop 边界**：root → loop internal、loop internal → root 被拒绝；同一 loop internal collection 内部可连；Break/Continue 无出边。
39. **Runtime edge**：`toRuntimeDto().flows` 与 `toExecutionPlan().flows` 不包含 AnnotationFlow，且 plan 提供 normal/decision/errorHandler flow 分组。
40. **AutoLayout 语义**：AutoLayout 前后 flow semantic hash 一致，case/errorHandler/annotation 类型不变化。
41. **Runtime Pipeline**：编辑器 test-run 走 `toRuntimeDto → toExecutionPlan → mockRunExecutionPlan`，RunSession/TraceFrame 不含 FlowGram JSON。
42. **Runtime 回归矩阵**：`sample-runtime-matrix.md` 中所有样例可完成 validate、FlowGram 投影、Runtime DTO、ExecutionPlan 与 mock run 契约验证。

## 自动化补充

- `pnpm --filter @atlas/mendix-studio-core run verify-contracts`
- `pnpm --filter @atlas/microflow run typecheck`
- `pnpm run verify:microflow-adapter-modes`
- `pnpm run verify:microflow-no-production-mock`
- `pnpm run verify:microflow-http-error-handling`
- `pnpm run verify:microflow-contract-mock`

## Contract Mock HTTP 模式

1. 设置 `VITE_MICROFLOW_API_MOCK=msw`、`VITE_MICROFLOW_ADAPTER_MODE=http`、`VITE_MICROFLOW_API_BASE_URL=/api`，启动 `pnpm run dev:app-web`。
2. ResourceTab 通过 HTTP mock 加载资源列表，支持搜索、状态、发布状态、收藏、模块、标签、排序与分页。
3. 新建微流后进入 EditorPage，刷新详情与 schema 不丢。
4. 保存 schema 走 `PUT /api/microflows/{id}/schema`，已发布资源保存后变为 `changedAfterPublish`。
5. Metadata selectors 走 `GET /api/microflow-metadata` 及 entity/enumeration 子路径。
6. 手动校验走 `POST /api/microflows/{id}/validate`，ProblemPanel 展示返回 issues。
7. PublishModal 走 publish API；validation error、high impact、重复版本均被阻止。
8. VersionsDrawer / VersionDetailDrawer 走 versions API，rollback 与 duplicate version 可用。
9. ReferencesDrawer 走 references API，PublishModal impact summary 走 impact API。
10. DebugPanel 走 test-run、get run、trace、cancel API，展示 RunSession 与 trace/logs。
11. 使用 `x-microflow-mock-error` 或 `?mockError=` 分别验证 404、403、409、validation failed、publish blocked、runtime service unavailable 与 network-like error。

## 第 42 轮真实后端 TestRun 验收

1. `VITE_MICROFLOW_ADAPTER_MODE=http` 且 `apiBaseUrl=/api` 时，点击测试运行命中 `POST /api/microflows/{id}/test-run`，返回 `data.session`。
2. DebugPanel 展示后端 `RunSession`，包括 `trace`、`logs`、`variables` 与 failed `error.code`。
3. FlowGram 高亮使用后端 trace 的 `objectId`、incoming/outgoing flow、`actionId`；Decision 显示 `selectedCaseValue`，Loop 显示 `loopIteration.index`。
4. `simulateRestError=true` 的 RestCall 样例应显示 failed rest frame 和 error handler 后续 frame；LogMessage 应出现在 logs。
5. `GET /api/microflows/runs/{runId}` 与 `/trace` 可刷新同一 run；cancel API 可返回 cancelled。
6. validation failed test-run 应进入 ProblemPanel，不生成 success session。

## 第 46～47 轮 Publish / Version / References + Debug 验收

1. PublishModal 使用后端 `mode=publish` validation 与 impact API；high impact 未确认禁止发布。
2. VersionsDrawer / VersionDetailDrawer 使用真实 versions/detail/rollback/duplicate/compare-current API。
3. ReferencesDrawer 支持 includeInactive/sourceType/impactLevel 后端筛选与 sourceName 搜索。
4. ResourceCard / ResourceTable / EditorHeader 显示 `status`、`publishStatus`、`latestPublishedVersion`、`changedAfterPublish`。
5. TestRunModal 使用后端 `mode=testRun` precheck，DebugPanel 使用持久化 get run/get trace，Cancel Run 可用。
6. FlowGram runtime highlight 使用后端 trace 的 object/flow/errorHandler/loop/decision 字段，清除运行不修改 AuthoringSchema。
