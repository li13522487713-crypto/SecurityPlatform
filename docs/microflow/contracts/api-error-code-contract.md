# 微流 API 错误码（冻结）

与 TypeScript 类型 `MicroflowApiErrorCode`（`@atlas/mendix-studio-core` / `microflow/contracts`）一一对应。

| 错误码 | 说明 |
|--------|------|
| `MICROFLOW_NOT_FOUND` | 资源、版本或运行不存在。 |
| `MICROFLOW_NAME_DUPLICATED` | 同工作区下 `name` 冲突。 |
| `MICROFLOW_SCHEMA_INVALID` | 无法解析/迁移到当前 `schemaVersion` 的 Authoring JSON。 |
| `MICROFLOW_VALIDATION_FAILED` | 校验不通过；`error.validationIssues` 含 `MicroflowValidationIssue[]`。 |
| `MICROFLOW_VERSION_CONFLICT` | 乐观锁失败（`baseVersion` / ETag 与当前不一致）。 |
| `MICROFLOW_PUBLISH_BLOCKED` | 发布被阻止：仍有 error 级问题、高影响未确认、业务规则等。 |
| `MICROFLOW_REFERENCE_BLOCKED` | 因引用/依赖无法执行删除或破坏性变更。 |
| `MICROFLOW_PERMISSION_DENIED` | 无租户/工作区/资源权限。 |
| `MICROFLOW_ARCHIVED` | 归档态禁止操作。 |
| `MICROFLOW_RUN_FAILED` | 运行失败（非用户取消）。 |
| `MICROFLOW_RUN_CANCELLED` | 运行被取消。 |
| `MICROFLOW_METADATA_NOT_FOUND` | 元数据或 qualifiedName 不存在。 |
| `MICROFLOW_STORAGE_ERROR` | 存储/数据库异常。 |
| `MICROFLOW_UNKNOWN_ERROR` | 未分类错误。 |

HTTP 层建议：业务错误在响应体中仍使用 `MicroflowApiError`；根据语义映射 4xx/5xx。
