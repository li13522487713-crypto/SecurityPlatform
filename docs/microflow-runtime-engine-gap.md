# Microflow Runtime Engine Gap

本文记录当前 Microflow Runtime 与发布目标之间的差距。目标是删除后端执行双轨，让 `IMicroflowRuntimeEngine` 成为唯一真实执行入口，`POST /api/v1/microflows/{id}/test-run` 不再依赖 mock runner、固定返回或伪成功。

## Current State

| 领域 | 当前实现 | 证据 | 发布风险 | 目标状态 |
|---|---|---|---|---|
| Test-run 入口 | `MicroflowTestRunService` 调用 `IMicroflowRuntimeEngine`。 | `src/backend/Atlas.Application.Microflows/Services/MicroflowTestRunService.cs` | 入口真实，但请求 DTO 名仍含 `Mock`。 | DTO 改为 `MicroflowExecutionRequest`，语义清晰。 |
| 双 Runtime | `MicroflowRuntimeEngine` 与 `MicroflowMockRuntimeRunner` 并存。 | `Runtime/MicroflowRuntimeEngine.cs`, `Services/MicroflowMockRuntimeRunner.cs` | 子调用、action 能力、表达式求值不一致。 | 删除 `IMicroflowMockRuntimeRunner`，统一 Engine。 |
| Call Microflow | `CallMicroflowActionExecutor` 通过 DI 获取 `IMicroflowMockRuntimeRunner`。 | `Runtime/Actions/CallMicroflowActionExecutor.cs` | 子微流执行回退 mock runner。 | 递归调用 `IMicroflowRuntimeEngine`。 |
| Engine 依赖 | Engine 默认构造只注入 schemaReader/clock，仓储可空。 | `MicroflowRuntimeEngine.cs` | 引擎内子图加载不可用。 | 构造注入仓储、Evaluator、ActionRegistry、Transaction、ObjectStore。 |
| 表达式 | Engine 内置 `ExpressionParser`，另有真实 `IMicroflowExpressionEvaluator`。 | `Runtime/MicroflowRuntimeEngine.cs`, `Runtime/Expressions/MicroflowExpressionEvaluator.cs` | 双求值语义不一致。 | 全部走 `IMicroflowExpressionEvaluator`。 |
| P0 节点 | Start/End/Parameter/Decision/Merge/CreateVariable/ChangeVariable 部分真实。 | `MicroflowRuntimeEngine.cs` | Action dispatch 不统一。 | 控制流由 Engine，Action 统一 registry。 |
| Call Microflow 参数/返回 | `CallMicroflowActionExecutor` 已有参数映射、return binding、call stack。 | `CallMicroflowActionExecutor.cs` | child session 由 mock runner 生成。 | 保留逻辑，child session 改 Engine。 |
| Loop | 已有 `IMicroflowLoopExecutor`。 | `Runtime/Loops/MicroflowLoopExecutor.cs` | Engine 未统一调度。 | foreach/while/break/continue 真实执行。 |
| Object actions | 有 `IMicroflowEntityAccessService`，无 `IMicroflowRuntimeObjectStore`。 | `Runtime/Security/MicroflowEntityAccessService.cs`, `Runtime/Objects/*` | 对象动作无法统一事务与 dry-run。 | 新建 DomainModel/InMemory ObjectStore。 |
| List actions | runner 中有相关逻辑，独立 executor 不完整。 | `MicroflowMockRuntimeRunner.cs` | 容易伪成功或只在 mock runner 有效。 | Create/Change/Aggregate List executor 真实实现。 |
| RestCall | 有安全策略和真实 HTTP client，但 `AllowRealHttp=false` 返回 mock response。 | `Runtime/Actions/Http/MicroflowRuntimeHttpClient.cs` | 禁止真实 HTTP 时仍伪造成功。 | 返回 `EXTERNAL_CALL_BLOCKED`。 |
| Trace | test-run 持久化 session/trace/logs。 | `MicroflowTestRunService.cs`, repositories | 字段需对齐前端 trace panel。 | trace event 含 callDepth、caller、duration、error。 |
| Error model | 有 runtime error DTO 与若干 code。 | `Models/MicroflowRuntimeDtos.cs` | code 命名与发布清单不全。 | 补齐 25+ runtime error codes 与 HTTP 映射。 |
| `.http` | 文件使用 `/api/microflows`。 | `src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` | 与控制器 `/api/v1/microflows` 不一致。 | 全部改 `/api/v1/microflows`。 |

## Required Runtime Capabilities

| 能力 | 当前状态 | 发布处理 |
|---|---|---|
| Draft test-run | 已支持 request schema。 | 保留并验证。 |
| Published run | Publish snapshot 已落库。 | Engine 按 mode 加载 snapshot。 |
| Debug trace | session.trace 已返回。 | 对齐 trace event 字段。 |
| Timeout | 部分 action 有 timeout。 | 引擎级 timeout + cancellationToken。 |
| Max steps | Engine 有 step guard。 | 暴露 option 并测试。 |
| Max call depth | Call stack service 有 guard。 | Engine 递归统一使用。 |
| Max loop iterations | 需统一。 | LoopExecutor 加默认 1000。 |
| Tenant/workspace isolation | controller/filter/repository 已有基础。 | ObjectStore 和 metadata 加强检查。 |
| Permission check | filter + resource permissions 部分存在。 | test-run/publish/run 前检查。 |
| Audit | Microflow 专用审计缺口。 | 记录 test-run/publish/delete 等审计事件。 |
| Sensitive masking | 部分 REST redaction 存在。 | trace/input/output 脱敏规则统一。 |

## Node Support Target

| Node / Action | 发布目标 | 当前差距 |
|---|---|---|
| StartEvent | Supported | 保留。 |
| EndEvent | Supported | 表达式改统一 Evaluator。 |
| ParameterObject | Supported | 类型校验加强。 |
| ExclusiveSplit / Decision | Supported | 分支表达式改统一 Evaluator。 |
| ExclusiveMerge | Supported | 保留。 |
| CreateVariable | Supported | 从 Engine 硬编码迁移为 ActionExecutor。 |
| ChangeVariable | Supported | 从 Engine 硬编码迁移为 ActionExecutor。 |
| CallMicroflow | Supported | 子调用改 Engine，去 MockRunner。 |
| Loop | Supported | 接 `IMicroflowLoopExecutor`。 |
| Break / Continue | Supported | 新增 executor。 |
| CreateObject | Supported | 新增 ObjectStore。 |
| RetrieveObject | Supported | 新增 ObjectStore 查询。 |
| ChangeObject | Supported | 新增 ObjectStore 变更。 |
| CommitObject | Supported | 接事务 flush。 |
| DeleteObject | Supported | 接 ObjectStore 删除。 |
| CreateList | Supported | 新增 list executor。 |
| ChangeList | Supported | 新增 list executor。 |
| AggregateList | Supported | 新增 list executor。 |
| RestCall | Supported with policy | 禁止 mock response，默认阻断外部网络。 |

## Definition Of Done

1. `IMicroflowMockRuntimeRunner` 和 `MicroflowMockRuntimeRunner.cs` 不再存在。
2. `CallMicroflowActionExecutor` 不再调用 mock runner。
3. `MicroflowRuntimeEngine` 构造函数注入完整依赖，不再有可空仓储作为正常路径。
4. `MicroflowRuntimeHttpClient` 无 `CreateMockResponse`。
5. Runtime 表达式全部通过 `IMicroflowExpressionEvaluator`。
6. P0/P1 节点真实执行或返回明确 runtime error，禁止伪成功。
7. 后端 unit/integration 测试覆盖 Start/End、Decision、Variable、CallMicroflow、Loop、Object、List、RestCall、Timeout、Depth、Trace。
