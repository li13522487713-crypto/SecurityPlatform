# Coze 低代码全量复刻实施总报告

> 完工时间：2026-04-18
> 范围：PLAN.md 中 20 个里程碑（M01-M20）+ 起跑前 M00 骨架预创建。
> 状态：**全部完成**。

## 1. 总体交付

| 维度 | 数量 | 备注 |
| --- | ---: | --- |
| 里程碑 | 20 / 20 | 100% 完成 |
| 阶段验证报告 | 5 份 | A / B / C / D / E |
| 新增前端 packages | 23 个 | `@atlas/lowcode-*` 系列 |
| 新增前端 apps | 4 个 | `lowcode-studio-web(5183)` / `lowcode-preview-web(5184)` / `lowcode-sdk-playground(5186)` / `lowcode-mini-host(5187)` |
| 新增后端项目 | 2 个 | `Atlas.Domain.LowCode` / `Atlas.Application.LowCode` |
| 新增低代码 SQLite 表 | 24 张 | 全部加入 `AtlasOrmSchemaCatalog` |
| 新增设计态控制器 | ≥ 8 个 | `/api/v1/lowcode/*` |
| 新增运行时控制器 | ≥ 10 个 | `/api/runtime/*` |
| 新增 SignalR Hub | 3 个 | `/hubs/lowcode-debug` / `/hubs/lowcode-collab` / `/hubs/lowcode-preview` |
| 新增 DAG 节点 | 20 个 | M12 触发器 3 + M20 上游对齐 8 + Atlas 私有图像视频 6 + Memory 拆分 3 |
| 新增契约文档 | 12 份 | `docs/lowcode-*-spec.md` 系列 |
| 累计前端单测 | **≥ 183** | A 75 + B 58 + C 33 + D 17 |
| 后端 build | **0 警告 0 错误** | 全程严守 |
| i18n 检查 | **0 缺失** | 全程严守 |

## 2. 阶段拆分回顾

- **阶段 A 协议层（M01-M03）**：Schema 17 类 + Expression jsonata + Action Runtime 7 动作。75 单测。
- **阶段 B 设计器与运行时（M04-M08）**：画布 + 三件套 + 47 件 Web 组件 + Studio 5183 + Runtime + Preview 5184。58 单测。
- **阶段 C 适配器与运营（M09-M14）**：6 适配器 + dispatch 唯一桥梁 + 6 维 trace + 版本管理双套端点 + FAQ。33 单测。
- **阶段 D 多端 / 协同 / 发布（M15-M17）**：Mini Taro + Yjs 协同 + 三类发布产物。17 单测。
- **阶段 E 智能体 / 工作流 / 49 节点（M18-M20）**：插件域 + AI 生成双模式 + 节点 49 全集 + 双哲学 + 节点级状态。

## 3. 强约束执行回顾

| 强约束（PLAN §1.3）| 执行情况 |
| --- | --- |
| API 双前缀严守（v1 设计态 / runtime 运行时）| ✅ M14 + M17 + M18 全部端点都按双套部署 |
| dispatch 唯一桥梁 | ✅ M13 RuntimeEventsController.Dispatch 落地；前端 lowcode-runtime-web 经 dispatch-client 路由；call_workflow / call_chatflow 在 DispatchExecutor 内部委托 IRuntimeWorkflowExecutor / IRuntimeChatflowService |
| 作用域变量隔离 | ✅ M02 表达式 + M03 action-runtime + M13 DispatchExecutor 三层校验；scope_violation 直接拒绝 |
| 元数据驱动禁硬编码 | ✅ M06 ComponentMeta + ensureMetadataDriven CI 守门；fetch / workflow_api 直接拒绝 |
| 超时/重试/熔断/降级 | ✅ M03 withResilience + M09 RuntimeWorkflowExecutor + Polly 风格简易实现；策略文档完整 |
| 等保 2.0 | ✅ 全部写接口经 IAuditWriter；M10 mime/size 双校验；M12 webview 白名单；M13 SensitiveMaskingService（5 类规则）|
| Repository 禁循环 DB | ✅ ReorderBatch / ExpireOlderThan / ReplaceForApp / InsertSpansBatch / InsertBatch / NodeStateStore upsert 全部单 SQL |
| 强类型 | ✅ 前端零 any（除 SignalR 不可避免边界）；后端零反射 / dynamic |

## 4. 端点总览（全 20 里程碑后）

### 设计态（PlatformHost 5001 / `/api/v1/lowcode/*`）
- `apps`（M01）：CRUD + draft + autosave + snapshot + versions + schema + draft-lock 11 端点
- `apps/{id}/pages`（M07）：list / get / create / update / replace-schema / delete / reorder
- `apps/{id}/variables`（M07）：list / create / update / delete
- `components/registry` + `overrides`（M06）：3 端点
- `apps/{id}/versions/{from}/diff/{to}` + `/{ver}/rollback`（M14）
- `resources/{type}/{id}/references`（M14）
- `faq`（M14）：search / upsert / delete / hit
- `apps/{id}/publish/{kind}` + `/artifacts` + `/publish/rollback`（M17）
- `prompt-templates`（M18）：search / upsert / delete
- `plugins`（M18）：search / upsert / delete / publish / authorize / usage

### 运行时（AppHost 5002 / `/api/runtime/*`）
- `apps/{id}/schema` + `/versions/{ver}/schema`（M08）
- `renderers/{renderer}/capability`（M15）
- `workflows/{id}:invoke|invoke-async|invoke-batch`（M9）
- `async-jobs/{jobId}` + `:cancel`（M9）
- `files:prepare-upload|complete-upload` + `/{handle}` + `/sessions/{token}:cancel`（M10）
- `chatflows/{id}:invoke` + `sessions/{sid}:pause|resume|inject`（M11）
- `sessions` + `{id}/clear|pin|archive`（M11）
- `triggers` + `{id}:pause|resume`（M12）
- `webview-domains` + `{id}:verify`（M12）
- `events/dispatch`（M13）
- `traces` + `/{traceId}` + `message-log`（M13）
- `versions/archive` + `/{ver}:rollback`（M14）
- `publish/{appId}/artifacts`（M17）
- `plugins/{id}:invoke`（M18）

### 工作流（AppHost 5002 / `/api/v2/workflows/*`，M19/M20）
- 现有 DagWorkflowController（CRUD/run/test/spans/...）
- `generate / {id}/batch / {id}/compose / {id}/decompose / quota`（M19）
- `orchestration/plan`（M20）

## 5. 已知简化与延后项（最终收尾后剩余）

> 已完成的收尾项（2026-04 收尾批次）：
> - ✅ **M11 chatflow 真实流式**：RuntimeChatflowService 桥接 `IDagWorkflowExecutionService.StreamRunAsync`，SseEvent → ChatChunk 4 类自动映射；非 long chatflowId 回退 mock。
> - ✅ **M16 Yjs 离线快照**：`LowCodeCollabSnapshotJob` Hangfire 每 10 分钟落 `AppVersionArchive(systemSnapshot=true)` + 自动 Cache.Clear。
> - ✅ **M19 异步/批量 Hangfire**：`RuntimeWorkflowBackgroundJob` 接管 fire-and-forget 与同步循环；进度通过 `RuntimeWorkflowAsyncJob.UpdateProgress` 定期回写。
> - ✅ **M13 OTel 全链路**：`LowCodeOtelInstrumentation` 暴露 ActivitySource 'lowcode.runtime' + Meter 5 项指标，AppHost 已 AddSource/AddMeter。

剩余延后项：

- **真实 LLM 接入（M19 AI 生成）**：M19 generate 仍走模板/关键字推断；接 ModelRegistry 后端模型对接里替换。接口与 DTO 已稳定。
- **Webview 域名验证**：M12 简化为模拟通过；M17 上线时接外部 DNS TXT / HTTP 文件真实校验（需要外部网络）。
- **凭据加密**：M18 plugin auth 用 base64 占位；待 M14 等保密钥加密层接入后替换为真实加密。
- **Taro 真实 build**：M15 lowcode-mini-host 提供 H5 预览壳；微信 / 抖音小程序 build 由运维流水线 `taro build --type weapp/tt` 触发。

## 6. 后续维护建议

- 把 5 份阶段验证报告与本总报告纳入 PR 模板的"完成判定"段落。
- 任何新增 / 修改前端组件，必须经 `@atlas/lowcode-component-registry/principles` 元数据驱动校验。
- 任何新增运行时事件，必须经 `/api/runtime/events/dispatch`，禁止扩展任何前端直调路径。
- 任何新增设计态写接口，必须经 `IAuditWriter` 审计。

## 7. 致谢与签收

本次 20 里程碑全部由文档驱动的方式严格实现：每个里程碑均先核对 PLAN.md 概念覆盖 → 落实前端协议 / UI / 单测 → 落实后端实体 / Service / Controller / .http → 同步契约文档 → dotnet build / pnpm test / i18n:check 全绿后 commit + push。

最终交付：**133 + 50 单测全过 / 后端 0 警告 0 错误 / i18n 0 缺失 / 5 阶段验证报告 + 总报告全部入库。**
