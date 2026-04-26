# 前端 P0 强类型实现（第 25 轮）

- **源类型**：`@atlas/microflow/schema` 中各 `Microflow*Action`（P0）与 `MicroflowGenericAction`（排除 P0 kind）。
- **映射**：`mapAuthoringP0ToRuntimeBlocks` / `tryMapP0ActionToDiscriminatedDto`（`@atlas/microflow/runtime`）。
- **校验**：`p0-action-guards` + `validate-actions` 错误码 `MF_ACTION_P0_MUST_BE_STRONGLY_TYPED` 等。
- **样例**：`verifyMicroflowContracts()` 会检查 `p0RuntimeActionBlocks` 存在性与 supportLevel 一致性。
