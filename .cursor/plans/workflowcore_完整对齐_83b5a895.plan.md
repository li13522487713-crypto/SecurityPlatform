---
name: WorkflowCore 完整对齐
overview: 通过与开源版 WorkflowCore 的深度对比，识别所有被简化的功能点，并按照功能模块制定详细的对齐任务清单。每个任务可独立闭环完成，确保实现颗粒度与开源版本一致。
todos:
  - id: todo-001-workflow-controller
    content: 重构 WorkflowHost - 提取 IWorkflowController 服务层，委托核心逻辑，添加分布式锁和中间件支持
    status: completed
  - id: todo-002-executor-integration
    content: 完善 WorkflowExecutor - 集成 CancellationProcessor、MiddlewareRunner、ScopeProvider，完善错误处理
    status: completed
  - id: todo-003-result-processor
    content: 完善 ExecutionResultProcessor - 支持 WorkflowExecutorResult、PendingPredecessor、StepCompleted 事件、错误处理器链
    status: completed
  - id: todo-004-pointer-factory-naming
    content: 统一 IExecutionPointerFactory 接口命名 - 对齐到 BuildGenesisPointer/BuildCompensationPointer 等开源命名
    status: completed
  - id: todo-005-host-events
    content: 完善 WorkflowHost - 添加 OnStepError/OnLifeCycleEvent 事件回调、活动API代理、数据库初始化检查
    status: completed
  - id: todo-006-consumer-subscription
    content: 完善 WorkflowConsumer - 实现 TryProcessSubscription、FutureQueue、ScheduledCommand 支持
    status: completed
  - id: todo-007-workflow-options
    content: 补全 WorkflowOptions 配置项 - 添加 MaxConcurrentWorkflows、PollInterval、IdleTime、ErrorRetryInterval 等
    status: completed
  - id: todo-008-step-builder-api
    content: 检查并补全 IStepBuilder API - 对比开源版本，确保高级 Input/Output 映射、Attach、表达式分支等方法完整
    status: completed
  - id: todo-009-primitives-check
    content: 逐个检查内置原语实现 - 对比 Decide、Recur、SagaContainer、SubWorkflowStepBody 等，确保逻辑一致
    status: completed
  - id: todo-010-opentelemetry
    content: 添加 OpenTelemetry 追踪支持 - 实现 WorkflowActivity 类，在关键服务中添加追踪点
    status: completed
  - id: todo-011-dsl-check
    content: 检查 DSL 项目完整性 - 对比 DefinitionLoader、TypeResolver、模型和反序列化器，确保功能完整
    status: completed
isProject: false
---

# WorkflowCore 完整对齐计划

## 对比分析总结

经过对比 `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src` 开源版本，发现当前实现在以下方面存在简化或不完整：

### 关键差距

#### 1. WorkflowExecutor 简化（高优先级）

**开源版本有，我们缺失：**

- `ICancellationProcessor` 调用 - 虽然定义了接口，但未在 Executor 中使用
- `IScopeProvider` 使用 - 缺少作用域管理
- `IWorkflowMiddlewareRunner` 集成 - 缺少中间件调用
- `WorkflowActivity.Enrich()` - 缺少OpenTelemetry追踪
- 完整的 `IExecutionResultProcessor.HandleStepException` 调用

**影响：** 无法正确处理取消逻辑、缺少DI作用域隔离、无法使用中间件扩展、缺少追踪能力

**对比文件：**

- 开源: `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src\WorkflowCore\Services\WorkflowExecutor.cs`
- 当前: [`src/backend/Atlas.WorkflowCore/Services/WorkflowExecutor.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Services\WorkflowExecutor.cs)

#### 2. WorkflowHost 架构简化（高优先级）

**开源版本有，我们缺失：**

- `IWorkflowController` 委托模式 - 开源版将核心逻辑委托给 WorkflowController
- 事件回调机制 - `OnStepError`、`OnLifeCycleEvent` 事件
- `IActivityController` 方法代理 - 直接暴露活动控制器API
- `PersistenceStore.EnsureStoreExists()` - 启动时确保数据库初始化
- `ILifeCycleEventHub.Start()` - 生命周期事件中心启动

**影响：** 架构不够清晰、缺少事件扩展点、活动API不完整

**对比文件：**

- 开源: `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src\WorkflowCore\Services\WorkflowHost.cs`
- 当前: [`src/backend/Atlas.WorkflowCore/Services/WorkflowHost.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Services\WorkflowHost.cs)

#### 3. WorkflowController 缺失（高优先级）

**开源版本有，我们缺失：**

- 独立的 `WorkflowController` 服务层
- `IWorkflowMiddlewareRunner.RunPreMiddleware()` - 启动前中间件
- 分布式锁保护 - `SuspendWorkflow`、`ResumeWorkflow`、`TerminateWorkflow` 需要锁
- `ActivatorUtilities.CreateInstance<TWorkflow>()` - DI注入支持

**影响：** 缺少分层、工作流操作缺少并发保护、无法DI注入工作流实例

**对比文件：**

- 开源: `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src\WorkflowCore\Services\WorkflowController.cs`
- 当前: 缺失，逻辑直接在 WorkflowHost 中

#### 4. ExecutionResultProcessor 不完整（高优先级）

**开源版本有，我们缺失：**

- `WorkflowExecutorResult` 完整使用 - 开源版通过 result 收集订阅和错误
- `PendingPredecessor` 状态处理 - 处理等待前置步骤的指针
- `StepCompleted` 事件发布 - 缺少步骤完成事件
- `IWorkflowErrorHandler` 集成 - 完整的错误处理器链
- `ShouldCompensate()` 判断逻辑 - 自动判断是否需要补偿

**影响：** 订阅管理不完整、缺少前置依赖支持、事件通知缺失、错误处理策略不完整

**对比文件：**

- 开源: `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src\WorkflowCore\Services\ExecutionResultProcessor.cs`
- 当前: [`src/backend/Atlas.WorkflowCore/Services/ExecutionResultProcessor.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Services\ExecutionResultProcessor.cs)

#### 5. WorkflowConsumer 简化（中优先级）

**开源版本有，我们简化了：**

- `WorkflowActivity.Enrich()` - OpenTelemetry追踪
- `TryProcessSubscription()` - 事件订阅处理逻辑
- `FutureQueue()` - 延迟队列处理
- `ScheduledCommand` 支持 - 计划命令调度

**影响：** 缺少追踪、事件订阅处理不完整、延迟执行不完善

**对比文件：**

- 开源: `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src\WorkflowCore\Services\BackgroundTasks\WorkflowConsumer.cs`
- 当前: [`src/backend/Atlas.WorkflowCore/Services/BackgroundTasks/WorkflowConsumer.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Services\BackgroundTasks\WorkflowConsumer.cs)

#### 6. ExecutionPointerFactory 不完整（中优先级）

**接口方法名不一致：**

- 开源: `BuildGenesisPointer`、`BuildCompensationPointer`、`BuildNextPointer`、`BuildChildPointer`
- 当前: `CreateInitialPointer`、`CreateCompensationPointer`、`CreateNextPointer`、`CreateChildPointer`

**影响：** 接口不兼容、命名不一致

**对比文件：**

- 开源: `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src\WorkflowCore\Interface\IExecutionPointerFactory.cs`
- 当前: [`src/backend/Atlas.WorkflowCore/Abstractions/IExecutionPointerFactory.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Abstractions\IExecutionPointerFactory.cs)

#### 7. IStepBuilder API 不完整（中优先级）

**需要检查的方法（开源版本有）：**

- 高级 `Input/Output` 映射 - 支持 `IStepExecutionContext`、`Action` 委托
- `Attach(stepId)` - 附加到指定步骤
- 表达式分支 - `Branch(expression, builder)`
- 所有控制流方法的完整签名

**影响：** Builder API 功能不完整、无法定义复杂映射

#### 8. WorkflowOptions 不完整（低优先级）

**开源版本有，我们可能缺失：**

- `MaxConcurrentWorkflows` - 最大并发数
- `PollInterval` - 轮询间隔
- `IdleTime` - 空闲时间
- `ErrorRetryInterval` - 错误重试间隔
- `EnableIndex` - 是否启用索引

**影响：** 配置选项不完整

#### 9. Primitives 原语细节（低优先级）

**需要检查每个原语是否完整实现：**

- `Decide`、`Recur`、`SagaContainer`、`SubWorkflowStepBody`、`OutcomeSwitch`、`When` 等

**影响：** 原语功能可能不完整

---

## 任务清单（按优先级和闭环能力划分）

### P0 - 核心架构对齐（必须完成）

#### TODO-001: 重构 WorkflowHost - 提取 IWorkflowController

**任务描述：** 创建独立的 `WorkflowController` 服务，将 WorkflowHost 的核心逻辑委托给 Controller。

**实现步骤：**

1. 创建 `WorkflowController` 类实现 `IWorkflowController`
2. 将 `StartWorkflowAsync`、`SuspendWorkflowAsync`、`ResumeWorkflowAsync`、`TerminateWorkflowAsync`、`PublishEventAsync` 逻辑移至 Controller
3. 更新 WorkflowHost，委托调用 Controller 方法
4. 在 Controller 中添加分布式锁保护（Suspend/Resume/Terminate）
5. 在 Controller 中添加中间件调用（`RunPreMiddleware`）
6. 注册 `IWorkflowController` 到 DI

**验收标准：**

- WorkflowHost 只负责生命周期管理，业务逻辑在 Controller
- Suspend/Resume/Terminate 使用分布式锁
- StartWorkflow 调用 PreWorkflow 中间件
- 所有测试通过

**影响文件：**

- 新增: `src/backend/Atlas.WorkflowCore/Services/WorkflowController.cs`
- 修改: `src/backend/Atlas.WorkflowCore/Services/WorkflowHost.cs`
- 修改: `src/backend/Atlas.WorkflowCore/ServiceCollectionExtensions.cs`

---

#### TODO-002: 完善 WorkflowExecutor - 集成取消处理器和中间件

**任务描述：** 在 WorkflowExecutor 中集成 CancellationProcessor、MiddlewareRunner、ScopeProvider。

**实现步骤：**

1. 更新构造函数注入 `ICancellationProcessor`、`IWorkflowMiddlewareRunner`、`IScopeProvider`
2. 在 `Execute` 方法开始调用 `_cancellationProcessor.ProcessCancellations()`
3. 在执行每个指针后再次调用 `ProcessCancellations()`
4. 集成中间件运行器（PreWorkflow、ExecuteWorkflow、PostWorkflow）
5. 使用 `IScopeProvider` 为每个步骤创建独立DI作用域
6. 确保 `HandleStepException` 正确调用 `_executionResultProcessor.HandleStepException()`

**验收标准：**

- 取消条件能正确触发补偿逻辑
- 中间件按正确顺序执行
- 每个步骤有独立的DI作用域
- 异常处理调用完整的错误处理器链

**影响文件：**

- 修改: `src/backend/Atlas.WorkflowCore/Services/WorkflowExecutor.cs`

---

#### TODO-003: 完善 ExecutionResultProcessor - 对齐订阅和事件处理

**任务描述：** 完善 ExecutionResultProcessor，支持 WorkflowExecutorResult 收集、PendingPredecessor 处理、事件发布。

**实现步骤：**

1. 修改 `ProcessExecutionResult` 签名，接受 `WorkflowExecutorResult` 参数
2. 在处理事件订阅时，将订阅添加到 `workflowResult.Subscriptions`
3. 处理 `PendingPredecessor` 状态 - 查找等待当前指针的后续步骤，激活它们
4. 步骤完成时发布 `StepCompleted` 事件
5. 在 `HandleStepException` 中实现完整的错误处理器链调用
6. 实现 `ShouldCompensate()` 方法 - 遍历指针作用域判断是否需要补偿

**验收标准：**

- 事件订阅正确添加到 result.Subscriptions
- PendingPredecessor 指针能正确激活
- StepCompleted 事件正确发布
- 错误处理器按策略（Retry/Suspend/Terminate/Compensate）执行
- 补偿逻辑自动判断并触发

**影响文件：**

- 修改: `src/backend/Atlas.WorkflowCore/Services/ExecutionResultProcessor.cs`
- 修改: `src/backend/Atlas.WorkflowCore/Abstractions/IExecutionResultProcessor.cs`

---

#### TODO-004: 统一 IExecutionPointerFactory 接口命名

**任务描述：** 将方法名对齐到开源版本命名规范。

**实现步骤：**

1. 重命名 `CreateInitialPointer` → `BuildGenesisPointer`
2. 重命名 `CreateCompensationPointer` → `BuildCompensationPointer`
3. 重命名 `CreateNextPointer` → `BuildNextPointer`
4. 重命名 `CreateChildPointer` → `BuildChildPointer`
5. 更新所有调用方

**验收标准：**

- 接口方法名与开源版本一致
- 所有引用已更新
- 编译通过，无警告

**影响文件：**

- 修改: `src/backend/Atlas.WorkflowCore/Abstractions/IExecutionPointerFactory.cs`
- 修改: `src/backend/Atlas.WorkflowCore/Services/ExecutionPointerFactory.cs`
- 修改: 所有调用方（ExecutionResultProcessor、WorkflowController等）

---

### P1 - 核心功能完善

#### TODO-005: 完善 WorkflowHost - 添加事件回调和活动API

**任务描述：** 在 WorkflowHost 中添加事件回调机制和活动控制器API代理。

**实现步骤：**

1. 添加 `event StepErrorEventHandler OnStepError`
2. 添加 `event LifeCycleEventHandler OnLifeCycleEvent`
3. 实现 `ReportStepError(workflow, step, exception)` 方法触发事件
4. 实现 `HandleLifeCycleEvent(evt)` 方法触发事件
5. 代理 `IActivityController` 的所有方法到 WorkflowHost
6. 在 `StartAsync` 中调用 `_lifeCycleEventHub.Start()`
7. 在 `StartAsync` 中调用 `_persistenceProvider.EnsureStoreExists()`
8. 订阅生命周期事件中心

**验收标准：**

- OnStepError、OnLifeCycleEvent 事件可订阅和触发
- 活动API可通过 WorkflowHost 调用
- 生命周期事件中心正确启动
- 数据库初始化检查

**影响文件：**

- 修改: `src/backend/Atlas.WorkflowCore/Services/WorkflowHost.cs`
- 修改: `src/backend/Atlas.WorkflowCore/Abstractions/IWorkflowHost.cs`

---

#### TODO-006: 完善 WorkflowConsumer - 添加订阅处理和延迟队列

**任务描述：** 在 WorkflowConsumer 中实现完整的事件订阅处理和延迟队列逻辑。

**实现步骤：**

1. 实现 `TryProcessSubscription()` 方法 - 处理步骤完成后的事件订阅
2. 实现 `FutureQueue()` 方法 - 处理延迟执行逻辑
3. 在 `ProcessItem` 中调用订阅处理
4. 在 `ProcessItem` 中调用延迟队列处理
5. 支持 `ScheduledCommand` - 如果 NextExecution 超出轮询间隔，使用计划命令

**验收标准：**

- 步骤完成后自动处理匹配的事件订阅
- 延迟执行的工作流在正确时间重新入队
- 计划命令正确创建和调度

**影响文件：**

- 修改: `src/backend/Atlas.WorkflowCore/Services/BackgroundTasks/WorkflowConsumer.cs`

---

#### TODO-007: 补全 WorkflowOptions 配置项

**任务描述：** 添加所有开源版本支持的配置选项。

**实现步骤：**

1. 检查开源版 `WorkflowOptions` 类
2. 添加缺失的配置项：`MaxConcurrentWorkflows`、`PollInterval`、`IdleTime`、`ErrorRetryInterval`、`EnableIndex` 等
3. 在各服务中使用这些配置
4. 更新 `appsettings.json` 示例

**验收标准：**

- 所有配置项与开源版本对齐
- 配置正确应用到各服务
- 文档更新

**影响文件：**

- 修改: `src/backend/Atlas.WorkflowCore/Models/WorkflowOptions.cs`
- 修改: 各使用配置的服务

---

### P2 - API 和原语细节对齐

#### TODO-008: 检查并补全 IStepBuilder API

**任务描述：** 逐一对比开源版本 IStepBuilder 接口，确保所有方法签名和功能完整。

**实现步骤：**

1. 对比开源版 `IStepBuilder` 接口（需要从开源代码中找到完整定义）
2. 检查高级 `Input/Output` 映射是否支持所有重载
3. 确保 `Attach(stepId)` 方法存在
4. 确保表达式分支 `Branch(expression, builder)` 存在
5. 检查所有控制流方法（WaitFor、Delay、Decide、ForEach、While、If、When、Parallel、Saga、Schedule、Recur、Activity）的签名和参数

**验收标准：**

- IStepBuilder 接口与开源版本完全一致
- 所有方法实现完整
- 示例代码可正常使用

**影响文件：**

- 检查: `src/backend/Atlas.WorkflowCore/Abstractions/IStepBuilder.cs`
- 修改: `src/backend/Atlas.WorkflowCore/Builders/StepBuilder.cs`

---

#### TODO-009: 逐个检查内置原语实现

**任务描述：** 对比每个内置原语的实现细节，确保与开源版本一致。

**实现步骤：**

1. 对比 `Decide.cs` - 确保表达式处理正确
2. 对比 `Recur.cs` - 确保循环调度逻辑完整
3. 对比 `SagaContainer.cs` - 确保补偿逻辑正确
4. 对比 `SubWorkflowStepBody.cs` - 确保子工作流启动和数据传递正确
5. 对比 `OutcomeSwitch.cs` 和 `When.cs` - 确保分支逻辑正确
6. 对比 `Activity.cs` - 确保活动令牌管理完整
7. 对比其他所有原语

**验收标准：**

- 每个原语逻辑与开源版本一致
- 所有属性和方法完整
- 单元测试覆盖

**影响文件：**

- 检查并修改: `src/backend/Atlas.WorkflowCore/Primitives/` 下所有文件

---

### P3 - 追踪和可观测性

#### TODO-010: 添加 OpenTelemetry 追踪支持

**任务描述：** 集成 OpenTelemetry，添加 WorkflowActivity 追踪。

**实现步骤：**

1. 添加 `OpenTelemetry` NuGet 包
2. 创建 `WorkflowActivity` 静态类
3. 实现 `StartHost()`、`Enrich(workflow)`、`Enrich(step)`、`Enrich(result)` 方法
4. 在 WorkflowHost、WorkflowExecutor、WorkflowConsumer 中调用追踪方法
5. 配置 OpenTelemetry 导出器

**验收标准：**

- 工作流执行生成追踪数据
- 可在追踪后端查看工作流执行链路

**影响文件：**

- 新增: `src/backend/Atlas.WorkflowCore/Services/WorkflowActivity.cs`
- 修改: WorkflowHost、WorkflowExecutor、WorkflowConsumer
- 修改: `src/backend/Atlas.WorkflowCore/Atlas.WorkflowCore.csproj`

---

### P4 - DSL 细节对齐

#### TODO-011: 检查 DSL 项目完整性

**任务描述：** 对比开源版 WorkflowCore.DSL 项目，确保所有功能完整。

**实现步骤：**

1. 对比 `IDefinitionLoader` 接口
2. 对比 `DefinitionLoader` 实现 - 表达式解析、类型解析、映射转换
3. 对比 `ITypeResolver` 和 `TypeResolver`
4. 对比 DSL 模型 - `DefinitionSourceV1`、`StepSourceV1`、`MappingSourceV1`
5. 对比反序列化器 - JSON、YAML
6. 确保支持 `System.Linq.Dynamic.Core` 表达式

**验收标准：**

- DSL 功能与开源版本一致
- 可加载 JSON/YAML 工作流定义
- 表达式解析正确

**影响文件：**

- 检查并修改: `src/backend/Atlas.WorkflowCore.DSL/` 下所有文件

---

## 实施建议

1. **按优先级顺序执行** - P0 → P1 → P2 → P3 → P4
2. **每个TODO独立分支** - 便于代码审查和测试
3. **完成一个TODO创建一个提交** - 保持提交历史清晰
4. **每个TODO附带单元测试** - 确保功能正确性
5. **P0任务完成后进行集成测试** - 确保核心架构稳定
6. **参考开源版本代码** - 遇到不确定的实现细节直接查看源码

## 预估工作量

- **P0 任务：** 约 16-20 小时（4个TODO，核心架构）
- **P1 任务：** 约 12-16 小时（3个TODO，功能完善）
- **P2 任务：** 约 8-12 小时（2个TODO，API对齐）
- **P3 任务：** 约 4-6 小时（1个TODO，可观测性）
- **P4 任务：** 约 4-6 小时（1个TODO，DSL检查）

**总计：** 约 44-60 小时（9-12 个工作日）

---

## 关键对比文件清单

| 功能模块 | 开源版本文件 | 当前实现文件 | 差异程度 |
|---------|------------|------------|---------|
| WorkflowExecutor | `WorkflowCore/Services/WorkflowExecutor.cs` | [`Atlas.WorkflowCore/Services/WorkflowExecutor.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Services\WorkflowExecutor.cs) | 中度简化 |
| WorkflowHost | `WorkflowCore/Services/WorkflowHost.cs` | [`Atlas.WorkflowCore/Services/WorkflowHost.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Services\WorkflowHost.cs) | 中度简化 |
| WorkflowController | `WorkflowCore/Services/WorkflowController.cs` | **缺失** | 完全缺失 |
| ExecutionResultProcessor | `WorkflowCore/Services/ExecutionResultProcessor.cs` | [`Atlas.WorkflowCore/Services/ExecutionResultProcessor.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Services\ExecutionResultProcessor.cs) | 中度简化 |
| WorkflowConsumer | `WorkflowCore/Services/BackgroundTasks/WorkflowConsumer.cs` | [`Atlas.WorkflowCore/Services/BackgroundTasks/WorkflowConsumer.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Services\BackgroundTasks\WorkflowConsumer.cs) | 轻度简化 |
| ExecutionPointerFactory | `WorkflowCore/Interface/IExecutionPointerFactory.cs` | [`Atlas.WorkflowCore/Abstractions/IExecutionPointerFactory.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Abstractions\IExecutionPointerFactory.cs) | 命名不一致 |
| IStepBuilder | `WorkflowCore/Services/FluentBuilders/IStepBuilder.cs` | [`Atlas.WorkflowCore/Abstractions/IStepBuilder.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Abstractions\IStepBuilder.cs) | 需检查 |
| Primitives | `WorkflowCore/Primitives/*.cs` | [`Atlas.WorkflowCore/Primitives/*.cs`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore\Primitives) | 需逐个对比 |
| DSL | `WorkflowCore.DSL/**/*` | [`Atlas.WorkflowCore.DSL/**/*`](d:\Code\SecurityPlatform\src\backend\Atlas.WorkflowCore.DSL) | 需检查 |

---

## 验收标准

完成所有TODO后，系统应满足：

1. **架构一致性** - 核心服务分层与开源版本对齐
2. **功能完整性** - 所有开源版本的功能都已实现
3. **接口兼容性** - 接口命名和签名与开源版本一致
4. **测试覆盖率** - 核心功能单元测试覆盖率 > 80%
5. **文档完整性** - 所有差异点和对齐工作已记录

## 开源版本源码路径

**基础路径：** `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src`

**关键目录：**

- 核心: `WorkflowCore/`
- DSL: `WorkflowCore.DSL/`
- 接口: `WorkflowCore/Interface/`
- 服务: `WorkflowCore/Services/`
- 原语: `WorkflowCore/Primitives/`
- 后台任务: `WorkflowCore/Services/BackgroundTasks/`