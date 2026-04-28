# Microflow Stage 20 - Validation, Problems Panel & Save Gate

## 1. Scope

本轮完成微流编辑器的本地 schema validation、Problems 面板、保存前门禁、后端 validate API 接入、问题项定位节点/连线、error/warning/info 分级，以及按 active microflow id 隔离校验状态。

本轮不做执行引擎、运行输入面板、trace/debug、表达式执行、完整类型推断、完整循环调用图算法、新节点类型、mock API、孤立 demo 页面和历史 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/schema/types.ts` | 修改 | 扩展 `MicroflowValidationIssue.source` 与 `quickFixAvailable`。 |
| `src/frontend/packages/mendix/mendix-microflow/src/validators/*` | 修改 | 补齐结构、连线、参数、变量、Decision、Call Microflow、Loop、List、Object/Domain 校验。 |
| `src/frontend/packages/mendix/mendix-microflow/src/performance/useDebouncedMicroflowValidation.ts` | 修改 | 本地 debounce 校验、按 microflowId 隔离、requestId 防乱序。 |
| `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 修改 | 保存门禁、本地优先校验、Problems 分组筛选、点击定位节点/连线。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/microflow-validation-adapter.ts` | 修改 | HTTP validate issue 映射为 `source="server"`。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/http-runtime-adapter.ts` | 修改 | runtime validate issue 映射为 server source。 |
| `src/frontend/packages/mendix/mendix-microflow/src/validators/__tests__/validate-microflow-schema.test.ts` | 新增 | Stage 20 validator 单测。 |
| `docs/microflow-stage-20-validation-problems-save-gate.md` | 新增 | 本阶段设计与验收说明。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 20 gap 状态。 |

## 3. Validation Issue Model

统一模型为 `MicroflowValidationIssue`：`severity` 区分 `error` / `warning` / `info`；`code` 和稳定 `id` 用于去重与定位；`message` 展示问题；`objectId`、`flowId`、`fieldPath` 分别定位节点、连线和属性字段；`source` 覆盖 `schema`、`node`、`flow`、`parameter`、`variable`、`callMicroflow`、`domainModel`、`loop`、`server` 等来源；`quickFixAvailable` 预留快速修复能力。

## 4. Validator Matrix

| Validator | 覆盖范围 | Error | Warning | 备注 |
|---|---|---|---|---|
| structure | schema/objectCollection/flows/selection/id | 空 schema、缺集合、重复 id、悬挂引用 | stale selection | 不修改输入 schema。 |
| start/end | Start/End 事件 | 缺 Start、多 Start、非法入边 | 缺 End、Start 无出边、End return mismatch | End 缺失按 warning。 |
| flow | source/target/port/case/重复连线 | 悬挂连线、非法 source/target/port、重复 Decision case | 完全重复连线、空 case | 覆盖 nested flow。 |
| parameter | schema parameters 与 Parameter node | 空名、重名、node/schema 残留 | 类型 unknown/void | 参数与变量冲突由 variable index 诊断。 |
| variable | create/change/outputs/scope | duplicate variable、target missing/stale、只读变量写入 | maybe scope、unknown output type | 基于当前 schema 派生 index。 |
| decision | ExclusiveSplit/Merge | duplicate true/false/case、非法 case 类型 | 缺 true/false、缺分支、Merge 入/出不足 | warning 允许保存。 |
| callMicroflow | target/mapping/return | 自调用、target stale/not found、required 参数缺失 | qualifiedName stale、void return store | server 校验也会补充引用问题。 |
| loop | loop source/body/exit/break/continue | Break/Continue outside loop、targetLoop stale、非法 outgoing | body/exit 缺失、多 loop target ambiguous | 不做完整调用图算法。 |
| list | Create/Change/Aggregate/List Operation | output/source/target 缺失、重复变量 | element type unknown | 类型精推断后续阶段。 |
| object activity | Create/Retrieve/Change/Commit/Delete | entity/member/target stale 或缺失 | memberChanges 为空 | metadata 缺失时不使用 mock。 |
| server | `POST /api/microflows/{id}/validate` | server error 阻止保存 | server warning 展示但允许保存 | source 统一映射为 `server`。 |

## 5. Save Gate Strategy

保存流程为：点击 Save 后先执行本地 validation；本地存在 error 时打开 Problems 并阻止 `PUT /schema`；仅有 warning 时展示 warning 并允许继续；本地无 error 且存在后端 validate adapter 时调用 `POST /api/microflows/{id}/validate`；server error 阻止保存，server warning 展示并允许保存；全部通过后才调用 `PUT /api/microflows/{id}/schema`。保存成功后 dirty=false 并刷新 validation；保存失败保持 dirty=true 且不清空 Problems。

## 6. Problems Panel Strategy

Problems 位于编辑器底部 panel，显示 error/warning/info 数量，支持 severity 筛选、source 筛选、关键词搜索，并按 source 分组展示。点击 object issue 会选中节点、打开属性面板并把 viewport 移到节点附近；点击 flow issue 会选中连线并定位到连线中点；全局 issue 保持全局提示。空状态显示 `No problems found`。

## 7. Async / Race Protection

自动校验使用 debounce（默认 400ms）。校验状态按 microflowId 存在 `issuesByMicroflowId`、`statusByMicroflowId`、`lastValidatedAtByMicroflowId` 中；每次 `runValidationNow` 递增当前 microflowId 的 requestId，异步返回时如果 requestId 或 schema id 已过期，不覆盖当前 Problems。切换 activeMicroflowId 后只读取对应 id 的 issues。

## 8. Verification

自动测试：`src/frontend/packages/mendix/mendix-microflow/src/validators/__tests__/validate-microflow-schema.test.ts` 覆盖缺 Start、duplicate object id、dangling flow、duplicate parameter、Change Variable missing target、Decision duplicate true/missing false、Call Microflow missing/stale target、Break without Loop、List source missing、Object entity stale、A/B 隔离和 validator 不修改输入。

手工验收建议按 Stage 20 清单在 `/space/:workspaceId/mendix-studio/:appId` 打开 `MF_ValidatePurchaseRequest` 与 `MF_CalculateApprovalLevel`，验证 Problems 展示、点击定位、保存 error 拦截、warning 允许保存、后端 validate error/warning 策略和 A/B 微流问题不串数据。
