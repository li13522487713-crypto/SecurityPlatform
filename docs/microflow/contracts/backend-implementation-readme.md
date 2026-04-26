# 后端实现指引（本仓库不实现）

供后端团队按**冻结契约**分阶段实现；不替代 OpenAPI/TS 类型。

## 建议启动顺序

1. **资源 CRUD** + 列表筛选分页（`MicroflowResource` 行 + DTO 映射)。
2. **Schema GET/PUT** + `baseVersion` 乐观锁 + 审计字段。
3. **版本**与**发布不可变快照**（`MicroflowPublishedSnapshot` 行，仅 Authoring JSON）。
4. **Metadata** 全量/缓存行（`updatedAt` / `version` 支持客户端缓存)。
5. **Validation** 端点，与 `MicroflowValidationIssue` 同构。
6. **TestRun 模拟器** + RunSession + Trace/Log 从表，REST 先全量再考虑流式。

## P0 可先交付

- 微流资源 + Schema 存取 + 列表。
- 发布 + 只读发布快照 + 基础版本树。
- 元数据全量 `GET`。
- 与前端 mock adapter 可切换的**单租户**、单工作区。

## 可暂缓

- 引用关系的完整索引/反向搜索（可先用简表+批任务）。
- Trace 长保留与冷归档。
- 流式 trace WebSocket。

## JSON 与事务

- 大 JSON 列（`SchemaJson`）建议**压缩/哈希**（`SchemaHash`）与乐观锁/去重；事务边界：单资源 `PUT /schema` 与行级 `MicroflowVersion` 追加一致提交。
- **禁止** 依赖 FlowGram JSON；仅 Authoring 与 DTO/Trace 契约。

## 权限

- 所有写路径带 `WorkspaceId` / `TenantId` 与 `userId` 校验，与 DTO 上 `permissions` 可组合；审计列 `CreatedBy/UpdatedBy`。

## 与前端切换真实 API

1. 实现上述 REST，响应包 `MicroflowApiResponse`。
2. 新增 `createHttpMicroflowResourceAdapter`（未来）在内部调 fetch，**仅解析 Envelope**。
3. 宿主注入 adapter，替换 `createLocalMicroflowResourceAdapter`。
4. 元数据、校验、TestRun 同理接入。

## 未知项

- 多区域复制与**最终一致**的 catalog 版本传播（可后续在 `metadata` 上扩展 ETag）。
