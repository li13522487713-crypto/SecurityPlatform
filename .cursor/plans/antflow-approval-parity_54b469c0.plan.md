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