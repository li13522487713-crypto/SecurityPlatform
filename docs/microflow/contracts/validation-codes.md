# MicroflowValidationCode 冻结清单

**权威源**：`src/frontend/packages/mendix/mendix-microflow/src/validators/validation-codes.ts` 中的 `microflowValidationCodes` 与 `MicroflowValidationCode`。

**再导出**：`@atlas/mendix-studio-core` → `microflow/contracts` 同步导出，便于与后端枚举对齐。

与 [validation-contract.md](./validation-contract.md) 的 `MicroflowValidationIssue` 配合使用；`issue.code` 以本清单为主，**允许**未列入的扩展码（并集 `string`）。

## 全量表（与源码数组顺序一致）

见源码 `microflowValidationCodes`（当前 93 项，含 `MF_ACTION_NANOFLOW_ONLY`、`MF_ACTION_DEPRECATED`、`MF_METADATA_SPECIALIZATION_NOT_FOUND` 等）。此处不重复粘贴，避免与代码漂移；发布前以 TypeScript 编译与 `verify-contracts` 为准。

## 与校验器未入码表的发射

若将来 `grep "MF_" validators` 发现新码，**应同时** 加入 `microflowValidationCodes` 数组，保持单源。
