# 运行时契约说明

执行期相关契约拆为两篇：

- **轨迹与调试**：[runtime-trace-contract.md](./runtime-trace-contract.md)（`MicroflowTraceFrame`、`MicroflowRunSession`、test-run 响应）。
- **Runtime DTO**：[runtime-dto-contract.md](./runtime-dto-contract.md)（`MicroflowRuntimeDto`、`p0RuntimeActionBlocks`、`toRuntimeDto`、v1 补充 DTO）。
- **执行计划**：`toExecutionPlan`（`@atlas/mendix-studio-core` / `runtime-semantics`）产出 `MicroflowExecutionNode.p0ActionRuntime`（P0 强类型）与 `unsupportedActions`（非 P0）；见 [runtime-action-support-matrix.md](./runtime-action-support-matrix.md)。

FlowGram / WorkflowJSON 仅属 **编辑期视图模型**，不得作为业务主存储格式。
