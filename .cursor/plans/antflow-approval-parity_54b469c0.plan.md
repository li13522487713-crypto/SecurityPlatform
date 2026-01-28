---
name: antflow-approval-parity
overview: 调研结论：本项目当前审批流实现与桌面 `AntFlow.net-master` 的能力不一致，现仅覆盖基础“定义/发起/待办/同意/驳回(终止)/取消/历史事件”，缺少 AntFlow.net 的大量运行时操作与完整流转引擎能力。按你选择的路线：以现有 `Atlas.*.Approval` 模块为基线，在保持 Clean Architecture 的前提下逐项补齐到与 AntFlow.net 能力等价（接口不强制兼容，但需提供迁移/适配层）。
todos:
  - id: inventory-gap
    content: 把 AntFlow.net 的 OperationType/按钮能力映射成 Atlas 的能力对照清单（含缺失项与优先级）
    status: completed
  - id: core-model
    content: 补齐领域实体/枚举与仓储：流程变量、操作记录、转办/委托/加签记录、缺失审批人策略等
    status: completed
  - id: engine-router
    content: 升级流程推进：多节点、条件分支、会签/或签/顺序会签，并接入操作分发器
    status: completed
  - id: runtime-ops
    content: 实现关键运行时操作：撤回、打回修改/退回任意节点、重新提交、转办、委托、加签/减签、撤销同意、变更处理人/未来处理人
    status: completed
  - id: api-contracts
    content: 扩展 WebAPI + DTO/Validator，并同步更新 `Bosch.http/Approval.http` 用例
    status: completed
  - id: quality-gate
    content: 全量 `dotnet build` 0 warnings；修复所有新增警告/问题
    status: completed
  - id: persistence-parity-audit
    content: 再次对照 AntFlow 的持久化模型（FreeSql CodeFirst 映射 + `bpm_init_db_*.sql`），梳理 Atlas 审批模块仍缺失/未覆盖的“落库配置/运行态表”（例如：按钮配置、变量配置、通知模板、外部系统回调记录/重试队列、版本信息等），并决定哪些放入 `DefinitionJson`、哪些必须结构化落库
    status: completed
  - id: approval-seed-codefirst
    content: 增加“审批模块种子数据（Code First + 幂等）”机制（可开关）：默认按钮能力/操作集合映射、默认流程分类/模板（如需要）、示例流程定义（可选），避免依赖 AntFlow 的 SQL 脚本初始化方式
    status: completed
  - id: approval-runtime-restart-persistence
    content: 审查并补齐“重启可恢复”的运行时状态持久化：节点执行记录/并行标记、关键操作的幂等键与重复提交保护，确保服务重启后审批实例可继续流转且不依赖内存状态
    status: completed
  - id: approval-db-indexing
    content: 为审批高频查询表补齐必要索引/约束（例如按 TenantId+Assignee+Status 的任务查询、按 InstanceId 的历史/节点记录查询等），并验证 CodeFirst 会生成（或以 SqlSugar 特性声明）
    status: completed
  - id: flow-node-types-parity
    content: 补齐 AntFlow 节点类型能力差异（P0）：网关/条件/并行网关、抄送节点、接入方条件节点；并把 DefinitionJson 解析器/引擎推进逻辑扩展到可正确“分支/并行/汇聚/抄送”
    status: completed
  - id: condition-evaluator-parity
    content: 落地条件规则评估器（P0）：支持 AntFlow 的条件组关系/操作符（参考 ConditionRelationShipEnum、JudgeOperatorEnum、ConditionTypeEnum 等），并从流程变量/表单字段中取值进行路由判断（替换当前 EvaluateNextNodesAsync 的“直接通过”占位实现）
    status: completed
  - id: sequential-approval-order
    content: 顺序会签按“前端传入顺序/人员列表顺序”严格推进（P0）：当前仅等价于 All，会导致与 AntFlow SIGN_TYPE_SIGN_IN_ORDER 不一致；需支持逐个激活/逐个完成
    status: completed
  - id: assignee-strategies-parity
    content: 补齐 AntFlow 节点属性/审批人规则（P0）：层层审批(Loop)、指定层级(Level)、直属领导(DirectLeader)、发起人(StartUser)、HRBP、自选模块(Customize)、关联业务表(BusinessTable)、外部传入人员(OutSideAccess)；并与 MissingAssigneeProcessStrategyEnum（不允许/跳过/转管理员）完全对齐
    status: completed
  - id: deduplication-parity
    content: 实现审批人去重策略（P1）：对齐 AntFlow 的前向/后向去重与排除规则（BpmnDeduplicationFormatService），含并行网关场景下递归遍历，避免重复生成任务/重复审批
    status: completed
  - id: runtime-ops-missing-batch
    content: 补齐仍未实现的运行时操作（P0，对照 ProcessOperationEnum/ButtonTypeEnum）：承办(Undertake)、转发(Forward)、变更处理人(ChangeAssignee)、变更未来节点处理人(ChangeFutureAssignee)、未来节点加签/减签(Add/RemoveFutureAssignee)、保存草稿(SaveDraft)、恢复已结束流程(RecoverToHistory)、流程推进(ProcessMoveAhead/管理员跳过)、减签(RemoveAssignee)、加批(AddApproval/生成新节点语义)
    status: completed
  - id: button-type-gap
    content: 补齐 ButtonTypeEnum 与操作语义差异（P2）：打回上节点修改(BUTTON_TYPE_BACK_TO_PREV_MODIFY)、预览/打印等按钮（前端能力+后端权限校验/审计记录），明确哪些仅 UI 按钮无需后端操作、哪些需要落库/回调
    status: completed
  - id: copy-node-runtime
    content: 抄送节点落地（P1）：生成抄送记录/收件人列表、与待办/已办列表的展现隔离、抄送“已读/未读”持久化（AntFlow 有专门的 remove-copy format 与消息体系）
    status: completed
  - id: notification-system-parity
    content: 消息通知体系对齐（P1）：对齐 AntFlow 的消息发送适配器（Email/SMS/AppPush）与模板（InformationTemplate/BpmnConfNoticeTemplate*），并在关键操作事件（MsgProcessEventEnum/MsgNoticeTypeEnum）触发发送与站内信落库
    status: completed
  - id: overtime-remind-parity
    content: 催办/超时提醒（P1）：对齐 BpmnApproveRemind、BpmProcessNodeOvertime、变量级提醒配置（BpmnTimeoutReminder*），实现定时扫描/发送（HostedService/后台任务）并保证幂等
    status: completed
  - id: outside-process-callback
    content: 外部系统流程/回调能力补齐（P1）：对齐 OutSideBpmCallbackUrlConf + OutSideCallBackRecord（记录回调类型、重试次数、状态），实现回调分发(ThirdPartyCallbackFactory)与失败重试；同时补齐安全校验与幂等
    status: completed
  - id: user-role-integration-contracts
    content: “接入自有用户/角色系统”扩展点（P1）：对齐 AntFlow 文档的 UserService/RoleService 替换思路，抽成 Atlas 的接口契约（按用户/角色/部门/直属领导/HRBP 查询最小能力），避免审批规则被 demo 表绑死
    status: pending
  - id: process-mgmt-extras
    content: 流程管理附加能力差异清单（P2）：流程类型/分类、快捷入口、流程权限/可见范围、版本信息(SysVersion)等（先对比是否需要纳入 Atlas 范围，必要时再实现）
    status: completed
isProject: false
---

# AntFlow.net 审批流能力对齐计划（基于现有 Atlas.Approval 扩展）

## 结论（是否一致）

- **不一致**：本项目当前实现是一个 MVP 级自研轻量引擎。
- 关键证据：`ApprovalRuntimeCommandService` 的推进逻辑当前仅做“所有任务都 Approved 则实例 Completed”，未解析/执行多节点、多分支、会签策略、回退/撤回等（见 `D:/Code/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/ApprovalRuntimeCommandService.cs`）。
- AntFlow.net 明确支持大量“按钮操作/运行时操作”与复杂回退/撤回/加签/转办等（见 `C:/Users/kuo13/Desktop/AntFlow.net-master/antflowcore/constant/enums/ProcessOperationEnum.cs` 与 `.../ButtonTypeEnum.cs`）。

## 当前 Atlas 已有能力（现状基线）

- **流程定义**：创建/更新(草稿)/发布/禁用/删除(草稿)、分页查询、按状态/关键词筛选。
- **流程实例**：发起（绑定 `BusinessKey` + `DataJson`）、取消、查询我的发起、实例详情。
- **任务**：生成第一批任务（仅首个 approve/condition 节点）、我的待办、同意、驳回（驳回即终止实例并取消其余待办）。
- **审批人策略（简化）**：指定用户、角色（未展开）、部门负责人。
- **审计/历史**：`ApprovalHistoryEvent` 记录启动/创建任务/同意/驳回/完成/取消等事件。

## AntFlow.net 目标能力范围（以其“按钮操作/运行时操作”为准）

- **运行时按钮/操作类型**（部分）：重新提交、打回修改、退回任意节点、撤回、撤销同意、加批、加签/减签、转办、变更当前/未来节点处理人、缺失审批人策略（不允许/跳过/转管理员）等。
- **业务表单回调适配**：通过 `IFormOperationAdaptor<T>` 在关键节点回调业务处理（提交/查询/同意/打回修改/取消/结束）。

## 实施路线（严格按“底层→上层”，避免过度设计）

### 1) 领域模型与持久化（底层）

- **补齐运行态核心概念**（尽量复用现有实体，必要时新增）：
- **流程图执行状态**：当前节点/并行 token（如果需要并行网关）、节点执行记录、流程变量。
- **任务归属与委托/转办记录**：转办/委托关系、原处理人/现处理人。
- **操作记录与按钮能力**：记录一次“操作类型 + 操作人 + 影响的任务/节点”。
- **新增枚举/值对象**：对齐 AntFlow 的操作语义（不必复刻同名同码，但要覆盖能力）。
- **仓储接口/实现**：为上述新实体提供最小 CRUD + 查询（按现有 Repository 模式）。

### 2) 引擎执行与操作分发（核心引擎层）

- **重写/替换推进逻辑**：把当前“全任务同意即结束”的 MVP 推进，升级为：
- 解析 `DefinitionJson` 得到节点/连线/条件
- 支持顺序流转、多节点、多分支、条件路由
- 支持会签/或签/顺序会签（以节点配置驱动）
- **引入“操作适配器/处理器”最小框架**（不做过度抽象）：
- 一个 `OperationDispatcher`（根据 OperationType 调用具体处理器）
- 逐个实现 AntFlow 对应操作：撤回、打回修改、退回任意节点、重新提交、转办、委托、加签/减签、撤销同意、变更处理人等
- **缺失审批人策略**：实现与 `MissingAssigneeProcessStrategyEnum` 等价行为（不允许/跳过/转管理员）。

### 3) 业务表单回调（应用层扩展点）

- 在 `Atlas.Application.Approval` 增加一个与 `IFormOperationAdaptor<T>` 等价的接口（或直接复用其思想），让“业务模块”可注册回调：
- 发起前/后、审批同意后、打回修改后、取消后、结束后。
- 先提供一个默认空实现，确保审批流模块可独立运行；再按业务需要逐步接入。

### 4) WebAPI 与 DTO/校验（上层）

- 在保持现有端点可用前提下，**新增**运行时操作端点（或在现有 Tasks/Runtime 控制器下扩展），覆盖 AntFlow 的操作集合。
- 为每个新/变更端点补齐 FluentValidation 校验与错误码。
- 按仓库规范：**每新增/修改端点同步更新 `src/backend/Atlas.WebApi/Bosch.http/Approval.http`**。

### 5) 验证与质量门禁

- 运行 `dotnet build`，确保 **0 error / 0 warning**。
- 针对关键路径写最小 `.http` 用例：发起→多节点审批→退回任意节点→重新提交→撤回→转办/委托→加签。

## 关键参考定位（用于实现对照）

- Atlas 当前引擎核心：`D:/Code/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/ApprovalRuntimeCommandService.cs`
- AntFlow 操作枚举：`C:/Users/kuo13/Desktop/AntFlow.net-master/antflowcore/constant/enums/ProcessOperationEnum.cs`
- AntFlow 表单回调适配：`C:/Users/kuo13/Desktop/AntFlow.net-master/antflowcore/adaptor/formoperation/IFormOperationAdaptor.cs`
- AntFlow 关键操作样例：
- 转办：`.../antflowcore/adaptor/processoperation/TransferAssigneeProcessService.cs`
- 加签：`.../antflowcore/adaptor/processoperation/AddAssigneeProcessService.cs`
- 打回/撤回/撤销同意/退回任意节点：`.../antflowcore/adaptor/processoperation/BackToModifyService.cs`
- 重新提交/加批：`.../antflowcore/adaptor/processoperation/ResubmitProcessService.cs`

## 风险与约束

- 你选择“不强制接口兼容”，但仍建议做一个薄的适配层，方便未来外部系统按 AntFlow 文档接入。
- 为避免过度设计：优先实现 AntFlow 枚举中最常用的运行时操作（撤回/打回修改/退回任意节点/重新提交/转办/委托/加签），其余按需求补齐。