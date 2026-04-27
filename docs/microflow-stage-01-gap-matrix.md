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
| 9 | 节点拖拽后真实进入当前微流定义 | Stage 08 已完成专项验收与修复：NodePanel payload -> FlowGram drop -> authoring schema objectCollection | 不新增节点类型 | **Stage 08** |
| 10 | 节点位置、类型、名称、配置真实保存 | Stage 09 已完成节点移动/删除/复制/重命名基础编辑持久化；随 Stage 06 save bridge 保存 | 节点属性深度增强留到后续阶段 | **Stage 09 / 后续增强** |
| 11 | 连接线可以创建、删除、保存 | Stage 10 已完成：FlowGram connect/delete 真实写回当前 microflow authoring schema，保存进入 `PUT /api/microflows/{id}/schema` | 复杂属性表单与运行语义留到后续阶段 | **Stage 10** |
| 12 | 节点属性面板可以编辑并保存 | Stage 11 已完成：selectedObject/selectedFlow 属性加载、node/action/flow 基础表单分发、caption/documentation/action config/edge config 写回当前 schema | Call Microflow 真实 metadata 与 Domain Model metadata 仍在后续阶段 | **Stage 11** |
| 13 | Call Microflow 目标选择 | Stage 07 已治理默认引用：新拖入节点 target 为空并标记待配置 | 真实微流列表选择仍未接入 | 后续阶段 |
| 14 | 执行引擎 / Trace | 未接入 | runtimeAdapter 链路未作为本轮目标接入 | 后续阶段 |
| 15 | 节点工具箱分类和节点注册表 | Stage 07 已完成：Events / Parameters / Flow Control / Variables / Objects / Lists / Integration / Documentation / Other 稳定分类 | 后续可继续接入上下文级 availability 规则 | **Stage 07** |
| 16 | Object/List/Variable/REST 默认配置治理 | Stage 07 已完成：默认 entity/list/target/url 为空或安全待配置值 | Domain Model metadata 绑定和深度属性编辑仍在后续阶段 | **Stage 07** |
| 17 | 不同微流节点不互相污染 | Stage 08 已补测试验证 A/B schema 分别 add node 不互相污染；运行时隔离继续依赖 Stage 05/06 tab/schema remount | 快速切换未保存 guard 仍可后续增强 | **Stage 08** |
| 18 | 节点删除真实保存 | Stage 09 已完成基础删除；Stage 10 复查并验证删除 object 时同步清理 root/nested related flows，避免悬挂连线 | 复杂子图复制/迁移留到后续 | **Stage 09 / Stage 10** |
| 19 | 节点复制真实保存 | Stage 09 已完成：复制同 collection object，生成新 object/action/parameter id、新 caption、偏移位置，不复制 flows | 复杂复制子图/连线留到后续 | **Stage 09** |
| 20 | 连线 source/target/handle 可以保存 | Stage 10 已完成：保存 `originObjectId`、`destinationObjectId`、`originConnectionIndex`、`destinationConnectionIndex`，刷新后由 schema 重新映射 FlowGram handle | 未做历史 schema migration | **Stage 10** |
| 21 | Decision true/false 分支基础持久化 | Stage 10 已完成：Decision true/false 出边写入 boolean `caseValues`，删除连线释放 case，重复 case 由连接校验阻止 | Enumeration/ObjectType 深度 metadata 仍在后续阶段 | **Stage 10** |
| 22 | 不同 nodeType/actionKind 可渲染对应基础表单 | Stage 11 已完成：事件、参数、注释、Decision、Merge、Loop、ActionActivity、Flow 分发到基础表单；unknown action 只读 fallback 不崩溃 | 深度 metadata selector 留到后续 | **Stage 11** |
| 23 | 空配置节点不崩溃 | Stage 11 已完成：Call Microflow target、Object/List entity、REST url、Decision expression、Parameter name 等空配置保留待配置状态并显示 warning | 完整 validate/quick fix 留到后续 | **Stage 11** |
| 24 | 属性修改 dirty/save/reload 闭环 | Stage 11 已完成 schema-bound helper 与测试覆盖；运行时保存复用 Stage 06 save bridge | 浏览器 Network 手工验收需在运行环境确认 | **Stage 11** |

---

## P1 Gap（引用 / 版本 / 发布）

| # | 能力项 | Gap |
|---|---|---|
| 10 | 删除前引用预检查 | Stage 04 已完成：App Explorer 删除前调用 references API；后端 `EnsureNoActiveTargetReferencesAsync` 仍作为最终保护（见注 B） |
| 11 | 版本历史查看 | 版本列表 UI 未实现 |
| 12 | 发布状态更新 | publishStatus 更新 UI 未实现 |
| 13 | 历史 schema demo 值迁移 | Stage 07 明确不做 migration；已保存 schema 中的旧 `Sales.*` 值打开时保留 | 如需治理历史数据，后续单独设计迁移与用户确认 |
| 14 | Start / End 节点 | Stage 12 已完成 Start caption/documentation、outgoing summary、多 Start warning；End caption/documentation、incoming summary、returnType/returnValue 基础编辑与保存恢复 |
| 15 | Parameter 配置 | Stage 12 已完成 Parameter name/type/required/defaultValue/description 基础编辑，空名与重名 warning/validation |
| 16 | 微流输入参数基础定义 | Stage 12 已完成 Parameter node 与 schema-level `parameters` 同步，创建/修改/删除/复制均写入当前 active microflow schema |
| 17 | 微流返回值基础定义 | Stage 12 已完成 End `returnValue` 与 schema-level `returnType` 基础保存；表达式仅保存不执行 |
| 18 | 参数与 schema-level parameters 同步 | Stage 12 已完成 helper 与测试覆盖；A/B 微流 schema 独立，参数不会跨微流污染 |
| 19 | Create Variable 节点属性配置 | Stage 13 已完成 variableName/dataType/initialValue/documentation 基础编辑；变量定义由 createVariable action 派生进入 `schema.variables` |
| 20 | Change Variable 节点属性配置 | Stage 13 已完成 target selector、新值表达式与缺失 target warning；target 来自当前微流 variable index |
| 21 | 微流变量索引 | Stage 13 已完成 `buildMicroflowVariableIndex` helper 与派生 `MicroflowVariableIndex`；包含 parameters、Create Variable 与既有 action outputs |
| 22 | 后续节点变量选择 | Stage 13 已完成 Change Variable `scopeMode=index` selector；严格拓扑作用域校验留到后续 Stage 20 |
| 23 | 变量保存刷新恢复 | Stage 13 已完成变量节点/action config/schema.variables 同步测试；保存仍复用 `PUT /api/microflows/{activeMicroflowId}/schema` |

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
