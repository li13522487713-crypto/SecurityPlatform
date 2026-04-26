# 存储模型草案（历史简述）

> **第 21 轮权威**：[storage-model-contract.md](./storage-model-contract.md) 与 `mendix-studio-core` 的 `microflow/contracts/storage/storage-types.ts`。

- **authoring_row**：`resource_id` → 当前编辑中 schema JSON（`MicroflowSchema` 包装）。
- **version_row**：`resource_id` + `version` + `schema_snapshot_id` + 审计列。
- **schema_snapshot_row**：`snapshot_id` + `content`（`MicroflowAuthoringSchema`）+ `content_hash`。
- **metadata_cache**（可选）：`MicroflowMetadataCatalog` 的缓存表或只读物化视图。

发布、回滚 = 移动指针或插入新版本行；**不**将 FlowGram JSON 作真相源列。
