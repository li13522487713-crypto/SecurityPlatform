# 低代码绑定矩阵（lowcode-binding-matrix）

> 状态：M09 落地。
> 范围：模式 A（表单 → 工作流 → 回填）/ 模式 B（动态选项填充）的全部黄金样本（≥ 20 用例）；与 `lowcode-workflow-adapter` 完全一致。

## 1. 模式 A 黄金样本：表单 → 工作流 → 回填

> 所有 outputMapping 的 key 为 jsonata 表达式（按工作流 outputs 求值），value 为目标变量路径或组件 prop 路径。
> 目标路径作用域守门：page / app / component（写）；其它只读作用域被忽略（编辑期 propertyPanels 阻断）。

| # | 场景 | inputMapping | outputMapping | 目标组件 |
| - | --- | --- | --- | --- |
| A1 | 搜索框 → 工作流 → 渲染 Markdown | `keyword: page.formValues.q` | `result: component.markdown-1.content` | Markdown |
| A2 | 表单 → AI 工作流 → 多字段回填 | `userId: app.currentUser.id` | `summary: page.summary, score: page.score` | Text/Number |
| A3 | 上传 → workflow → 图片回填（与模式 C 联动） | `fileHandle: page.uploaded` | `processedUrl: component.image-1.src` | Image |
| A4 | 表单 → workflow → 视频回填 | `prompt: page.prompt` | `videoUrl: component.video-1.src` | Video |
| A5 | 列表项点击 → workflow → 详情面板 | `itemId: event.payload.id` | `detail: app.currentDetail` | Drawer |
| A6 | 提交订单 → workflow → 跳转 | `order: page.order` | `redirectUrl: page.redirectUrl` | Button + navigate |
| A7 | 工作流执行结果 → Toast | `event: page.evt` | `message: app.toastMsg` | Toast |
| A8 | 工作流 → 表格分页 | `page: page.page, size: page.size` | `data: page.tableData, total: page.tableTotal` | Table |
| A9 | 工作流 → 富文本 | `q: page.q` | `html: component.markdown-1.content` | Markdown |
| A10 | 工作流 → AI 卡片 | `userQ: page.q` | `card: component.aicard-1.cardConfig` | AiCard |

## 2. 模式 B 黄金样本：动态选项填充

> 数据源 binding 与触发动作完全解耦：DataSourceBinding 描述"从哪取"，refreshDataSource 动作描述"何时取"。

| # | 组件 | 数据源 binding | 触发动作 | 备注 |
| - | --- | --- | --- | --- |
| B1 | Select.options | workflow_output(workflowId=wf-options).items | onPageLoad → refreshDataSource | 默认页面加载即取 |
| B2 | Select.options | workflow_output(wf-cascade).filter(parentId=event.payload.parentId) | onChange(parent) → refreshDataSource | 级联下拉 |
| B3 | RadioGroup.options | workflow_output(wf-categories).list | onClick(refresh) → refreshDataSource | 手动刷新 |
| B4 | CheckboxGroup.options | workflow_output(wf-tags).items | onPageLoad → refreshDataSource | 多选 |
| B5 | Filter.options | workflow_output(wf-attrs).attributes | onChange(category) → refreshDataSource | 筛选项 |
| B6 | List.items | workflow_output(wf-search) | onSubmit(searchBox) → refreshDataSource | 列表搜索 |
| B7 | Table.dataSource | workflow_output(wf-page).data | onChange(pagination) → refreshDataSource | 服务端分页 |
| B8 | WaterfallList.items | workflow_output(wf-feed).items | onScrollEnd → refreshDataSource(append) | 无限滚动 |
| B9 | AiSuggestion.suggestions | workflow_output(wf-recommend).list | onPageLoad → refreshDataSource | 推荐 |
| B10 | Chart.data | workflow_output(wf-chart).series | onChange(rangeFilter) → refreshDataSource | 图表数据 |

## 3. inputMapping / outputMapping JSONata 约定

- **key**：在 inputMapping 中是工作流 input 名；在 outputMapping 中是 jsonata 表达式（基于 outputs 求值）。
- **value**：在 inputMapping 中是 BindingSchema；在 outputMapping 中是目标路径字符串。
- 复杂 jsonata 例：`outputMapping: { 'users[role="admin"]': 'page.adminUsers' }`。
- 失败 / 解析为 undefined 时不产出 patch，等价于"不更新"。

## 4. loadingTargets / errorTargets 自动绑定规则

| 阶段 | patch | scope | 含义 |
| --- | --- | --- | --- |
| 调用前 | `component.<id>.loading = true` | component | 显示骨架屏 |
| 成功后 | `component.<id>.loading = false`、`component.<id>.error = unset` | component | 清理状态 |
| 失败时 | `component.<id>.loading = false`、`component.<id>.error = { kind, message }` | component | 显示错误态 |

## 5. 反例（禁止用法）

- 在表达式 / outputMapping 中将值写入 `system.*` / `component.*.value` / `event.*` / `workflow.outputs.*` / `chatflow.outputs.*` —— 编辑期被 ScopeViolationError 拒绝；运行时被 applyOutputMapping 静默忽略并记录调试日志。
- 在 React 组件实现内直 fetch `/api/runtime/workflows/...:invoke` —— 由 `@atlas/lowcode-component-registry/principles` 元数据驱动校验拒绝。
- 把 `File` 对象直接塞 inputMapping —— 必须先经 `@atlas/lowcode-asset-adapter` 两阶段上传换 fileHandle / URL（M10 落地）。

## 6. 后端弹性策略默认值

| 项 | 值 | 备注 |
| --- | --- | --- |
| timeoutMs | 30_000 | 单次调用超时 |
| retry.maxAttempts | 3 | 重试次数上限 |
| retry.backoff | exponential | 500ms → 1s → 2s |
| circuitBreaker.failuresThreshold | 5 | 5 次失败入熔断 |
| circuitBreaker.windowMs | 60_000 | 计数窗口 |
| circuitBreaker.openMs | 30_000 | 半开等待 |
| fallback | undefined | 默认无降级；可配 static / workflow |

详见 `docs/lowcode-resilience-spec.md`。

## 7. 端点速查（M09）

- `POST /api/runtime/workflows/{id}:invoke`        — 同步
- `POST /api/runtime/workflows/{id}:invoke-async`  — 异步
- `POST /api/runtime/workflows/{id}:invoke-batch`  — 批量
- `GET  /api/runtime/async-jobs/{jobId}`           — 异步任务查询
- `POST /api/runtime/async-jobs/{jobId}:cancel`    — 异步任务取消
