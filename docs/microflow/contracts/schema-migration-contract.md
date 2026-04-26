# Schema 迁移策略（冻结）

- **源类型**：`MicroflowSchemaMigration`、`MicroflowSchemaMigrationResult`（`microflow/contracts/migration`）。
- **`schemaVersion`**：写在 `MicroflowAuthoringSchema` 上，表示**结构**版本；与 OpenAPI/REST 的 `schemaVersion` 字段一致。
- **`migrationVersion`**：后端存储/迁移管道版本，记录在 `GET /schema` 与 `MicroflowSchemaSnapshot` 行，可与 Authoring 结构版本**独立**累进。
- **加载旧 JSON**时：先识别 `fromVersion` → 经 `POST /schema/migrate` 或内置迁移链升至此发行版支持的 `toVersion`；不兼容时返回 `MICROFLOW_SCHEMA_INVALID`。
- **实现方**：`migrate` 可为 `frontend` | `backend` | `both`；前端本地 adapter 应至少**检测**版本，并在可能时与后端同一套规则对齐。
- **样例 schema**：`sample-manifest` 中样例应使用**当前** `schemaVersion`，见 `verify-microflow-contracts`。
