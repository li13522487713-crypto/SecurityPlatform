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
| 5 | App Explorer 中 Microflows 分组真实管理多个微流 | Stage 04 已完成真实列表 + CRUD 入口；Stage 05-06 已接 Workbench 与真实编辑器；Stage 15 已接 Call Microflow 真实 metadata 目标列表 | Domain Model metadata 等深度能力仍在后续阶段 | Stage 03-06 / Stage 15 分阶段完成 |
| 6 | 微流 CRUD 入口 | Stage 04 已完成 | 新建 / 重命名 / 复制 / 删除均已通过真实 Microflow Resource API 接入 App Explorer | **Stage 04** |
| 7 | 点击微流真实打开指定 microflowId 的画布 | Stage 06 已完成：activeMicroflowId 打开嵌入式真实 MicroflowEditor | 深度属性与 metadata 仍在后续阶段 | **Stage 06** |
| 8 | 画布按 microflowId 保存和加载 | Stage 06 已完成：GET resource/schema，PUT schema 保存 | 未保存切换 guard 待后续增强 | **Stage 06** |
| 9 | 节点拖拽后真实进入当前微流定义 | Stage 08 已完成专项验收与修复：NodePanel payload -> FlowGram drop -> authoring schema objectCollection | 不新增节点类型 | **Stage 08** |
| 10 | 节点位置、类型、名称、配置真实保存 | Stage 09 已完成节点移动/删除/复制/重命名基础编辑持久化；随 Stage 06 save bridge 保存 | 节点属性深度增强留到后续阶段 | **Stage 09 / 后续增强** |
| 11 | 连接线可以创建、删除、保存 | Stage 10 已完成：FlowGram connect/delete 真实写回当前 microflow authoring schema，保存进入 `PUT /api/microflows/{id}/schema` | 复杂属性表单与运行语义留到后续阶段 | **Stage 10** |
| 12 | 节点属性面板可以编辑并保存 | Stage 11 已完成基础表单；Stage 15 已完成 Call Microflow 真实 target、parameterMappings、return binding 写回当前 schema | Domain Model metadata 仍在后续阶段 | **Stage 11 / Stage 15** |
| 13 | Call Microflow 目标选择 | Stage 15 已完成真实目标微流选择与 `targetMicroflowId` / `targetMicroflowQualifiedName` / `targetMicroflowName` 保存；Stage 16 已验证 target rename 后以 `targetMicroflowId` 保持稳定 | 后端执行器不在 Stage 16 范围 | Stage 15 / Stage 16 |
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
| 25 | 保存校验 / Save Gate | Stage 20 已完成：Save 前先本地 validation，本地无 error 后接后端 validate，error 阻止 `PUT /api/microflows/{id}/schema`，warning 展示但允许保存 | 后端执行引擎不在本阶段 | **Stage 20** |
| 26 | Problems 面板 | Stage 20 已完成：底部 Problems panel 展示 error/warning/info，支持 severity/source/keyword 筛选、source 分组、空状态与点击定位 | 字段级滚动高亮仍可后续增强 | **Stage 20** |
| 27 | 端口规则 / 未连接节点提示 | Stage 20 已完成：悬挂连线、非法 source/target/port、重复连线、Start 无出边、不可达/死路节点均进入 validation issues | 复杂端口运行语义后续增强 | **Stage 20** |
| 28 | 重复名称 / 空 target 提示 | Stage 20 已完成：重复参数、重复变量、参数/变量冲突、Change Variable/List/Object/Call Microflow 空 target 可见 | warning/error 策略按保存门禁执行 | **Stage 20** |
| 29 | 无效 entity / 引用失效提示 | Stage 20 已完成：Domain Model entity/member/association/enumeration stale、Call Microflow stale/not found 进入统一 issue model | metadata 缺失时不 fallback mock | **Stage 20** |
| 30 | 分支缺失 / 循环非法提示 | Stage 20 已完成：Decision missing true/false、duplicate true/false、Merge 入/出不足、Loop body/exit 缺失、Break/Continue outside loop/target stale 可见 | 不做完整循环调用图算法 | **Stage 20** |

---

## P1 Gap（引用 / 版本 / 发布）

| # | 能力项 | Gap |
|---|---|---|
| 10 | 删除前引用预检查 | Stage 16 已增强：App Explorer 删除前展示 callers，active callers 阻止删除；预检查失败不放行；DELETE 409 打开 references drawer 并保留树节点（见注 B） |
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
| 24 | Decision / If 节点属性配置 | Stage 14 已完成 ExclusiveSplit expression/resultType/rule 基础配置、branch summary 与空表达式/缺分支 warning |
| 25 | true / false 出边规则 | Stage 14 已完成 `caseValues` true/false helper、重复 case warning、FlowEdgeForm selector 编辑与删除 flow 后 case 清理 |
| 26 | Merge 节点属性配置 | Stage 14 已完成 ExclusiveMerge `firstArrived` 策略展示/编辑、incoming/outgoing summary 与 warning |
| 27 | 分支建模基础保存刷新恢复 | Stage 14 已完成 Decision expression、flow label/caseValues、connection index、Merge behavior 的 schema 级测试；保存仍复用 `PUT /api/microflows/{activeMicroflowId}/schema` |
| 28 | Call Microflow 参数映射 | Stage 15 已完成：目标参数来自真实 metadata API，mapping expression/source variable 写入当前 schema，required 缺失有 warning |
| 29 | Call Microflow 返回值绑定 | Stage 15 已完成：returnType 展示，当前变量/新变量基础绑定写入 `returnValue.outputVariableName`，Void return 禁用并清空 |
| 30 | Call Microflow 引用关系基础写入 | Stage 15 已完成：schema 中保留 `kind=callMicroflow`、`targetMicroflowId`、`targetMicroflowQualifiedName`，供后端 reference scanner 重建 |
| 31 | mock metadata 替换为真实 metadata | Stage 15 已完成：mendix-studio 嵌入路径通过 HTTP metadata adapter 调用真实 metadata API；Provider 缺 adapter/失败不 fallback mock |
| 28 | MicroflowReference 引用关系保存、查询、删除保护 | Stage 16 已完成 callers 查询与展示、schema 保存后后端重建、duplicate 后重建新 source outgoing refs、删除保护 UI 与 409 处理；callees 当前从 schema 解析 |
| 29 | Call Microflow target 重命名后引用稳定 | Stage 16 已完成：`targetMicroflowId` 为权威，qualifiedName 仅作显示快照；资源列表刷新后 UI 用最新 target 名称展示 |
| 30 | 被引用提示 / referenceCount 展示 | Stage 16 已完成 App Explorer 后端 `referenceCount` badge；保存、重命名、复制、删除后重新拉取资源列表，不伪造计数 |
| 31 | Loop 节点属性配置 | Stage 17 已完成：`loopedActivity.loopSource` 支持 forEach / while、source list expression、condition expression、iterator name/type、flow summary 与 warning；保存仍复用当前 active microflow 的 schema PUT |
| 32 | Break / Continue 节点属性配置 | Stage 17 已完成：`breakEvent` / `continueEvent` 支持 caption/documentation、`targetLoopObjectId`、incoming/outgoing summary 与合法性 warning |
| 33 | Loop variable 基础索引 | Stage 17 已完成：Loop iterator 从当前 schema 派生进入 `schema.variables.loopVariables`，source=`loopIterator`、scope=`loop`；严格拓扑作用域校验后置 Stage 20 |
| 34 | Loop body / exit flow 基础保存刷新恢复 | Stage 17 已完成：Loop body 使用 `originConnectionIndex=2`，after/exit 使用 `originConnectionIndex=1`，FlowEdgeForm 可识别与设置；刷新后由 handle/index 恢复 |
| 35 | Break/Continue 合法性 warning | Stage 17 已完成：无 Loop、未处于 Loop body、target stale、多 Loop ambiguous、存在 outgoing flow 均显示 warning；不实现完整拓扑执行顺序分析 |
| 36 | List / Collection 节点属性配置 | Stage 18 已完成 Create List / Change List / Aggregate List / List Operation 基础属性面板；字段写回当前 active microflow schema |
| 37 | Create List / Change List / Aggregate List / List Operation 基础能力 | Stage 18 已完成集合变量创建、List selector、聚合 result variable、List Operation output variable 基础建模；不执行表达式或集合运行逻辑 |
| 38 | List variable index | Stage 18 已完成 `createList`、`aggregateList`、`listOperation` 变量索引扩展；selector 仅显示当前 schema 的 List 类型变量 |
| 39 | 集合变量保存刷新恢复 | Stage 18 已完成 schema 字段与派生 `variables` 同步；保存仍复用 `PUT /api/microflows/{activeMicroflowId}/schema`，A/B 微流索引隔离通过测试覆盖 |
| 40 | Object Activity 节点属性配置 | Stage 19 已完成：Create/Retrieve/Change/Commit/Delete/Rollback Object 基础属性表单写回当前 active schema，保存仍复用 `PUT /api/microflows/{activeMicroflowId}/schema` |
| 41 | Domain Model 实体绑定 | Stage 19 已完成：Object Activity Entity selector 使用真实 `GET /api/microflow-metadata` catalog，不 fallback mock，不新增 mock API |
| 42 | 字段 / 关联 / 枚举 metadata 绑定 | Stage 19 已完成：memberChanges 支持 attribute/association selector，enumeration attribute 支持 enum value selector，metadata 缺失时保留 stale 配置并 warning |
| 43 | Object variable index | Stage 19 已完成：Create Object 输出 Object(entity)，Retrieve Object 根据 range 输出 Object(entity) 或 List<Object(entity)>；Commit/Delete/Rollback selector 过滤 Object/List<Object> |
| 44 | Create/Retrieve/Change/Commit/Delete Object 基础保存刷新恢复 | Stage 19 已完成 schema helper 与测试覆盖；A/B 微流基于当前 schema 重建 index，Object Activity 配置不跨微流污染 |

---

## P2 Gap（运行入口 / 运行契约）

| # | 能力项 | Gap |
|---|---|---|
| 45 | 运行输入面板 | Stage 21 已完成：编辑器 toolbar Run 打开真实 Run input panel，读取当前 active microflow 的 `schema.parameters`，无 schema-level parameters 时仅 fallback Parameter nodes 并显示 warning |
| 46 | 运行 API 契约 | Stage 21 已完成：Workbench 嵌入编辑器传入 HTTP `runtimeAdapter`，Run 调用真实 `POST /api/microflows/{activeMicroflowId}/test-run`；当前后端无独立 `/run`，本轮不新增重复 endpoint |
| 47 | 参数输入 DTO | Stage 21 已完成：请求以 `{ schema, input, options }` 对齐后端 `TestRunMicroflowApiRequest`，前端 request 同时保留 `microflowId` 用于 active path，`input` key 使用参数名且 value 先完成类型转换 |
| 48 | 运行结果面板基础展示 | Stage 21 已完成：Run 面板和底部 Debug 面板展示真实后端 session、runId、status、duration、output、error、logs 与 trace frame output 摘要，不伪造 success |
| 49 | validation gate 阻止运行 | Stage 21 已完成：工具栏 Run 和面板 Run 都复用 Stage 20 `testRun` validation；本地/服务端 error、required 缺失、类型转换错误均阻止调用 run API；dirty schema 采用 Save & Run 策略 |

---

## 注记（Stage 02 修正版）

### 注 A：Local Adapter 持久化边界

`createLocalMicroflowApiClient` / Local Adapter **不属于**后端真实保存。
它可能使用 `localStorage`，也可能 fallback 到内存，**不能**作为真实持久化验收依据。

### 注 B：微流引用保护现状

后端 `MicroflowResourceService.DeleteAsync` 已调用 `EnsureNoActiveTargetReferencesAsync`，
因此**后端具备被引用保护**。

前端缺口已在 **Stage 16** 增强：App Explorer 删除入口会先调用
`GET /api/microflows/{id}/references` 做 active callers 预检查；
如果存在 active callers，前端打开 references drawer 并禁止调用 DELETE。
若后端 `DELETE /api/microflows/{id}` 仍返回 409 / `MICROFLOW_REFERENCE_BLOCKED`，
前端会展示后端错误、打开 references drawer、刷新引用关系并保留树节点。

### 注 D：Stage 04 已覆盖的 CRUD 缺口

- “新建微流真实创建 MicroflowResource”：已完成，入口位于 App Explorer 的 `Microflows` 分组右键菜单，调用 `resourceAdapter.createMicroflow`。
- “微流重命名”：已完成，入口位于真实微流节点右键菜单，调用 `resourceAdapter.renameMicroflow`，resource id 保持不变。
- “复制微流”：已完成，入口位于真实微流节点右键菜单，调用 `resourceAdapter.duplicateMicroflow`，返回新 resource id。
- “删除微流前检查是否被其他微流引用”：Stage 16 已增强，删除确认前调用 `resourceAdapter.getMicroflowReferences`，active callers 会阻止删除并展示来源；后端 409 作为最终保护且不会移除前端树节点。

### 注 C：Stage 02 边界（修正）

第 2 轮**只做 asset foundation / context / model / adapter bundle**，
不做整个 P0。具体范围见 `microflow-stage-01-inventory.md` Stage 边界说明表。
