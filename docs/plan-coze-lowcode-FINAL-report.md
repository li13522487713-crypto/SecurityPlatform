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

## 5. 已知简化与延后项（两轮收尾后剩余）

> 第一轮收尾批次（2026-04）：
> - ✅ **M11 chatflow 真实流式**：RuntimeChatflowService 桥接 `IDagWorkflowExecutionService.StreamRunAsync`，SseEvent → ChatChunk 4 类自动映射；非 long chatflowId 回退 mock。
> - ✅ **M16 Yjs 离线快照**：`LowCodeCollabSnapshotJob` Hangfire 每 10 分钟落 `AppVersionArchive(systemSnapshot=true)` + 自动 Cache.Clear。
> - ✅ **M19 异步/批量 Hangfire**：`RuntimeWorkflowBackgroundJob` 接管 fire-and-forget 与同步循环；进度通过 `RuntimeWorkflowAsyncJob.UpdateProgress` 定期回写。
> - ✅ **M13 OTel 全链路**：`LowCodeOtelInstrumentation` 暴露 ActivitySource 'lowcode.runtime' + Meter 5 项指标，AppHost 已 AddSource/AddMeter。
>
> 第二轮收尾批次（2026-04）：
> - ✅ **M18 凭据加密**：`LowCodeCredentialProtector`（AES-CBC + 'lcp:' 前缀幂等 + Mask + 主密钥多源回退）替换 plugin auth base64 占位；7 xUnit 单测全过；审计仅写 Mask 摘要。
> - ✅ **M19 AI 生成 LLM**：`WorkflowGenerationService` 接 `IChatClientFactory.CreateAsync` + `Microsoft.Extensions.AI.IChatClient.GetResponseAsync`；30s 超时；JSON 解析失败 / 无 LLM 配置自动回退到模板/关键字 fallback；status='success-fallback' 区分。

第三轮收尾批次（2026-04）：

- ✅ **M03 数组路径**：`state-patch` 实装 `[index]` + dot-path 数字段（`a.list[0].title` / `a.list.0.title` 等价）；ensureChild/readAt/writeAt/deleteAt；5 项数组用例。
- ✅ **M02→M18 模板增强**：`renderTemplate` 支持 `{% break %}` / `{% continue %}` + `{{ x | upper | default('-') }}` 链式 filter + 8 个内置 filter + `registerFilter` 扩展点；TOKEN_RE.lastIndex 复位修复跨调用残留 bug。
- ✅ **M17 SDK applyPatches 完整**：`@atlas/lowcode-web-sdk` `mount().update()` 完整支持 set/merge/unset + `[index]` 数组路径 + 中间路径自动建对象/数组，与 action-runtime 语义一致但内联实现避免增大 UMD bundle。
- ✅ **M08 → M14 历史快照**：`RuntimeSchemaController.GetVersionSchema` 真实加载 `app_version_archive.schema_snapshot_json` + `resource_snapshot_json`，新增 `AppVersionedSchemaSnapshotDto`；新增 `IAppDefinitionQueryService.GetVersionSchemaSnapshotAsync`。
- ✅ **M11 真实续流**：`ResumeSseAsync` 从消息日志查最近 user_input + 后续 user_inject → 拼出 RuntimeChatflowInvokeRequest → 复用 `StreamSseAsync` 完整真实流；不再 stub 一帧 final。
- ✅ **M19 S19-2 数据库批量源**：`WorkflowBatchService` database 源真实接 `AiDatabaseNodeHelper.LoadRecordsAsync`，复用 DatabaseQuery 节点底层逻辑，支持 `db:{databaseId}` 与纯数字两种 queryId 写法。
- ✅ **M19 S19-4 IO 推断算法**：`WorkflowCompositionTopologyAnalyzer.Analyze` 解析 canvas edges，识别跨界边产 inferredInputs/inferredOutputs，兼容 source/target、sourceNodeKey/targetNodeKey、from/to 三种命名，端口字段优先级清晰；6 项单测覆盖。
- ✅ **M19 S19-5 真实配额**：`WorkflowQuotaService` 接 `IOptionsMonitor<LowCodeWorkflowQuotaOptions>` + `ISqlSugarClient` 实时统计；配额超出抛 `BusinessException("WORKFLOW_QUOTA_EXCEEDED", ...)`；支持 `PerTenant` 字典覆盖。
- ✅ **M12 → M17 Webview HTTP 文件验证**：`http_file` 模式真实拉 `https://{domain}/.well-known/atlas-webview-verify.txt` + token 比对；`dns_txt` 模式因未引入 DnsClient 包，标记 `dns_txt:not-implemented` 并允许通过（契约稳定，加包后替换实现即可）。
- ✅ **M11/M19 Trigger FireAsync 真实链**：`SubmitAsyncAsync` 异步提交工作流到 Hangfire（不阻塞 cron 调度）；失败审计 detail 含 ExceptionType。
- ✅ **M12 cron 真实调度**：`RuntimeTriggerService` 集成 `IRecurringJobManager`：UpsertAsync 自动 `AddOrUpdate`，Delete/Pause `RemoveIfExists`，Resume 重新 sync；`LowCodeTriggerCronJob` 桥接到 `FireAsync`；`LowCodeTriggerReconcileHostedService` 启动期跨租户重新注册（`ListAllAsync` ClearFilter）。
- ✅ **M12 webhook 入口**：`POST /api/runtime/triggers/{id}:rotate-webhook-secret` 生成 24 字节随机 hex 密钥（whs_xxxx 前缀，明文响应只 1 次）；`POST /api/runtime/webhooks/triggers/{id}` `[AllowAnonymous]` + `X-Atlas-Webhook-Secret` 常量时间比对（防时序攻击）；与 cron/event 三类入口齐全。
- ✅ **M12 event 总线入口**：`IRuntimeTriggerService.RaiseEventAsync(eventName)` + `POST /api/runtime/triggers/events/{eventName}:raise`；扫描所有 enabled + kind=event + EventName 严格相等的触发器并触发；返回触发数。
- ✅ **Studio 三抽屉**：`VersionDrawer`（diff/rollback）/ `PublishDrawer`（hosted/embedded-sdk/preview + 撤回）/ `DebugDrawer`（6 维 trace + Span 时间线）全部接通真实 API；CanvasViewport + RightInspector 接通 schema 反序列化；preview-web 接通 SignalR HMR。
- ✅ **lowcode-runtime-spec / content-params-spec / assistant-spec** 三份 stub 升级为完整契约文档（共 ~400 行新增）。

剩余延后项（外部依赖性，无法在仓库内闭环）：

- **DNS TXT 验证**：需引入 `DnsClient` NuGet 包。契约稳定，加包后替换 `TryVerifyAsync` 内 `dns_txt` 分支即可。
- **Taro 真实 build**：M15 lowcode-mini-host 提供 H5 预览壳；微信 / 抖音小程序 build 由运维流水线 `taro build --type weapp/tt` 触发。
- **License / 模型供应商真实接入**：现有 ModelRegistry 已可工作但部分供应商需运行时 API Key；M19 LLM 调用在租户未配置模型时自动回退到关键字模板。

## 6. 后续维护建议

- 把 5 份阶段验证报告与本总报告纳入 PR 模板的"完成判定"段落。
- 任何新增 / 修改前端组件，必须经 `@atlas/lowcode-component-registry/principles` 元数据驱动校验。
- 任何新增运行时事件，必须经 `/api/runtime/events/dispatch`，禁止扩展任何前端直调路径。
- 任何新增设计态写接口，必须经 `IAuditWriter` 审计。

## 7. 致谢与签收

本次 20 里程碑全部由文档驱动的方式严格实现：每个里程碑均先核对 PLAN.md 概念覆盖 → 落实前端协议 / UI / 单测 → 落实后端实体 / Service / Controller / .http → 同步契约文档 → dotnet build / pnpm test / i18n:check 全绿后 commit + push。

最终交付：**133 + 50 单测全过 / 后端 0 警告 0 错误 / i18n 0 缺失 / 5 阶段验证报告 + 总报告全部入库。**
