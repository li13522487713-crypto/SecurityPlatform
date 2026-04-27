# 第 60 轮已知限制与内测风险

## 已知限制

1. **API 版本前缀**：当前微流后端实际路径为 `api/microflows` 与 `api/microflow-metadata`，不是仓库通用规则中的 `api/v1/*`。Round60 不新增第二套路由，避免破坏第 31～59 轮契约。
2. **浏览器 E2E 专项**：仓库已有 Playwright app E2E 基础设施，但尚无微流资源库/编辑器专项浏览器用例。Round60 总控先收敛 HTTP verify、前端契约 verify 与文档化手工检查。
3. **同步 TestRun 取消**：`POST /api/microflows/runs/{runId}/cancel` 可标记已持久化 run，但同步请求内执行的长循环不能等同异步运行中协作取消。完整运行中取消需后续 Job Queue 或异步 runtime。
4. **鉴权态**：微流控制器当前为 `[AllowAnonymous]`；401/403 UI 映射已由前端错误处理验证覆盖，真实权限策略需要第 61 轮生产准备阶段与统一权限体系对齐。
5. **运行时组件命名漂移**：`TraceWriter`、`RuntimeLogWriter`、`RunStateMachine`、`CancellationRegistry`、`RuntimeLimitsOptions` 没有同名类型，相关职责由现有 runner、service、repository、HTTP client 和 options 承担。
6. **Legacy verify 资源命名**：部分第 48～58 轮 legacy verify 脚本创建 `Verify*` / `RuntimeRound*` 资源。Round60 seed/reset 严格使用 `R60_E2E_` / `E2E_MF_` 前缀，但 legacy 脚本完全统一前缀需要后续清理。
7. **真实外部 REST**：RestCall 默认不真实访问网络；只有 `allowRealHttp=true` 且通过安全策略才允许出站。Round60 验证 mock、SSRF 阻断、timeout 保护与 `$latestHttpResponse`，不验证外部生产 Connector。

## 风险分级

- Blocker：前端无法构建/启动、后端无法构建/启动、Resource/Schema 保存丢失、TestRun 主链路失败、Trace 无法查询、数据库初始化失败。
- Critical：生产路径 fallback mock、Metadata/Validation 全局不可用、Publish/Version/References 主链路失败、ErrorHandling 四模式错误、Cancel/limits 不生效、Run 卡 running。
- Major：某类 ActionExecutor 失败、浏览器级微流 E2E 缺口、大图性能超阈值、DebugPanel 局部展示异常。
- Minor：非阻塞 UI 文案、标签、局部空态。

## 与 Mendix 完整能力差距

1. 未实现完整 JavaAction / Connector / SOAP / Document / Workflow / ML 执行平台。
2. 未实现真实业务对象数据库 CRUD 与完整 EntityAccess enforcement。
3. 未实现异步微流 Job Queue、WebSocket Trace Streaming、运行中暂停/恢复。
4. 未实现完整 Mendix 客户端状态刷新、页面导航和用户任务运行时。
5. 大图性能阈值已纳入 Round60 报告，但仍需要真实浏览器性能采样长期跟踪。
