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
| 5 | App Explorer 中 Microflows 分组真实管理多个微流 | Stage 03 已完成只读真实列表 | CRUD 仍未完成；点击打开真实画布仍未完成；保存加载仍未完成 | Stage 03+ |
| 6 | 微流 CRUD 入口 | 无 | 新建 / 重命名 / 删除 UI 未实现 | Stage 04 |
| 7 | 真实保存画布 | 未接入 | schema load / save → resourceAdapter.update() 链路缺失 | Stage 05 |
| 8 | Call Microflow 目标选择 | 静态 | 节点属性面板未接入真实微流列表 | Stage 05 |
| 9 | 执行引擎 / Trace | 未接入 | runtimeAdapter 链路未接前端 | Stage 06 |

---

## P1 Gap（引用 / 版本 / 发布）

| # | 能力项 | Gap |
|---|---|---|
| 10 | 删除前引用预检查 | 前端未调用 references API；后端已有 EnsureNoActiveTargetReferencesAsync（见注 B） |
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

前端缺口是：**App Explorer 删除入口尚未接入 references 预检查和后端错误友好提示**。
这属于 Stage 03 范围，当前只有通用错误 Toast，无专项引用冲突提示 UI。

### 注 C：Stage 02 边界（修正）

第 2 轮**只做 asset foundation / context / model / adapter bundle**，
不做整个 P0。具体范围见 `microflow-stage-01-inventory.md` Stage 边界说明表。
