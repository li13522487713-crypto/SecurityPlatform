---
name: WorkflowCore 完整实现
overview: 对齐开源版本 WorkflowCore，实现完整的工作流引擎能力，包括队列系统、分布式锁、后台任务、中间件、DSL支持、搜索索引等核心功能
todos:
  - id: queue-system
    content: 实现队列系统 - IQueueProvider接口、SingleNodeQueueProvider实现、QueueType枚举
    status: completed
  - id: distributed-lock
    content: 实现分布式锁 - IDistributedLockProvider接口、SingleNodeLockProvider实现
    status: completed
  - id: background-tasks-infra
    content: 实现后台任务基础设施 - IBackgroundTask接口、QueueConsumer抽象基类
    status: completed
  - id: workflow-consumer
    content: 实现工作流消费者 - WorkflowConsumer（从队列消费、获取锁、执行、释放锁、重新入队）
    status: completed
  - id: event-consumer
    content: 实现事件消费者 - EventConsumer（事件出队、获取订阅、唤醒工作流、标记已处理）
    status: completed
  - id: index-consumer
    content: 实现索引消费者 - IndexConsumer（调用ISearchIndex索引工作流）
    status: completed
  - id: runnable-poller
    content: 实现可运行实例轮询器 - RunnablePoller（轮询实例、事件、命令并入队）
    status: completed
  - id: execution-result-processor
    content: 实现执行结果处理器 - IExecutionResultProcessor、ExecutionResultProcessor（处理Proceed、OutcomeValue、SleepFor、分支、事件订阅）
    status: completed
  - id: execution-pointer-factory
    content: 实现执行指针工厂 - IExecutionPointerFactory、ExecutionPointerFactory（创建初始、补偿、子工作流、下一个指针）
    status: completed
  - id: cancellation-processor
    content: 实现取消处理器 - ICancellationProcessor、CancellationProcessor（处理取消条件和补偿逻辑）
    status: completed
  - id: activity-controller
    content: 实现活动控制器 - IActivityController、ActivityController、ActivityResult、WorkflowActivity（管理外部活动令牌、提交结果）
    status: completed
  - id: auxiliary-services
    content: 实现辅助服务 - IScopeProvider、IGreyList、IDateTimeProvider及其实现
    status: completed
  - id: error-handlers
    content: 实现错误处理器 - IWorkflowErrorHandler接口、RetryHandler、SuspendHandler、TerminateHandler、CompensateHandler
    status: completed
  - id: middleware-interfaces
    content: 定义中间件接口 - IWorkflowMiddleware、IWorkflowStepMiddleware、IWorkflowMiddlewareRunner、IWorkflowMiddlewareErrorHandler
    status: completed
  - id: middleware-runner
    content: 实现中间件运行器 - WorkflowMiddlewareRunner、DefaultWorkflowMiddlewareErrorHandler
    status: completed
  - id: builder-api-enhancement-core
    content: 增强IWorkflowBuilder - UseDefaultErrorBehavior、CreateBranch、AttachBranch方法
    status: completed
  - id: builder-api-enhancement-step
    content: 增强IStepBuilder - Name、Id、Attach、高级Input/Output映射、OnError、CompensateWith、CancelCondition、Branch方法
    status: completed
  - id: builder-api-control-flow
    content: 补全IStepBuilder控制流方法 - WaitFor、Delay、Decide、ForEach、While、If、When、Parallel、Saga、Schedule、Recur、Activity
    status: completed
  - id: container-step-builder
    content: 实现容器步骤构建器 - IContainerStepBuilder、ContainerStepBuilder
    status: completed
  - id: primitives-decision
    content: 实现决策和分支原语 - Decide、OutcomeSwitch、When
    status: completed
  - id: primitives-scheduling
    content: 实现调度原语 - Recur（循环调度）
    status: completed
  - id: primitives-saga
    content: 实现Saga原语 - SagaContainer
    status: completed
  - id: primitives-subworkflow
    content: 实现子工作流原语 - SubWorkflowStepBody
    status: completed
  - id: primitives-action
    content: 实现操作步骤原语 - ActionStepBody
    status: completed
  - id: primitives-activity-enhance
    content: 增强Activity原语 - 支持effectiveDate、cancelCondition、token管理
    status: completed
  - id: scheduled-commands
    content: 实现计划命令支持 - IScheduledCommandRepository、ScheduledCommand、SchedulePersistenceData、更新IPersistenceProvider
    status: completed
  - id: search-index
    content: 实现搜索索引 - ISearchIndex、NullSearchIndex、搜索模型（WorkflowSearchResult、SearchFilter、StepInfo、Page）
    status: completed
  - id: model-collections
    content: 实现模型集合 - ExecutionPointerCollection、WorkflowStepCollection
    status: completed
  - id: model-options
    content: 实现工作流选项 - WorkflowOptions、WorkflowExecutorResult
    status: completed
  - id: model-expression
    content: 实现表达式结果 - ExpressionOutcome
    status: completed
  - id: exceptions
    content: 定义自定义异常 - ActivityFailedException、CorruptPersistenceDataException、WorkflowNotRegisteredException、WorkflowDefinitionLoadException、WorkflowLockedException、NotFoundException
    status: completed
  - id: dsl-project
    content: 创建DSL项目 - Atlas.WorkflowCore.DSL.csproj、添加依赖（Newtonsoft.Json、YamlDotNet、System.Linq.Dynamic.Core）
    status: completed
  - id: dsl-models
    content: 实现DSL模型 - DefinitionSource、Envelope、DefinitionSourceV1、StepSourceV1、MappingSourceV1
    status: pending
  - id: dsl-interfaces
    content: 定义DSL接口 - IDefinitionLoader、ITypeResolver
    status: pending
  - id: dsl-loader
    content: 实现定义加载器 - DefinitionLoader（表达式解析、类型解析、输入输出映射转换、构建WorkflowDefinition）
    status: pending
  - id: dsl-resolver
    content: 实现类型解析器 - TypeResolver
    status: pending
  - id: dsl-deserializers
    content: 实现反序列化器 - Deserializers（Json、Yaml）
    status: pending
  - id: dsl-di
    content: 实现DSL的DI扩展 - ServiceCollectionExtensions
    status: pending
  - id: workflow-host-integration
    content: 集成WorkflowHost - 更新构造函数、实现StartAsync/StopAsync、启动后台任务、更新StartWorkflowAsync和PublishEventAsync
    status: completed
  - id: workflow-executor-integration
    content: 集成WorkflowExecutor - 更新构造函数、集成中间件、集成执行结果处理器、集成取消处理器
    status: completed
  - id: di-registration
    content: 更新DI注册 - ServiceCollectionExtensions（注册所有新服务、选项配置）
    status: completed
  - id: sync-workflow-runner
    content: 实现同步运行器 - ISyncWorkflowRunner、SyncWorkflowRunner（同步等待工作流完成）
    status: completed
  - id: unit-tests
    content: 创建单元测试 - Atlas.WorkflowCore.Tests项目、测试ExecutionPointerFactory、ExecutionResultProcessor、CancellationProcessor、ErrorHandlers、Primitives
    status: pending
  - id: integration-tests
    content: 创建集成测试 - 完整工作流执行、事件订阅发布、错误处理重试、Saga补偿、DSL加载执行
    status: pending
  - id: samples
    content: 创建示例工作流 - Sample01-15对应开源版本示例
    status: pending
isProject: false
---

# WorkflowCore 完整能力实现计划

## 差距分析总结

通过对比开源版本 `C:\Users\kuo13\Downloads\workflow-core-master\workflow-core-master\src`，当前实现缺失以下关键功能：

### 一、基础设施层（关键，必须实现）

**缺失功能：**

1. **IQueueProvider** - 分布式队列（3种队列：Workflow、Event、Index）
2. **IDistributedLockProvider** - 分布式锁（防止并发执行）
3. **ISearchIndex** - 搜索索引接口
4. **IScheduledCommandRepository** - 计划命令存储

**影响：** 无法支持多节点部署、无法防止并发冲突、无法延迟执行

### 二、后台任务系统（关键，必须实现）

**缺失组件：**

1. **WorkflowConsumer** - 工作流消费者（从队列消费并执行）
2. **EventConsumer** - 事件消费者（处理事件并唤醒工作流）
3. **IndexConsumer** - 索引消费者（更新搜索索引）
4. **RunnablePoller** - 可运行实例轮询器（定时轮询待执行工作流）
5. **IBackgroundTask** - 后台任务接口

**影响：** WorkflowHost 只能手动触发，无法自动轮询和执行

### 三、核心服务缺失（必须实现）

**缺失服务：**

1. **IExecutionResultProcessor** - 执行结果处理器（处理步骤结果、创建后续指针）
2. **IExecutionPointerFactory** - 执行指针工厂（创建初始、补偿、子工作流指针）
3. **ICancellationProcessor** - 取消处理器（处理工作流取消逻辑）
4. **IActivityController** - 活动控制器（管理外部活动）
5. **ISyncWorkflowRunner** - 同步运行器（同步等待工作流完成）
6. **IScopeProvider** - 服务作用域提供者（为步骤创建DI作用域）
7. **IGreyList** - 灰名单管理（防止重复处理）
8. **IDateTimeProvider** - 时间提供者（便于测试）

**影响：** 执行逻辑不完整，缺少关键协调组件

### 四、中间件系统（增强扩展性）

**缺失接口：**

1. **IWorkflowMiddleware** - 工作流中间件（Pre/Execute/Post）
2. **IWorkflowStepMiddleware** - 步骤中间件
3. **IWorkflowMiddlewareRunner** - 中间件运行器
4. **IWorkflowMiddlewareErrorHandler** - 中间件错误处理器

**影响：** 无法在执行前后插入自定义逻辑（日志、验证、性能监控等）

### 五、错误处理器（完整补偿机制）

**缺失处理器：**

1. **RetryHandler** - 重试处理器
2. **SuspendHandler** - 暂停处理器
3. **TerminateHandler** - 终止处理器
4. **CompensateHandler** - 补偿处理器
5. **IWorkflowErrorHandler** - 错误处理器接口

**当前状态：** 只有基础错误记录，无自动重试和补偿

### 六、Builder API 增强（必须补全）

**缺失方法：**

1. **CompensateWith()** - 定义补偿步骤
2. **CompensateWithSequence()** - 补偿序列
3. **CancelCondition()** - 取消条件
4. **Branch()** - 表达式分支
5. **Saga()** - Saga事务容器
6. **OnError()** - 错误处理策略
7. **UseDefaultErrorBehavior()** - 默认错误行为
8. **Attach()** - 附加到指定步骤
9. **高级输入输出映射** - 支持 IStepExecutionContext、Action、自动类型映射

**影响：** Fluent API 不完整，无法定义复杂工作流

### 七、内置原语缺失（必须补全）

**缺失原语：**

1. **Decide** - 决策分支（多分支选择）
2. **Recur** - 循环调度（按间隔循环执行）
3. **OutcomeSwitch** - 结果开关
4. **When** - 结果分支容器
5. **SagaContainer** - Saga事务容器
6. **SubWorkflowStepBody** - 子工作流步骤
7. **ActionStepBody** - 操作步骤（简化步骤定义）

**当前状态：** 只有 Delay、If、While、Foreach、Sequence、WaitFor、Activity

### 八、DSL 支持（WorkflowCore.DSL 项目）

**完全缺失：**

1. **Atlas.WorkflowCore.DSL** 项目
2. **IDefinitionLoader** - 定义加载器
3. **ITypeResolver** - 类型解析器
4. **JSON/YAML 反序列化支持**
5. **表达式解析（System.Linq.Dynamic.Core）**
6. **DefinitionSourceV1、StepSourceV1 模型**

**影响：** 无法通过配置文件定义工作流，只能硬编码

### 九、模型和集合增强

**缺失模型：**

1. **ExecutionPointerCollection** - 执行指针集合（带查找方法）
2. **WorkflowStepCollection** - 步骤集合
3. **WorkflowOptions** - 工作流配置选项
4. **WorkflowExecutorResult** - 执行器结果（包含订阅和错误）
5. **ActivityResult** - 活动结果
6. **ScheduledCommand** - 计划命令
7. **SchedulePersistenceData** - 调度持久化数据
8. **搜索模型** - WorkflowSearchResult、SearchFilter、StepInfo、Page&lt;T&gt;

### 十、异常类型

**缺失异常：**

1. **ActivityFailedException** - 活动失败异常
2. **CorruptPersistenceDataException** - 持久化数据损坏异常
3. **WorkflowNotRegisteredException** - 工作流未注册异常
4. **WorkflowDefinitionLoadException** - 定义加载异常
5. **WorkflowLockedException** - 工作流锁定异常
6. **NotFoundException** - 未找到异常

---

## 完整实现路线图

### 阶段一：核心基础设施（优先级：最高）

#### 任务 1.1：队列系统实现

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IQueueProvider.cs`
  - `Atlas.WorkflowCore/Models/QueueType.cs` (枚举：Workflow, Event, Index)
  - `Atlas.WorkflowCore/Services/DefaultProviders/SingleNodeQueueProvider.cs`
- **方法：** `QueueWork(id, queue)`、`DequeueWork(queue, token)`、`Start()`、`Stop()`、`IsDequeueBlocking`

#### 任务 1.2：分布式锁实现

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IDistributedLockProvider.cs`
  - `Atlas.WorkflowCore/Services/DefaultProviders/SingleNodeLockProvider.cs`
- **方法：** `AcquireLock(id, token)`、`ReleaseLock(id)`、`Start()`、`Stop()`

#### 任务 1.3：计划命令支持

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/Persistence/IScheduledCommandRepository.cs`
  - `Atlas.WorkflowCore/Models/ScheduledCommand.cs`
  - `Atlas.WorkflowCore/Models/SchedulePersistenceData.cs`
- **更新：** `IPersistenceProvider` 继承 `IScheduledCommandRepository`

#### 任务 1.4：搜索索引接口

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/ISearchIndex.cs`
  - `Atlas.WorkflowCore/Models/Search/WorkflowSearchResult.cs`
  - `Atlas.WorkflowCore/Models/Search/SearchFilter.cs`
  - `Atlas.WorkflowCore/Models/Search/StepInfo.cs`
  - `Atlas.WorkflowCore/Models/Search/Page.cs`
  - `Atlas.WorkflowCore/Services/DefaultProviders/NullSearchIndex.cs`
- **方法：** `IndexWorkflow(instance)`、`Search(filter, skip, take)`

### 阶段二：后台任务系统（优先级：最高）

#### 任务 2.1：后台任务基础设施

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IBackgroundTask.cs`
  - `Atlas.WorkflowCore/Services/BackgroundTasks/QueueConsumer.cs` (抽象基类)

#### 任务 2.2：工作流消费者

- **文件：** `Atlas.WorkflowCore/Services/BackgroundTasks/WorkflowConsumer.cs`
- **职责：**
  - 从 Workflow 队列出队工作流ID
  - 获取分布式锁
  - 执行工作流
  - 释放锁
  - 重新入队（如果未完成）

#### 任务 2.3：事件消费者

- **文件：** `Atlas.WorkflowCore/Services/BackgroundTasks/EventConsumer.cs`
- **职责：**
  - 从 Event 队列出队事件ID
  - 获取事件订阅
  - 唤醒等待的工作流
  - 标记事件已处理

#### 任务 2.4：索引消费者

- **文件：** `Atlas.WorkflowCore/Services/BackgroundTasks/IndexConsumer.cs`
- **职责：**
  - 从 Index 队列出队工作流ID
  - 调用 ISearchIndex.IndexWorkflow()

#### 任务 2.5：可运行实例轮询器

- **文件：** `Atlas.WorkflowCore/Services/BackgroundTasks/RunnablePoller.cs`
- **职责：**
  - 定时调用 `GetRunnableInstances()`
  - 定时调用 `GetRunnableEvents()`
  - 定时调用 `ProcessCommands()`
  - 将结果入队

### 阶段三：核心服务补全（优先级：高）

#### 任务 3.1：执行结果处理器

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IExecutionResultProcessor.cs`
  - `Atlas.WorkflowCore/Services/ExecutionResultProcessor.cs`
- **职责：**
  - 处理 ExecutionResult（Proceed、OutcomeValue、SleepFor）
  - 创建后续执行指针
  - 处理分支（BranchValues）
  - 处理事件订阅（EventName/EventKey）
  - 处理步骤异常

#### 任务 3.2：执行指针工厂

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IExecutionPointerFactory.cs`
  - `Atlas.WorkflowCore/Services/ExecutionPointerFactory.cs`
- **方法：**
  - `CreateInitialPointer(step)`
  - `CreateCompensationPointer(step)`
  - `CreateChildPointer(parentPointer, step, scope)`
  - `CreateNextPointer(currentPointer, step)`

#### 任务 3.3：取消处理器

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/ICancellationProcessor.cs`
  - `Atlas.WorkflowCore/Services/CancellationProcessor.cs`
- **职责：** 处理取消条件（CancelCondition）、取消补偿逻辑

#### 任务 3.4：活动控制器

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IActivityController.cs`
  - `Atlas.WorkflowCore/Services/ActivityController.cs`
  - `Atlas.WorkflowCore/Models/ActivityResult.cs`
  - `Atlas.WorkflowCore/Services/WorkflowActivity.cs`
- **方法：**
  - `GetPendingActivities()`
  - `ReleaseActivityToken(token, workerId)`
  - `SubmitActivitySuccess(token, data)`
  - `SubmitActivityFailure(token, message)`

#### 任务 3.5：同步运行器

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/ISyncWorkflowRunner.cs`
  - `Atlas.WorkflowCore/Services/SyncWorkflowRunner.cs`
- **方法：** `RunWorkflow<TData>(workflow, data, timeout, token)`

#### 任务 3.6：辅助服务

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IScopeProvider.cs` → `Services/ScopeProvider.cs`
  - `Atlas.WorkflowCore/Abstractions/IGreyList.cs` → `Services/GreyList.cs`
  - `Atlas.WorkflowCore/Abstractions/IDateTimeProvider.cs` → `Services/DateTimeProvider.cs`

### 阶段四：错误处理系统（优先级：高）

#### 任务 4.1：错误处理器接口和枚举

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IWorkflowErrorHandler.cs`
  - `Atlas.WorkflowCore/Models/WorkflowErrorHandling.cs` (枚举：Retry, Suspend, Terminate, Compensate)

#### 任务 4.2：错误处理器实现

- **文件：**
  - `Atlas.WorkflowCore/Services/ErrorHandlers/RetryHandler.cs`
  - `Atlas.WorkflowCore/Services/ErrorHandlers/SuspendHandler.cs`
  - `Atlas.WorkflowCore/Services/ErrorHandlers/TerminateHandler.cs`
  - `Atlas.WorkflowCore/Services/ErrorHandlers/CompensateHandler.cs`

#### 任务 4.3：集成错误处理器到执行器

- **更新：** `WorkflowExecutor`、`StepExecutor` 使用错误处理器

### 阶段五：中间件系统（优先级：中）

#### 任务 5.1：中间件接口定义

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IWorkflowMiddleware.cs`
  - `Atlas.WorkflowCore/Abstractions/IWorkflowStepMiddleware.cs`
  - `Atlas.WorkflowCore/Abstractions/IWorkflowMiddlewareRunner.cs`
  - `Atlas.WorkflowCore/Abstractions/IWorkflowMiddlewareErrorHandler.cs`
  - `Atlas.WorkflowCore/Models/WorkflowMiddlewarePhase.cs` (枚举：PreWorkflow, ExecuteWorkflow, PostWorkflow)

#### 任务 5.2：中间件运行器实现

- **文件：**
  - `Atlas.WorkflowCore/Services/WorkflowMiddlewareRunner.cs`
  - `Atlas.WorkflowCore/Services/DefaultWorkflowMiddlewareErrorHandler.cs`

#### 任务 5.3：集成中间件到执行器

- **更新：** `WorkflowExecutor`、`StepExecutor` 调用中间件

### 阶段六：Builder API 增强（优先级：高）

#### 任务 6.1：补全 IWorkflowBuilder 方法

- **更新：** `Atlas.WorkflowCore/Abstractions/IWorkflowBuilder.cs`
- **新增方法：**
  - `UseDefaultErrorBehavior(behavior, retryInterval)`
  - `CreateBranch()`
  - `AttachBranch(branch)`

#### 任务 6.2：补全 IStepBuilder 方法

- **更新：** `Atlas.WorkflowCore/Abstractions/IStepBuilder.cs`、`Builders/StepBuilder.cs`
- **新增方法：**
  - `Name(name)`、`Id(id)`、`Attach(id)`
  - `Input<TInput>(stepProperty, value)` - 支持表达式映射
  - `Input<TInput>(stepProperty, valueWithContext)` - 支持上下文
  - `Input(action)` - 自定义输入设置
  - `Output<TOutput>(dataProperty, stepProperty)` - 输出映射
  - `Output(action)` - 自定义输出
  - `OnError(behavior, retryInterval)`
  - `CompensateWith<TStep>()`、`CompensateWith(body)`、`CompensateWithSequence(builder)`
  - `CancelCondition(condition, proceedAfterCancel)`
  - `Branch<TStep>(outcomeValue, branch)`
  - `Branch<TStep>(outcomeExpression, branch)`
  - **高级控制流方法（从 IWorkflowModifier 移到 IStepBuilder）：**
    - `WaitFor(eventName, eventKey, effectiveDate, cancelCondition)`
    - `Delay(period)`
    - `Decide(expression)`
    - `ForEach(collection)` / `ForEach(collection, runParallel)`
    - `While(condition)`
    - `If(condition)`
    - `When(outcomeValue, label)`
    - `Parallel()`
    - `Saga(builder)`
    - `Schedule(time)`
    - `Recur(interval, until)`
    - `Activity(activityName, parameters, effectiveDate, cancelCondition)`

#### 任务 6.3：容器步骤构建器

- **文件：**
  - `Atlas.WorkflowCore/Abstractions/IContainerStepBuilder.cs`
  - `Atlas.WorkflowCore/Builders/ContainerStepBuilder.cs`
- **方法：** `Do(builder)`

### 阶段七：内置原语补全（优先级：中）

#### 任务 7.1：决策和分支原语

- **文件：**
  - `Atlas.WorkflowCore/Primitives/Decide.cs`
  - `Atlas.WorkflowCore/Primitives/OutcomeSwitch.cs`
  - `Atlas.WorkflowCore/Primitives/When.cs`

#### 任务 7.2：调度原语

- **文件：**
  - `Atlas.WorkflowCore/Primitives/Recur.cs` (循环调度)

#### 任务 7.3：Saga 原语

- **文件：**
  - `Atlas.WorkflowCore/Primitives/SagaContainer.cs`

#### 任务 7.4：子工作流原语

- **文件：**
  - `Atlas.WorkflowCore/Primitives/SubWorkflowStepBody.cs`

#### 任务 7.5：辅助原语

- **文件：**
  - `Atlas.WorkflowCore/Primitives/ActionStepBody.cs` (简化步骤定义)

#### 任务 7.6：更新现有原语

- **更新：** Activity.cs - 支持 effectiveDate、cancelCondition、token 管理

### 阶段八：模型和集合增强（优先级：中）

#### 任务 8.1：执行指针集合

- **文件：** `Atlas.WorkflowCore/Models/ExecutionPointerCollection.cs`
- **方法：** `FindById()`, `FindByStepId()`, `GetActivePointers()`, `GetWaitingPointers()`

#### 任务 8.2：步骤集合

- **文件：** `Atlas.WorkflowCore/Models/WorkflowStepCollection.cs`
- **方法：** `FindById()`, `FindByName()`, `Add()`, `Remove()`

#### 任务 8.3：工作流选项

- **文件：** `Atlas.WorkflowCore/Models/WorkflowOptions.cs`
- **属性：** PollInterval、IdleTime、MaxConcurrentWorkflows、EnableIndex 等

#### 任务 8.4：执行器结果

- **文件：** `Atlas.WorkflowCore/Models/WorkflowExecutorResult.cs`
- **属性：** `Subscriptions`、`Errors`、`RetryPointers`

#### 任务 8.5：表达式结果

- **更新：** `Atlas.WorkflowCore/Models/ExpressionOutcome.cs`
- **属性：** 支持表达式条件的结果

### 阶段九：异常类型（优先级：低）

#### 任务 9.1：定义自定义异常

- **文件：**
  - `Atlas.WorkflowCore/Exceptions/ActivityFailedException.cs`
  - `Atlas.WorkflowCore/Exceptions/CorruptPersistenceDataException.cs`
  - `Atlas.WorkflowCore/Exceptions/WorkflowNotRegisteredException.cs`
  - `Atlas.WorkflowCore/Exceptions/WorkflowDefinitionLoadException.cs`
  - `Atlas.WorkflowCore/Exceptions/WorkflowLockedException.cs`
  - `Atlas.WorkflowCore/Exceptions/NotFoundException.cs`

### 阶段十：DSL 支持（优先级：中）

#### 任务 10.1：创建 DSL 项目

- **项目：** `Atlas.WorkflowCore.DSL.csproj`
- **依赖：** `Atlas.WorkflowCore`、`Newtonsoft.Json`、`YamlDotNet`、`System.Linq.Dynamic.Core`

#### 任务 10.2：DSL 模型定义

- **文件：**
  - `Atlas.WorkflowCore.DSL/Models/DefinitionSource.cs`
  - `Atlas.WorkflowCore.DSL/Models/Envelope.cs`
  - `Atlas.WorkflowCore.DSL/Models/v1/DefinitionSourceV1.cs`
  - `Atlas.WorkflowCore.DSL/Models/v1/StepSourceV1.cs`
  - `Atlas.WorkflowCore.DSL/Models/v1/MappingSourceV1.cs`

#### 任务 10.3：核心接口

- **文件：**
  - `Atlas.WorkflowCore.DSL/Interface/IDefinitionLoader.cs`
  - `Atlas.WorkflowCore.DSL/Interface/ITypeResolver.cs`

#### 任务 10.4：实现定义加载器

- **文件：** `Atlas.WorkflowCore.DSL/Services/DefinitionLoader.cs`
- **职责：**
  - 解析表达式（Linq.Dynamic.Core）
  - 解析类型
  - 输入输出映射转换
  - 构建 WorkflowDefinition

#### 任务 10.5：实现类型解析器

- **文件：** `Atlas.WorkflowCore.DSL/Services/TypeResolver.cs`

#### 任务 10.6：反序列化器

- **文件：** `Atlas.WorkflowCore.DSL/Services/Deserializers.cs`
- **方法：** `Json`、`Yaml`

#### 任务 10.7：DI 扩展

- **文件：** `Atlas.WorkflowCore.DSL/ServiceCollectionExtensions.cs`

### 阶段十一：WorkflowHost 集成（优先级：最高）

#### 任务 11.1：更新 WorkflowHost 构造函数

- **更新：** `Services/WorkflowHost.cs`
- **新增依赖：**
  - `IQueueProvider`
  - `IDistributedLockProvider`
  - `ISearchIndex`
  - `IDateTimeProvider`
  - `IEnumerable<IBackgroundTask>`

#### 任务 11.2：实现 StartAsync / StopAsync

- **更新：** `Services/WorkflowHost.cs`
- **逻辑：**
  - 启动 IQueueProvider、IDistributedLockProvider
  - 启动所有 IBackgroundTask（WorkflowConsumer、EventConsumer、IndexConsumer、RunnablePoller）
  - 停止时优雅关闭

#### 任务 11.3：更新 StartWorkflowAsync

- **逻辑：**
  - 创建工作流实例
  - 调用 IPersistenceProvider.CreateWorkflowAsync
  - 调用 IQueueProvider.QueueWork(id, QueueType.Workflow)
  - 调用 IQueueProvider.QueueWork(id, QueueType.Index)
  - 发布 WorkflowStarted 事件

#### 任务 11.4：更新 PublishEventAsync

- **逻辑：**
  - 创建 Event
  - 调用 IPersistenceProvider.CreateEventAsync
  - 调用 IQueueProvider.QueueWork(eventId, QueueType.Event)

### 阶段十二：WorkflowExecutor 集成（优先级：最高）

#### 任务 12.1：更新 WorkflowExecutor 构造函数

- **新增依赖：**
  - `IExecutionResultProcessor`
  - `IExecutionPointerFactory`
  - `ICancellationProcessor`
  - `IEnumerable<IWorkflowMiddleware>`
  - `IWorkflowMiddlewareRunner`

#### 任务 12.2：集成中间件

- **逻辑：**
  - 执行前调用 PreWorkflow 中间件
  - 执行过程调用 ExecuteWorkflow 中间件
  - 执行后调用 PostWorkflow 中间件

#### 任务 12.3：集成执行结果处理器

- **逻辑：**
  - 步骤执行后调用 `IExecutionResultProcessor.ProcessResult()`
  - 处理分支、事件订阅、休眠

#### 任务 12.4：集成取消处理器

- **逻辑：**
  - 执行前检查取消条件
  - 触发取消补偿逻辑

### 阶段十三：DI 注册更新（优先级：最高）

#### 任务 13.1：更新 ServiceCollectionExtensions

- **文件：** `Atlas.WorkflowCore/ServiceCollectionExtensions.cs`
- **新增注册：**
  - 队列提供者（默认 SingleNodeQueueProvider）
  - 锁提供者（默认 SingleNodeLockProvider）
  - 搜索索引（默认 NullSearchIndex）
  - 后台任务（WorkflowConsumer、EventConsumer、IndexConsumer、RunnablePoller）
  - 核心服务（ExecutionResultProcessor、ExecutionPointerFactory、CancellationProcessor、ActivityController、SyncWorkflowRunner、ScopeProvider、GreyList、DateTimeProvider）
  - 错误处理器（RetryHandler、SuspendHandler、TerminateHandler、CompensateHandler）
  - 中间件运行器（WorkflowMiddlewareRunner、DefaultWorkflowMiddlewareErrorHandler）

#### 任务 13.2：选项配置

- **新增：** `AddWorkflowCore(Action<WorkflowOptions>)` 重载

### 阶段十四：测试和示例（优先级：中）

#### 任务 14.1：创建测试项目

- **项目：** `Atlas.WorkflowCore.Tests.csproj`
- **框架：** xUnit + FluentAssertions

#### 任务 14.2：单元测试

- **覆盖：**
  - ExecutionPointerFactory
  - ExecutionResultProcessor
  - CancellationProcessor
  - ErrorHandlers
  - Primitives

#### 任务 14.3：集成测试

- **场景：**
  - 完整工作流执行
  - 事件订阅和发布
  - 错误处理和重试
  - Saga 补偿
  - DSL 加载和执行

#### 任务 14.4：示例工作流

- **创建：** Sample01-15 对应开源版本示例

---

## 实现优先级排序

### P0（关键路径，必须先完成）

1. 队列系统（任务 1.1）
2. 分布式锁（任务 1.2）
3. 后台任务系统（任务 2.1-2.5）
4. 执行结果处理器（任务 3.1）
5. 执行指针工厂（任务 3.2）
6. WorkflowHost 集成（任务 11.1-11.4）
7. WorkflowExecutor 集成（任务 12.1-12.4）
8. DI 注册更新（任务 13.1-13.2）

### P1（核心功能增强）

9. 错误处理系统（任务 4.1-4.3）
10. Builder API 增强（任务 6.1-6.3）
11. 内置原语补全（任务 7.1-7.6）
12. 计划命令支持（任务 1.3）
13. 活动控制器（任务 3.4）

### P2（扩展能力）

14. 中间件系统（任务 5.1-5.3）
15. DSL 支持（任务 10.1-10.7）
16. 搜索索引（任务 1.4）
17. 模型和集合增强（任务 8.1-8.5）

### P3（辅助功能）

18. 同步运行器（任务 3.5）
19. 辅助服务（任务 3.6）
20. 异常类型（任务 9.1）
21. 测试和示例（任务 14.1-14.4）

---

## 关键代码参考

### 1. 队列提供者示例

```csharp
public interface IQueueProvider
{
    Task QueueWork(string id, QueueType queue);
    Task<string?> DequeueWork(QueueType queue, CancellationToken token);
    bool IsDequeueBlocking { get; }
    Task Start();
    Task Stop();
}

public enum QueueType { Workflow = 0, Event = 1, Index = 2 }
```

### 2. WorkflowConsumer 示例逻辑

```csharp
public class WorkflowConsumer : IBackgroundTask
{
    public async Task Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var workflowId = await _queueProvider.DequeueWork(QueueType.Workflow, token);
            if (workflowId == null) continue;

            if (await _lockProvider.AcquireLock(workflowId, token))
            {
                try
                {
                    var instance = await _persistenceProvider.GetWorkflowAsync(workflowId, token);
                    var result = await _executor.Execute(instance, token);
                    await _persistenceProvider.PersistWorkflowAsync(instance, token);
                    
                    if (instance.Status == WorkflowStatus.Runnable)
                        await _queueProvider.QueueWork(workflowId, QueueType.Workflow);
                }
                finally
                {
                    await _lockProvider.ReleaseLock(workflowId);
                }
            }
        }
    }
}
```

### 3. Builder API 增强示例

```csharp
builder
    .StartWith<Step1>()
        .Input(step => step.Value1, data => data.InputValue)
        .Output(data => data.Result, step => step.OutputValue)
        .OnError(WorkflowErrorHandling.Retry, TimeSpan.FromSeconds(30))
        .CompensateWith<CompensateStep1>()
    .Then<Step2>()
        .CancelCondition(data => data.ShouldCancel)
    .Decide(data => data.Status)
        .Branch((data, outcome) => outcome.Equals("Success"), then => 
            then.StartWith<SuccessStep>())
        .Branch((data, outcome) => outcome.Equals("Failure"), then => 
            then.StartWith<FailureStep>());
```

---

## 验收标准

完成以上所有任务后，系统应具备以下能力：

1. **分布式执行** - 多节点部署，队列调度，分布式锁保护
2. **自动轮询** - WorkflowHost 启动后自动轮询和执行
3. **完整错误处理** - Retry、Suspend、Terminate、Compensate
4. **事件驱动** - 支持事件订阅、发布、唤醒
5. **外部活动** - 支持人工任务、第三方系统集成
6. **Saga 事务** - 支持补偿逻辑
7. **DSL 定义** - 支持 JSON/YAML 定义工作流
8. **中间件扩展** - 支持日志、监控、验证等扩展
9. **搜索能力** - 支持工作流实例搜索
10. **完整 Builder API** - 支持复杂工作流定义

---

## 预估工作量

- **P0 任务：** 约 40-50 小时（8-10 个工作日）
- **P1 任务：** 约 30-40 小时（6-8 个工作日）
- **P2 任务：** 约 20-30 小时（4-6 个工作日）
- **P3 任务：** 约 10-20 小时（2-4 个工作日）

**总计：** 约 100-140 小时（20-28 个工作日）

---

## 文件清单预估

新增文件：约 80-100 个 .cs 文件

修改文件：约 20-30 个现有文件

新增项目：1 个（Atlas.WorkflowCore.DSL）

---

## 依赖包

需要添加的 NuGet 包：

**Atlas.WorkflowCore**

- System.Linq.Dynamic.Core
- System.Threading.Channels (用于队列)
- Microsoft.Extensions.ObjectPool (用于对象池)

**Atlas.WorkflowCore.DSL**

- Newtonsoft.Json
- YamlDotNet
- System.Linq.Dynamic.Core