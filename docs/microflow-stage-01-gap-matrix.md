# Microflow Stage 01 — Gap 矩阵

> **轮次**：Stage 01（Inventory & Gap Analysis）
> **状态**：已完成（已由 Stage 02 修正三处内容）

---

## P0 Gap 一览

| # | 能力项 | 当前状态 | Gap | 目标轮次 |
|---|---|---|---|---|
| 1 | 真实 workspaceId / tenantId 接入 MendixStudioApp | 路由层未传递 workspace 上下文 | 路由组件只 `<MendixStudioApp appId={appId} />`，无 workspace props | **Stage 02** |
| 2 | Adapter Bundle 创建与保存 | MendixStudioApp 内未创建 bundle | 无法按真实 workspace/tenant 路由到正确后端 | **Stage 02** |
| 3 | Studio Microflow 视图模型 | 无 | 缺少 StudioMicroflowDefinitionView 展示层类型 | **Stage 02** |
| 4 | Store 微流资产索引骨架 | 无 | Store 中无 microflowResourcesById / idsByModuleId 索引 | **Stage 02** |
| 5 | App Explorer 中 Microflows 分组真实管理多个微流 | Stage 04 已完成真实列表 + CRUD 入口；Stage 05-06 已接 Workbench 与真实编辑器 | Call Microflow metadata 等深度能力仍在后续阶段 | Stage 03-06 分阶段完成 |
| 6 | 微流 CRUD 入口 | Stage 04 已完成 | 新建 / 重命名 / 复制 / 删除均已通过真实 Microflow Resource API 接入 App Explorer | **Stage 04** |
| 7 | 点击微流真实打开指定 microflowId 的画布 | Stage 06 已完成：activeMicroflowId 打开嵌入式真实 MicroflowEditor | 深度属性与 metadata 仍在后续阶段 | **Stage 06** |
| 8 | 画布按 microflowId 保存和加载 | Stage 06 已完成：GET resource/schema，PUT schema 保存 | 未保存切换 guard 待后续增强 | **Stage 06** |
| 9 | 节点拖拽后真实进入当前微流定义 | Stage 06 已完成：编辑器修改当前 microflowId 的 authoring schema | 不新增节点类型 | **Stage 06** |
| 10 | 节点位置、类型、名称、配置真实保存 | Stage 06 基础完成：随 MicroflowAuthoringSchema 保存 | 节点属性深度增强留到后续阶段 | **Stage 06 / 后续增强** |
| 11 | 连接线可以创建、删除、保存 | Stage 06 已完成：连接线随当前微流 schema 保存 | 高级引用重建优化留到后续阶段 | **Stage 06** |
| 12 | 节点属性面板可以编辑并保存 | Stage 06 基础完成；Stage 07 已保证空 target/entity/list/url 默认配置不崩溃 | Call Microflow 真实 metadata 与深度属性体验仍未接入 | 后续阶段 |
| 13 | Call Microflow 目标选择 | Stage 07 已治理默认引用：新拖入节点 target 为空并标记待配置 | 真实微流列表选择仍未接入 | 后续阶段 |
| 14 | 执行引擎 / Trace | 未接入 | runtimeAdapter 链路未作为本轮目标接入 | 后续阶段 |
| 15 | 节点工具箱分类和节点注册表 | Stage 07 已完成：Events / Parameters / Flow Control / Variables / Objects / Lists / Integration / Documentation / Other 稳定分类 | 后续可继续接入上下文级 availability 规则 | **Stage 07** |
| 16 | Object/List/Variable/REST 默认配置治理 | Stage 07 已完成：默认 entity/list/target/url 为空或安全待配置值 | Domain Model metadata 绑定和深度属性编辑仍在后续阶段 | **Stage 07** |

---

## P1 Gap（引用 / 版本 / 发布）

| # | 能力项 | Gap |
|---|---|---|
| 10 | 删除前引用预检查 | Stage 04 已完成：App Explorer 删除前调用 references API；后端 `EnsureNoActiveTargetReferencesAsync` 仍作为最终保护（见注 B） |
| 11 | 版本历史查看 | 版本列表 UI 未实现 |
| 12 | 发布状态更新 | publishStatus 更新 UI 未实现 |
| 13 | 历史 schema demo 值迁移 | Stage 07 明确不做 migration；已保存 schema 中的旧 `Sales.*` 值打开时保留 | 如需治理历史数据，后续单独设计迁移与用户确认 |

---

## 注记（Stage 02 修正版）

### 注 A：Local Adapter 持久化边界

`createLocalMicroflowApiClient` / Local Adapter **不属于**后端真实保存。
它可能使用 `localStorage`，也可能 fallback 到内存，**不能**作为真实持久化验收依据。

### 注 B：微流引用保护现状

后端 `MicroflowResourceService.DeleteAsync` 已调用 `EnsureNoActiveTargetReferencesAsync`，
因此**后端具备被引用保护**。

前端缺口已在 **Stage 04** 补齐：App Explorer 删除入口会先调用
`GET /api/microflows/{id}/references` 做 active references 预检查；
若后端 `DELETE /api/microflows/{id}` 仍返回 409 / `MICROFLOW_REFERENCE_BLOCKED`，
前端会展示友好错误并保留树节点。

### 注 D：Stage 04 已覆盖的 CRUD 缺口

- “新建微流真实创建 MicroflowResource”：已完成，入口位于 App Explorer 的 `Microflows` 分组右键菜单，调用 `resourceAdapter.createMicroflow`。
- “微流重命名”：已完成，入口位于真实微流节点右键菜单，调用 `resourceAdapter.renameMicroflow`，resource id 保持不变。
- “复制微流”：已完成，入口位于真实微流节点右键菜单，调用 `resourceAdapter.duplicateMicroflow`，返回新 resource id。
- “删除微流前检查是否被其他微流引用”：已完成，删除确认前调用 `resourceAdapter.getMicroflowReferences`，后端 409 作为最终保护。

### 注 C：Stage 02 边界（修正）

第 2 轮**只做 asset foundation / context / model / adapter bundle**，
不做整个 P0。具体范围见 `microflow-stage-01-inventory.md` Stage 边界说明表。
