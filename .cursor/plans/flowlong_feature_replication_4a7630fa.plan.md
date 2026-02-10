---
name: FlowLong Feature Replication
overview: Replicate all FlowLong workflow engine capabilities into SecurityPlatform, covering both backend (C#/.NET) and frontend (Vue 3/TypeScript) changes across 5 implementation phases.
todos:
  - id: phase1-backend-enums
    content: "阶段1: 后端 - 扩展枚举 (ApprovalMode, FlowNodeType, ApprovalInstanceStatus, ApprovalTaskStatus, 新建 RejectStrategy/ReApproveStrategy)"
    status: completed
  - id: phase1-backend-entities
    content: "阶段1: 后端 - 扩展实体 (ApprovalTask, ApprovalProcessInstance) + 新建实体 (DelegateRecord, AgentConfig, SubProcessLink)"
    status: completed
  - id: phase1-backend-parser
    content: "阶段1: 后端 - 扩展 FlowDefinitionParser (FlowNode 新属性, FlowDefinition 新方法)"
    status: completed
  - id: phase1-backend-engine
    content: "阶段1: 后端 - 扩展 FlowEngine (票签逻辑, 包容/路由/子流程/定时器/触发器节点处理)"
    status: completed
  - id: phase1-backend-services
    content: "阶段1: 后端 - 扩展 CommandService + API (委派/挂起/激活/终止/草稿/认领 等端点)"
    status: completed
  - id: phase1-frontend-types
    content: "阶段1: 前端 - 扩展 approval-tree.ts 类型定义 (新节点类型 + 新属性)"
    status: completed
  - id: phase1-frontend-palette
    content: "阶段1: 前端 - 扩展 ApprovalNodePalette.vue (新增包容/路由/子流程/定时器/触发器节点)"
    status: completed
  - id: phase1-frontend-props
    content: "阶段1: 前端 - 扩展 ApprovalPropertiesPanel.vue (票签/驳回策略/新节点配置面板)"
    status: completed
  - id: phase1-frontend-tree
    content: "阶段1: 前端 - 扩展 useApprovalTree + converter + validator (新节点类型支持)"
    status: completed
  - id: phase1-frontend-shapes
    content: "阶段1: 前端 - 新增 X6 节点形状组件 (5个 Shape + 5个 Widget)"
    status: completed
  - id: phase2-backend-handlers
    content: "阶段2: 后端 - 新增操作处理器 (跳转/拿回/唤醒/认领/催办/沟通/离职转办/追加)"
    status: completed
  - id: phase2-backend-jobs
    content: "阶段2: 后端 - 新增后台调度 Job (超时/提醒/定时器/触发器)"
    status: completed
  - id: phase2-frontend-runtime
    content: "阶段2: 前端 - 运行时操作面板 + 新页面 (任务池/沟通面板/跳转选择器/批量转办)"
    status: completed
  - id: phase2-frontend-api
    content: "阶段2: 前端 - 扩展 api.ts (所有新操作的 API 调用方法)"
    status: completed
  - id: phase3-features
    content: "阶段3: 暂存待审 + 穿越时空 + 已阅 + 离职转办 + 挂起激活 + 追加 + 分组策略 + 模型缓存"
    status: completed
  - id: phase4-ai
    content: "阶段4: AI审批Handler + 异步子流程 + 流程模型缓存优化"
    status: completed
  - id: phase5-runtime-pages
    content: "阶段5: 前端运行时页面 (实例详情/任务审批/流程图状态/时间线/表单渲染/导出导入)"
    status: completed
isProject: false
---

# FlowLong 完整能力复刻实施计划

基于对 FlowLong 项目和 SecurityPlatform 现有代码的深度分析，以下是分前后端、分阶段的详细实施步骤。

---

## 阶段一：核心审批模式与分支类型补全

### 后端改动

#### 1.1 新增/扩展枚举

**文件**: [Atlas.Domain.Approval/Enums/ApprovalMode.cs](src/backend/Atlas.Domain.Approval/Enums/ApprovalMode.cs)

- 新增 `Vote = 3`（票签：按权重投票，超过指定比例即通过）

**文件**: [Atlas.Domain.Approval/Enums/FlowNodeType.cs](src/backend/Atlas.Domain.Approval/Enums/FlowNodeType.cs)

- 新增 `InclusiveGateway = 8`（包容分支：条件+并行结合体）
- 新增 `RouteGateway = 9`（路由分支：重定向到指定节点）
- 新增 `CallProcess = 10`（子流程节点）
- 新增 `Timer = 11`（定时器节点）
- 新增 `Trigger = 12`（触发器节点）

**文件**: [Atlas.Domain.Approval/Enums/ApprovalInstanceStatus.cs](src/backend/Atlas.Domain.Approval/Enums/ApprovalInstanceStatus.cs)

- 新增 `Suspended = -2`（挂起/暂停）
- 新增 `Draft = -1`（暂存待审/草稿）
- 新增 `Terminated = 5`（强制终止，区别于 Canceled 发起人撤销）
- 新增 `TimedOut = 6`（超时结束）
- 新增 `AutoApproved = 7`（自动通过）
- 新增 `AutoRejected = 8`（自动拒绝）

**文件**: [Atlas.Domain.Approval/Enums/ApprovalTaskStatus.cs](src/backend/Atlas.Domain.Approval/Enums/ApprovalTaskStatus.cs)

- 新增 `Delegated = 5`（已委派）
- 新增 `Claimed = 6`（已认领）
- 新增 `AutoApproved = 7`（自动通过）
- 新增 `AutoRejected = 8`（自动拒绝）

新建枚举文件 `RejectStrategy.cs`:

```csharp
public enum RejectStrategy
{
    ToPrevious = 1,    // 退回上一步
    ToInitiator = 2,   // 退回发起人
    ToAnyNode = 3      // 退回任意节点
}
```

新建枚举文件 `ReApproveStrategy.cs`:

```csharp
public enum ReApproveStrategy
{
    Continue = 1,       // 从驳回节点继续往后执行
    BackToRejectNode = 2 // 重新从驳回目标节点开始审批
}
```

#### 1.2 扩展实体

**文件**: [Atlas.Domain.Approval/Entities/ApprovalTask.cs](src/backend/Atlas.Domain.Approval/Entities/ApprovalTask.cs)

- 新增 `Weight` 属性 (int?, 票签权重比例)
- 新增 `ParentTaskId` 属性 (long?, 父任务ID，用于委派/加签追踪)
- 新增 `DelegatorUserId` 属性 (long?, 委派人ID)
- 新增 `ViewedAt` 属性 (DateTimeOffset?, 已阅时间)
- 新增 `TaskType` 属性 (int, 任务类型: 0=主办 1=审批 2=抄送 10=转办 11=委派 12=委派归还 13=代理)
- 新增方法: `Delegate()`, `ClaimBack()`, `MarkViewed()`, `SetWeight()`

新建实体 `ApprovalDelegateRecord.cs`:

- `Id`, `TenantId`, `TaskId`, `DelegatorUserId`, `DelegateeUserId`, `Status`, `CreatedAt`, `CompletedAt`

新建实体 `ApprovalAgentConfig.cs`:

- `Id`, `TenantId`, `AgentUserId`, `PrincipalUserId`, `StartTime`, `EndTime`, `IsEnabled`, `CreatedAt`

新建实体 `ApprovalSubProcessLink.cs`:

- `Id`, `TenantId`, `ParentInstanceId`, `ParentNodeId`, `ChildInstanceId`, `ChildProcessId`, `IsAsync`, `Status`, `CreatedAt`, `CompletedAt`

**文件**: [Atlas.Domain.Approval/Entities/ApprovalProcessInstance.cs](src/backend/Atlas.Domain.Approval/Entities/ApprovalProcessInstance.cs)

- 新增 `ParentInstanceId` 属性 (long?, 父流程实例ID)
- 新增 `Priority` 属性 (int?, 优先级)
- 新增 `InstanceNo` 属性 (string?, 流程编号)
- 新增 `CurrentNodeName` 属性 (string?, 当前节点名称)
- 新增方法: `Suspend()`, `Activate()`, `SaveAsDraft()`, `Terminate()`

#### 1.3 扩展流程定义解析器

**文件**: [Atlas.Infrastructure/Services/ApprovalFlow/FlowDefinitionParser.cs](src/backend/Atlas.Infrastructure/Services/ApprovalFlow/FlowDefinitionParser.cs)

- `FlowNode` 类新增属性:
  - `Weight` (int?, 票签默认权重比例)
  - `VotePassRate` (int?, 票签通过比率，如 50 表示 50%)
  - `RejectStrategy` (RejectStrategy?, 驳回策略)
  - `ReApproveStrategy` (ReApproveStrategy?, 重新审批策略)
  - `CallProcessId` (long?, 子流程定义ID)
  - `CallAsync` (bool, 是否异步子流程)
  - `TimerConfig` (string?, 定时器配置JSON)
  - `TriggerType` (string?, 触发器类型: immediate/scheduled)
  - `GroupStrategy` (int?, 分组策略: 0=认领 1=全员审批)
- `FlowDefinition` 类新增方法:
  - `IsInclusiveSplitGateway(nodeId)` / `IsInclusiveJoinGateway(nodeId)`
  - `GetRouteTarget(nodeId)` 获取路由目标节点

#### 1.4 扩展流程引擎

**文件**: [Atlas.Infrastructure/Services/ApprovalFlow/FlowEngine.cs](src/backend/Atlas.Infrastructure/Services/ApprovalFlow/FlowEngine.cs)

- `CheckNodeCompletion()` 新增票签逻辑:
  ```csharp
  case ApprovalMode.Vote:
      var totalWeight = tasks.Sum(t => t.Weight ?? 1);
      var approvedWeight = approvedTasks.Sum(t => t.Weight ?? 1);
      var passRate = node.VotePassRate ?? 50;
      return (approvedWeight * 100 / totalWeight) >= passRate;
  ```

- `ProcessNextNodeAsync()` 新增分支:
  - `inclusiveGateway`: 类似并行但只走满足条件的分支（复用 `EvaluateNextNodesAsync` + token 机制）
  - `routeGateway`: 直接跳转到配置的目标节点
  - `callProcess`: 创建子流程实例并关联
  - `timer`: 创建定时任务记录，到时间后自动推进
  - `trigger`: 创建触发器记录（立即执行或定时执行）
- `EvaluateNextNodesAsync()` 扩展:
  - 包容网关：返回所有满足条件的分支（非排它，至少一条）
  - 路由网关：计算路由目标，返回重定向节点
- 新增方法:
  - `HandleInclusiveSplitAsync()` 处理包容分支的 fork/join
  - `HandleSubProcessAsync()` 创建子流程实例
  - `EndSubProcessAsync()` 子流程结束时回调主流程
  - `GenerateVoteTasksAsync()` 为票签模式生成带权重的任务

#### 1.5 扩展命令服务

**文件**: [Atlas.Infrastructure/Services/ApprovalRuntimeCommandService.cs](src/backend/Atlas.Infrastructure/Services/ApprovalRuntimeCommandService.cs)

新增方法:

- `DelegateTaskAsync()` — 委派任务（A 委派给 B，B 审批后还给 A）
- `ResolveTaskAsync()` — 委派归还（被委派人完成后归还）
- `StartSubProcessAsync()` — 启动子流程
- `SuspendInstanceAsync()` — 挂起实例
- `ActivateInstanceAsync()` — 激活实例
- `TerminateInstanceAsync()` — 管理员强制终止
- `SaveDraftAsync()` — 暂存待审（草稿）
- `SubmitDraftAsync()` — 提交草稿激活流程

**文件**: [Atlas.Application.Approval/Abstractions/IApprovalRuntimeCommandService.cs](src/backend/Atlas.Application.Approval/Abstractions/IApprovalRuntimeCommandService.cs)

同步新增对应接口方法。

#### 1.6 新增 API 端点

**文件**: [Atlas.WebApi/Controllers/ApprovalRuntimeController.cs](src/backend/Atlas.WebApi/Controllers/ApprovalRuntimeController.cs)

- `POST /api/v1/approval/instances/{id}/suspension` — 挂起实例
- `POST /api/v1/approval/instances/{id}/activation` — 激活实例
- `POST /api/v1/approval/instances/{id}/termination` — 管理员终止
- `POST /api/v1/approval/instances/draft` — 暂存草稿
- `POST /api/v1/approval/instances/{id}/submission` — 提交草稿

**文件**: [Atlas.WebApi/Controllers/ApprovalTasksController.cs](src/backend/Atlas.WebApi/Controllers/ApprovalTasksController.cs)

- `POST /api/v1/approval/tasks/{id}/delegation` — 委派
- `POST /api/v1/approval/tasks/{id}/resolution` — 委派归还
- `POST /api/v1/approval/tasks/{id}/claim` — 认领
- `POST /api/v1/approval/tasks/{id}/viewed` — 标记已阅

新建控制器 `ApprovalAgentController.cs`:

- `GET/POST/DELETE /api/v1/approval/agents` — 代理人配置 CRUD

### 前端改动

#### 1.7 扩展类型定义

**文件**: [src/frontend/Atlas.WebApp/src/types/approval-tree.ts](src/frontend/Atlas.WebApp/src/types/approval-tree.ts)

- `NodeType` 新增: `'inclusive'` | `'route'` | `'callProcess'` | `'timer'` | `'trigger'`
- `ApproveNode` 新增属性:
  - `voteWeight?: number` (票签默认权重)
  - `votePassRate?: number` (票签通过比率)
  - `rejectStrategy?: 'toPrevious' | 'toInitiator' | 'toAnyNode'`
  - `reApproveStrategy?: 'continue' | 'backToRejectNode'`
  - `groupStrategy?: 'claim' | 'allParticipate'`
- 新增接口:
  - `InclusiveNode` (类似 ConditionNode，走所有满足条件的分支)
  - `RouteNode` (路由节点，含 `routeTargetNodeId`)
  - `CallProcessNode` (子流程节点，含 `callProcessId`, `callAsync`)
  - `TimerNode` (定时器节点，含 `timerConfig`)
  - `TriggerNode` (触发器节点，含 `triggerType`, `triggerConfig`)
- `TreeNode` 联合类型新增上述节点

#### 1.8 扩展节点面板

**文件**: [src/frontend/Atlas.WebApp/src/components/approval/ApprovalNodePalette.vue](src/frontend/Atlas.WebApp/src/components/approval/ApprovalNodePalette.vue)

新增三组节点:

```
基础节点: 审批人、抄送人
分支控制: 条件分支、并行分支、包容分支、路由分支
高级节点: 子流程、定时器、触发器
```

新增图标: `NodeIndexOutlined` (包容分支), `SwapOutlined` (路由分支), `SubnodeOutlined` (子流程), `ClockCircleOutlined` (定时器), `ThunderboltOutlined` (触发器)

#### 1.9 扩展属性面板

**文件**: [src/frontend/Atlas.WebApp/src/components/approval/ApprovalPropertiesPanel.vue](src/frontend/Atlas.WebApp/src/components/approval/ApprovalPropertiesPanel.vue)

- 审批节点 Tab "审批设置" 新增:
  - 票签模式卡片（第4张 radio card）+ 权重百分比 + 通过比率滑块
  - 驳回策略下拉: 退回上一步 / 退回发起人 / 退回任意节点
  - 重新审批策略下拉: 继续执行 / 退回驳回节点
  - 分组策略: 认领模式 / 全员审批
- 新增模板: 包容分支配置（类似条件分支但允许多条命中）
- 新增模板: 路由分支配置（目标节点选择器）
- 新增模板: 子流程配置（流程定义选择 + 同步/异步开关）
- 新增模板: 定时器配置（延迟时间 + 定时执行策略）
- 新增模板: 触发器配置（立即/定时 + 触发类型）

#### 1.10 扩展树编辑器

**文件**: [src/frontend/Atlas.WebApp/src/composables/useApprovalTree.ts](src/frontend/Atlas.WebApp/src/composables/useApprovalTree.ts)

- `createNode()` 方法新增 `inclusive`, `route`, `callProcess`, `timer`, `trigger` 分支的工厂逻辑
- `addConditionBranch()` 支持包容分支节点
- `findNodeOrBranch()` 递归支持新节点类型

**文件**: [src/frontend/Atlas.WebApp/src/utils/approval-tree-converter.ts](src/frontend/Atlas.WebApp/src/utils/approval-tree-converter.ts)

- `treeNodeToDefinition()` 和 `definitionNodeToTree()` 新增所有新节点类型的序列化/反序列化
- 新增包容分支、路由分支转换为 graph edges 的逻辑

**文件**: [src/frontend/Atlas.WebApp/src/utils/approval-tree-validator.ts](src/frontend/Atlas.WebApp/src/utils/approval-tree-validator.ts)

- 新增包容分支校验（至少2个分支，至少一个非默认有条件）
- 新增路由分支校验（必须配置目标节点）
- 新增子流程校验（必须选择流程定义）
- 新增定时器校验（必须配置时间）

#### 1.11 新增 X6 节点形状组件

在 `src/frontend/Atlas.WebApp/src/components/approval/x6/shapes/` 下新建:

- `InclusiveBranchShape.vue` — 包容分支节点形状
- `RouteBranchShape.vue` — 路由分支节点形状
- `CallProcessNodeShape.vue` — 子流程节点形状
- `TimerNodeShape.vue` — 定时器节点形状
- `TriggerNodeShape.vue` — 触发器节点形状

在 `src/frontend/Atlas.WebApp/src/components/approval/nodes/` 下新建:

- `InclusiveNodeWidget.vue`
- `RouteNodeWidget.vue`
- `CallProcessNodeWidget.vue`
- `TimerNodeWidget.vue`
- `TriggerNodeWidget.vue`

---

## 阶段二：高级流程操作

### 后端改动

#### 2.1 新增操作处理器

在 `Atlas.Infrastructure/Services/ApprovalFlow/OperationHandlers/` 下新建:

- `JumpTaskHandler.cs` — 跳转到任意审批节点（取消当前任务，在目标节点创建新任务）
- `ReclaimTaskHandler.cs` — 拿回（上一节点提交人在下一节点未处理前收回）
- `ResumeTaskHandler.cs` — 唤醒（指定历史任务/节点重新激活）
- `ClaimTaskHandler.cs` — 认领（公共任务池认领）
- `ReleaseTaskHandler.cs` — 释放认领
- `UrgeTaskHandler.cs` — 催办（触发通知给当前任务处理人）
- `CommunicateHandler.cs` — 沟通（创建沟通记录 + 通知）
- `BatchTransferHandler.cs` — 离职转办（批量转交某人所有任务）
- `AppendAssigneeHandler.cs` — 追加（动态修改未来节点的处理人）

#### 2.2 新增实体

- `ApprovalCommunicationRecord.cs` — 沟通记录表 (`Id`, `TenantId`, `InstanceId`, `TaskId`, `SenderUserId`, `RecipientUserId`, `Content`, `CreatedAt`)
- `ApprovalTimerJob.cs` — 定时器任务表 (`Id`, `TenantId`, `InstanceId`, `NodeId`, `ScheduledAt`, `ExecutedAt`, `Status`)
- `ApprovalTriggerJob.cs` — 触发器任务表 (`Id`, `TenantId`, `InstanceId`, `NodeId`, `TriggerType`, `ScheduledAt`, `ExecutedAt`, `Status`)

#### 2.3 新增后台调度

新建 `Atlas.Infrastructure/Services/ApprovalFlow/Jobs/`:

- `ApprovalTimeoutJob.cs` — 定时扫描超时任务，执行自动通过/拒绝/跳过
- `ApprovalReminderJob.cs` — 定时扫描提醒记录，发送催办通知（支持最大提醒次数）
- `ApprovalTimerNodeJob.cs` — 定时器节点到期自动推进
- `ApprovalTriggerNodeJob.cs` — 触发器节点定时执行

#### 2.4 扩展 API

`ApprovalOperationType` 枚举新增:

- `Jump = 36` (跳转)
- `Reclaim = 37` (拿回)
- `Resume = 38` (唤醒)
- `Claim = 39` (认领)
- `Release = 40` (释放认领)
- `Urge = 41` (催办)
- `Communicate = 42` (沟通)
- `BatchTransfer = 43` (离职转办)
- `Delegate = 44` (委派)
- `Append = 45` (追加处理人)
- `Terminate = 46` (强制终止)

`ApprovalHistoryEventType` 新增:

- `TaskDelegated = 23`
- `TaskDelegateReturned = 24`
- `TaskClaimed = 25`
- `TaskJumped = 26`
- `TaskReclaimed = 27`
- `TaskResumed = 28`
- `InstanceSuspended = 29`
- `InstanceActivated = 30`
- `InstanceTerminated = 31`
- `TaskUrged = 32`
- `TaskCommunicated = 33`

新建控制器端点:

- `POST /api/v1/approval/tasks/{id}/urge` — 催办
- `POST /api/v1/approval/tasks/{id}/communication` — 沟通
- `GET /api/v1/approval/tasks/{id}/communications` — 获取沟通记录
- `GET /api/v1/approval/tasks/pool` — 公共任务池（待认领）
- `POST /api/v1/approval/tasks/batch-transfer` — 离职转办

### 前端改动

#### 2.5 运行时操作面板

在 `ApprovalTasksPage.vue` 或任务详情组件中新增操作按钮:

- 跳转（弹窗选择目标节点）
- 拿回（确认弹窗）
- 催办（弹窗输入催办消息）
- 沟通（弹窗输入沟通内容）
- 委派（弹窗选择被委派人）
- 认领（直接点击认领）

#### 2.6 新增页面/组件

- `ApprovalTaskPoolPage.vue` — 公共任务池页面（待认领任务列表）
- `CommunicationPanel.vue` — 沟通面板组件（消息列表 + 输入框）
- `JumpNodeSelector.vue` — 跳转节点选择器（展示流程图让用户选目标节点）
- `BatchTransferDialog.vue` — 离职转办弹窗

#### 2.7 扩展 API 服务

**文件**: [src/frontend/Atlas.WebApp/src/services/api.ts](src/frontend/Atlas.WebApp/src/services/api.ts)

新增 API 调用方法:

- `delegateTask(taskId, delegateeUserId)`
- `claimTask(taskId)`
- `releaseTask(taskId)`
- `urgeTask(taskId, message)`
- `communicateTask(taskId, recipientUserId, content)`
- `getCommunications(taskId)`
- `jumpTask(instanceId, targetNodeId)`
- `reclaimTask(taskId)`
- `getTaskPool(pagedRequest)`
- `batchTransferTasks(fromUserId, toUserId)`
- `suspendInstance(instanceId)`
- `activateInstance(instanceId)`
- `terminateInstance(instanceId)`
- `saveDraft(request)`
- `submitDraft(instanceId)`

---

## 阶段三：特色功能

### 后端

- 暂存待审完整流程: `SaveDraftAsync` 保存草稿 -> `SubmitDraftAsync` 提交激活
- 穿越时空: `StartAsync` 新增可选参数 `overrideCreateTime`，所有任务创建时间使用该时间
- 已阅标记: `ApprovalTask.MarkViewed()` + API `POST /tasks/{id}/viewed`
- 离职转办: `BatchTransferHandler` 查询某人所有活跃任务并批量转交
- 流程实例挂起/激活: `Suspend()` 暂停所有活动任务，`Activate()` 恢复
- 追加处理人: 运行时动态修改未来节点的 assigneeValue（写入 `ApprovalProcessVariable`）
- 分组策略: `FlowNode.GroupStrategy` 区分认领模式 vs 全员审批模式
- 流程模型缓存: 新建 `ProcessModelCache` 类，使用 `IMemoryCache` 缓存已解析的 `FlowDefinition`

### 前端

- 暂存按钮（设计器页面 + 发起页面）
- 穿越时空日期选择器（发起审批时可选历史日期）
- 已阅标记 UI（任务列表显示已阅/未阅状态）
- 流程挂起/激活按钮（实例详情页）

---

## 阶段四：AI 审批 + 性能优化

### 后端

- 新建 `IApprovalAiHandler` 接口:
  - `HandleAsync(context)` — AI 智能审批
  - `DecideRouteAsync(context)` — AI 路由决策
  - `DecideInclusiveRoutesAsync(context)` — AI 包容分支决策
- `FlowNode` 新增 `CallAi` (bool) + `AiConfig` (JSON) 属性
- `FlowEngine.ProcessNextNodeAsync()` 中，当 `node.CallAi == true` 时，调用 `IApprovalAiHandler`
- 新增 `ApprovalInstanceStatus.AiProcessing = 8` 和 `AiManualReview = 9`
- 异步子流程: `CallProcessNode.CallAsync = true` 时，子流程在后台执行，主流程继续
- 流程模型缓存: `IMemoryCache` + 发布时清除缓存

### 前端

- AI 审批节点配置（开关 + AI 配置面板）
- AI 审批状态展示（"AI处理中"、"AI转人工"标签）

---

## 阶段五：前端运行时页面完善

### 前端

- `ApprovalInstanceDetailPage.vue` — 实例详情页（流程图实时状态 + 审批记录时间线）
- `ApprovalTaskDetailPage.vue` — 任务审批页（表单渲染 + 审批操作按钮）
- 流程图实时状态渲染（已完成=绿色，进行中=蓝色，未执行=灰色）
- 审批记录时间线组件（Timeline）
- 表单渲染器（根据 formPermissionConfig 控制字段读写）
- 流程导出（PNG/SVG）
- 流程导入（JSON 文件）

---

## 关键架构决策

- **后端分支处理统一模式**: 包容分支复用并行网关的 Token 机制，但 fork 阶段只为满足条件的分支创建 Token
- **路由分支实现**: 路由网关不生成任务，直接根据条件计算目标节点并跳转（类似 goto）
- **子流程关联**: 通过 `ApprovalSubProcessLink` 表维护父子关系，子流程结束时回调 `FlowEngine.EndSubProcessAsync()`
- **委派 vs 转办**: 转办后原任务直接结束，新人完成后进入下一步；委派后新人完成还回原人，原人确认后进入下一步
- **认领机制**: 当 `GroupStrategy = Claim` 时，为角色/部门下所有人创建 `Claimed=false` 的任务，认领后其他人的任务取消
- **定时器/触发器**: 通过后台 Job（Hangfire 或 BackgroundService）定时扫描执行