# 校验：MicroflowValidationIssue 契约

## 权威类型

`MicroflowValidationIssue`（`@atlas/microflow/schema`）。可选别名：`ValidationIssue`（由 `mendix-studio-core/contracts` 导出 `ValidationIssue`）。

## 定位字段（冻结）

| 字段 | 用途 |
|------|------|
| `id` | 稳定键 |
| `severity` | error / warning / info |
| `code` | `MF_*` 机器码 |
| `message` | 人读说明 |
| `objectId` / `flowId` / `actionId` / `parameterId` / `collectionId` | 图与参数定位 |
| `fieldPath` | 属性路径，供 ProblemPanel / 属性栏 |
| `source` | root / objectCollection / flow / action / … |
| `quickFixes` / `relatedObjectIds` / `relatedFlowIds` / `details` | 扩展展示 |

## ValidationCode 分层

代码以 `MF_` 前缀为主。**冻结清单**为 `@atlas/microflow/validators` 中导出的 `microflowValidationCodes` 常量与 `MicroflowValidationCode` 类型；`@atlas/mendix-studio-core` 的 `microflow/contracts` 已再导出，便于后端与 OpenAPI 对齐。

- **Root**：`MF_ROOT_*`
- **Object / Flow / Decision / ObjectType / Loop / Action**
- **Metadata**：`MF_METADATA_*`（含 `MF_METADATA_SPECIALIZATION_NOT_FOUND`）
- **Action 扩展**：`MF_ACTION_NANOFLOW_ONLY`、`MF_ACTION_DEPRECATED` 等
- **Variable / Expression**
- **ErrorHandling / Reachability**

运行期 `code` 仍可能为**清单外**字符串（`MicroflowValidationCode` 含 `string` 并集）；后端应宽容接收并在迭代中合入 `microflowValidationCodes`。

## 统一入口

`validateMicroflowSchema({ schema, options: { mode: "edit"|"save"|"publish"|"testRun", includeWarnings, includeInfo } })`。

## 后端建议

持久化或 CI 可返回 **相同 JSON 结构**，前端 ProblemPanel 即可复用。
