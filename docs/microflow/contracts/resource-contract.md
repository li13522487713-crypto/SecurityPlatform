# Resource / Version / Publish / Reference 契约

## 权威类型

产品级资源模型在 **`@atlas/mendix-studio-core`** 中实现并导出，而非 `@atlas/microflow` 的遗留 `MicroflowResource`（避免混淆）：

- `MicroflowResource`、`MicroflowResourceQuery`、`MicroflowCreateInput`、`MicroflowResourcePatch`
- `MicroflowPublishInput`、`MicroflowPublishResult`（见 `publish/microflow-publish-types`）
- `MicroflowVersionSummary`、`MicroflowVersionDetail`、`MicroflowPublishedSnapshot`（`versions/microflow-version-types`）
- `MicroflowVersionDiff`、`MicroflowBreakingChange`（`versions/microflow-version-diff`）
- `MicroflowReference`（`references/microflow-reference-types`）
- `MicroflowPublishImpactAnalysis`（publish utils/types）
- `MicroflowResourcePermissions`（`resource/resource-types`）

## 关系约束

- 每个 `MicroflowResource` **包含** `schema: MicroflowSchema`（以 Authoring 为主，含 `version` 等包装字段）。
- 版本摘要含 **`schemaSnapshotId`**（与发布快照/哈希策略对齐，由适配器层实现）。
- `MicroflowPublishedSnapshot` 内含 **`MicroflowAuthoringSchema` 内容**（与运行时/审计一致）。

## 后端表建议（草案）

- `Microflow`：id、workspaceId、元数据、当前 draft schema 指针。
- `MicroflowVersion`：resourceId、version、snapshotId、创建信息。
- `MicroflowSchemaSnapshot`：id、content JSON（AuthoringSchema）、contentHash。

## 第 46～47 轮联调补充

- HTTP ResourceAdapter 已按真实后端路径回归 publish / versions / rollback / duplicate version / compare-current / references / impact；impact query 明确使用 `includeBreakingChanges` 与 `includeReferences`。
- ReferencesDrawer 的 `includeInactive/sourceType/impactLevel` 由后端过滤，前端只做 sourceName 搜索。
- RuntimeAdapter 的 test-run 后回读 get run / get trace，DebugPanel 使用持久化 RunSession / TraceFrame / RuntimeLog。
