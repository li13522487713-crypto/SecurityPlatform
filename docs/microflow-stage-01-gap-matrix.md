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
| 5 | App Explorer 中 Microflows 分组真实管理多个微流 | Stage 04 已完成真实列表 + CRUD 入口 | 真实画布 schema load/save 仍未完成 | Stage 03-04 已完成列表/CRUD；Stage 05+ 接入画布 |
| 6 | 微流 CRUD 入口 | Stage 04 已完成 | 新建 / 重命名 / 复制 / 删除均已通过真实 Microflow Resource API 接入 App Explorer | **Stage 04** |
| 7 | 点击微流真实打开指定 microflowId 的画布 | Stage 05 已完成 activeMicroflowId 驱动的 Workbench/tab 编辑上下文 | 真实 schema 加载与画布渲染仍未完成 | Stage 06 |
| 8 | 真实保存画布 | 未接入 | schema load / save → resourceAdapter.update() 链路缺失 | Stage 06 |
| 9 | Call Microflow 目标选择 | 静态 | 节点属性面板未接入真实微流列表 | 后续阶段 |
| 10 | 执行引擎 / Trace | 未接入 | runtimeAdapter 链路未接前端 | 后续阶段 |

---

## P1 Gap（引用 / 版本 / 发布）

| # | 能力项 | Gap |
|---|---|---|
| 10 | 删除前引用预检查 | Stage 04 已完成：App Explorer 删除前调用 references API；后端 `EnsureNoActiveTargetReferencesAsync` 仍作为最终保护（见注 B） |
| 11 | 版本历史查看 | 版本列表 UI 未实现 |
| 12 | 发布状态更新 | publishStatus 更新 UI 未实现 |

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
