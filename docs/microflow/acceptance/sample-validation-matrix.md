# 样例验收集

| key | 标题 | 类别 | 说明 |
|-----|------|------|------|
| sample-order-processing | Order Processing | basic | 主样例 `sampleMicroflowSchema` |
| sample-approval-flow | Approval Flow | workflow | 工作流动作链 |
| sample-rest-error-handling | REST Error Handling | integration | REST + 日志 |
| sample-loop-processing | Loop Processing | loop | 循环子域 |
| sample-object-type-decision | Object Type Decision | validation | 类型决策与 cast |
| sample-list-processing | List Processing | basic | 列表系动作 |
| sample-large-100-nodes | Large 100+ nodes | large | `createLargeMicroflowSample(120)` |

**程序入口**：`microflowSampleManifest`（`@atlas/mendix-studio-core` → `contracts/sample-manifest`）。

**验收脚本**：`verifyMicroflowContracts()` — 对每条执行 `validateMicroflowSchema`、`authoringToFlowGram`、`toRuntimeDto`、`buildVariableIndex`。
