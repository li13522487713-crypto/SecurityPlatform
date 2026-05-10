# Microflow Runtime 生产级补齐计划（严格版）

## 0. 目标与范围

### 0.1 总目标
在**不推翻现有 Runtime 架构**前提下，将当前“短事务解释型 Runtime + 部分 ExecutionPlan 能力”升级为“可生产运行的 ExecutionPlan-first Runtime 基座”，并保证不回退既有 TestRun、Debug、Run History、Cancellation 能力。

### 0.2 本期范围（必须完成）
1. ExecutionPlan-first + 版本化缓存。
2. ExecutionContext 语义收敛与扩展（CancellationToken / MemoryBudget / NodeResults / Errors）。
3. 统一内存预算与大对象引用治理。
4. 生产日志与快照摘要化（默认脱敏、限长、可采样）。
5. Runtime 事务与 DB 事务分层拆分（PublishedRun 禁用 InMemory store）。
6. 错误处理统一收口到 `MicroflowErrorHandlingService`。
7. 并发分支“非真并行”现状显式化 + 分支隔离接口骨架。
8. 覆盖新增测试集并保留现有测试。

### 0.3 非目标（本期不做）
1. 不做前端 UI 大改。
2. 不做长流程 Workflow Runtime 全量落地（仅预留边界/接口）。
3. 不进行微流 DSL 重写或节点体系重构。

---

## 1. 现状基线与约束

### 1.1 基线结论
- 当前已经具备：RuntimeEngine、VariableStore、ExecutionPlanBuilder/Validator/Query、TransactionManager、ErrorHandlingService、RunSession 持久化。
- 当前不足：Engine 仍有 schema graph 运行时构建路径；默认对象存储为 InMemory；日志/快照 raw 值体量不可控；并行分支未形成隔离执行语义。

### 1.2 约束
1. 禁止破坏既有运行链路（TestRun、Debug、历史查询）。
2. API 契约变化必须同步 `.http` 示例、契约文档与测试。
3. 新增后端能力必须保持 async/await 与分层边界。
4. PublishedRun 必须严格走真实 DB-backed object store。

---

## 2. 交付结构（WBS）

### WBS-A：ExecutionPlan-first 与缓存
**目标**：运行路径优先消费已编译 plan，避免重复建图与重复解析。

**任务清单**
1. 新增 `IMicroflowExecutionPlanCache` 与默认实现（LRU + size limit + version key）。
2. 缓存键固定为：`resourceId + schemaId/versionId + metadataVersion + mode`。
3. RuntimeEngine 执行顺序：
   - 优先 `request.ExecutionPlan`；
   - 次选 `ExecutionPlanCache.GetOrCreateAsync(...)`；
   - 最后 fallback 兼容路径（仅用于兜底）。
4. `MicroflowExecutionPlanQuery` 作为执行期唯一节点/连线查询入口。
5. 版本发布、保存、元数据变更时触发缓存失效。

**交付物**
- 新接口/实现代码。
- plan-first 执行路径单测。
- cache 失效单测。

**退出准则**
- 同版本重复运行不再生成随机 plan identity。
- 100 节点基准场景 plan 构建次数显著下降（日志/指标可观测）。

---

### WBS-B：ExecutionContext 收敛
**目标**：消除 Engine 内外双上下文语义漂移。

**任务清单**
1. 在 `RuntimeExecutionContext` 增加：
   - `CancellationToken`
   - `ExecutionMemoryBudget`
   - `NodeResults`（摘要存储）
   - `Errors`（统一错误轨迹）
2. 明确只读身份字段（TenantId/UserId/WorkspaceId）顶层访问。
3. 保留并兼容 DebugSession、CallStack、LoopStack、ErrorStack。
4. 清理 Engine 内部与公开上下文重复字段，统一由 context 驱动。

**交付物**
- context 模型升级。
- RuntimeContextIsolationTests。

**退出准则**
- 所有执行器从统一 context 可获取取消信号与预算配置。

---

### WBS-C：内存预算与大对象治理
**目标**：防止变量、循环、HTTP 响应和 trace 膨胀拖垮运行实例。

**任务清单**
1. 新增 `ExecutionMemoryBudget`：
   - MaxContextBytes
   - MaxVariableBytes
   - MaxNodeOutputBytes
   - MaxCollectionItems
   - MaxLoopIterations
   - MaxHttpResponseBytes
   - MaxTraceFrames
2. `MicroflowVariableStore` 增强：
   - `EstimatedSizeBytes`
   - `IsLargeObject`
   - `ValueRef`（blob/file/httpBody/queryPage）
3. 循环执行器禁止全量物化超大集合（流式/分批迭代）。
4. Rest 响应超限时仅落 `RuntimeValueRef + preview`。
5. 生产默认 `IncludeRawValue=false`。

**交付物**
- `RuntimeValueRef` / `ExecutionMemoryGuard`。
- 预算超限异常与诊断码。
- 相关测试。

**退出准则**
- 10MB HTTP body 不进入 `RawValueJson`。
- 大 List 不全量装载到单次 loop 内存。

---

### WBS-D：日志、快照与脱敏
**目标**：生产可观测而不泄露、不膨胀。

**任务清单**
1. 生产 trace 默认仅存 summary（InputSummary/OutputSummary/VariablesSummary）。
2. Debug 模式才允许 raw，且受 `MaxSnapshotBytes` 和采样策略限制。
3. 新增敏感字段脱敏器（token/password/credential/header/body 关键字段）。
4. 日志写入改为异步批量 pipeline（bounded channel + flush policy）。
5. 失败关键日志支持同步兜底落盘，避免丢失。

**交付物**
- `ExecutionLogSanitizer`。
- `RuntimeTraceSummaryTests`。

**退出准则**
- Production trace 不保存完整变量 raw 值。
- 大对象日志仅保存 ref/size/preview。

---

### WBS-E：事务分层与对象存储生产化
**目标**：明确 Runtime 语义事务与真实 DB 事务边界。

**任务清单**
1. 保留 `MicroflowTransactionManager`（运行时语义事务）。
2. 新增 `IMicroflowDatabaseUnitOfWork` 与 SqlSugar 实现。
3. 新增 `DbBackedRuntimeObjectStore`（权限、租户、分页、并发控制）。
4. DI 策略：
   - PublishedRun：强制 DB-backed。
   - TestRun：允许 InMemory（测试隔离）。
5. Commit/Rollback 节点映射 DB UoW（含 savepoint 能力）。

**交付物**
- DB UoW 接口与实现。
- Object action 执行器改造。
- 集成测试。

**退出准则**
- PublishedRun 不使用 `InMemoryRuntimeObjectStore`。
- 节点失败可验证 DB rollback 与 Runtime rollback 语义区分。

---

### WBS-F：错误处理统一
**目标**：错误路径单一入口、语义一致。

**任务清单**
1. RuntimeEngine 删除分叉式硬编码跳转（`ShouldEnterErrorHandler` 直跳）。
2. 所有 action failure 统一封装为 `MicroflowErrorHandlingContext`。
3. 统一调用 `MicroflowErrorHandlingService.Handle(...)`。
4. 明确 Continue / Rollback / CustomWithRollback / CustomWithoutRollback 行为表。

**交付物**
- Engine 与 error service 协同改造。
- 语义回归测试。

**退出准则**
- 四种错误策略在同一入口执行，trace 行为一致。

---

### WBS-G：并发隔离准备
**目标**：在暂不引入真并行执行前，先建立不可误用的隔离契约。

**任务清单**
1. 明确标记当前 parallel/inclusive gateway 为“非真并行兼容模式”。
2. 新增接口骨架：
   - `IBranchExecutionContextFactory`
   - `IVariableScopeForker`
   - `IBranchMergePolicy`
3. 合同约束：并行分支不得共享同一 VariableStore 写状态。

**交付物**
- 并发契约接口与文档。
- `ParallelBranchIsolationContractTests`。

**退出准则**
- 并发契约测试通过，未来真并行改造可平滑接入。

---

## 3. 测试策略（先测后改）

## 3.1 必增测试清单
1. `MicroflowExecutionPlanCacheTests`
2. `MicroflowRuntimeEnginePlanFirstTests`
3. `RuntimeContextIsolationTests`
4. `MicroflowVariableScopeIsolationTests`
5. `MicroflowMemoryBudgetTests`
6. `RuntimeTraceSummaryTests`
7. `RestLargeResponseShouldUseRefTests`
8. `MicroflowDatabaseTransactionTests`
9. `MicroflowErrorHandlingSemanticsTests`
10. `ParallelBranchIsolationContractTests`

## 3.2 回归测试清单
- 微流现有 TestRun/Debug/RunHistory/Cancellation 全集。
- 相关集成测试（对象节点、错误处理、循环、子微流）。

## 3.3 性能与并发基准
1. 100 节点稳定执行。
2. 1000 节点无栈溢出（while 主循环 + max step 保护）。
3. 100 并发实例变量不串扰。

---

## 4. 里程碑与排期（建议）

- M1（1~1.5 周）：WBS-A + WBS-B。
- M2（1~1.5 周）：WBS-C + WBS-D。
- M3（1.5~2 周）：WBS-E + WBS-F。
- M4（0.5~1 周）：WBS-G + 全量回归 + 文档收口。

> 总计建议：4~6 周（按双人并行开发与单分支集成节奏估算）。

---

## 5. 风险清单与应对

1. **缓存一致性风险**：版本变更后旧 plan 误用。  
   - 应对：版本化 cache key + 发布事件失效 + 运行实例持有 version pin。
2. **事务语义混淆**：Runtime rollback 被误当 DB rollback。  
   - 应对：接口命名显式区分 + 事务日志增加 `runtimeTxId/dbTxId`。
3. **日志体量回弹**：Debug 默认策略误入生产。  
   - 应对：环境开关白名单 + 生产强制 summary 模式。
4. **并发改造误伤现有行为**：分支隔离引入变量不可见问题。  
   - 应对：先合同测试，后真实并行，逐步灰度。

---

## 6. 文档与契约同步要求

每个里程碑完成时同步：
1. `docs/contracts.md`（新增/变更契约字段、错误码、运行模式）。
2. `docs/workflow-editor-validation-matrix.md`（如涉及节点语义变化）。
3. `src/backend/Atlas.AppHost/Bosch.http/`（如涉及 API 行为变化）。
4. 相关测试说明与基准报告（可放 `docs/plan-*` 附录）。

---

## 7. 完整验收门槛（DoD）

1. 功能：F1~F10 全部满足或给出可追踪豁免单。
2. 性能：P1~P8 全部达标。
3. 事务：T1~T8 全部达标。
4. 构建：相关 `dotnet build` / `dotnet test` 全绿，0 error 0 warning。
5. 安全：敏感字段脱敏策略通过审计抽检。
6. 运行：PublishedRun 路径确认未使用 InMemory object store。

---

## 8. 执行顺序（严格）

1. 先补测试骨架（红灯）。
2. 实施最小代码改造（逐 WBS）。
3. 每个 WBS 完成立即跑最小必要回归。
4. 通过后再进入下一 WBS。
5. 最后执行全量回归 + 文档同步 + 基准报告。

> 任何一项未通过不得声称完成；必须回到“修正 → 验证 → 再自审”闭环。
