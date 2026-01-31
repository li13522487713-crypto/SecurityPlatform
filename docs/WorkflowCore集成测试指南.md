# WorkflowCore 可视化集成 - 测试指南

## 集成概述

已成功将 demo 中的 WorkflowCore 审批流引擎集成到 Atlas.WebApi 后端，并在前端实现了基于 @antv/x6 的可视化工作流设计器。

### 实现的功能

**后端 API**：
1. ✅ `GET /api/v1/workflows/step-types` - 获取所有可用的 Primitive 步骤类型（Delay、If、While、Foreach 等）
2. ✅ `POST /api/v1/workflows/definitions` - 从 JSON 定义注册动态工作流
3. ✅ `GET /api/v1/workflows/instances/{id}/pointers` - 获取执行指针详情（步骤级监控）
4. ✅ 复用现有的工作流实例管理 API（启动、挂起、恢复、终止）

**前端页面**：
1. ✅ 工作流设计器 (`/workflow/designer`) - 拖拽式工作流设计
2. ✅ 实例监控页面 (`/workflow/instances`) - 实时监控执行状态

**基础设施**：
1. ✅ `WorkflowHostedService` - 自动启动/停止 WorkflowHost
2. ✅ DSL 支持 - 使用 DefinitionLoader 加载 JSON 定义
3. ✅ 菜单集成 - 主布局添加"工作流引擎"子菜单

## 端到端测试步骤

### 1. 启动后端服务

```bash
cd src/backend/Atlas.WebApi
dotnet run
```

服务将在 `http://localhost:5000` 启动。

### 2. 启动前端服务

```bash
cd src/frontend/Atlas.WebApp
npm install  # 首次运行需要
npm run dev
```

前端将在 `http://localhost:5173` 启动。

### 3. 登录系统

1. 访问 `http://localhost:5173`
2. 使用默认管理员账户登录：
   - 租户ID: `00000000-0000-0000-0000-000000000001`
   - 用户名: `admin`
   - 密码: `P@ssw0rd!`

### 4. 测试工作流设计器

#### 4.1 访问设计器

点击左侧菜单"工作流引擎" → "工作流设计器"，或直接访问 `/workflow/designer`。

#### 4.2 设计简单工作流

**场景：延迟 5 秒的简单工作流**

1. 输入工作流ID：`test-delay-workflow`
2. 从左侧工具栏拖拽"延迟"节点到画布
3. 双击节点，配置参数：
   - 节点名称：等待5秒
   - Period（延迟时长）：`00:00:05`（5秒）
4. 点击"确定"保存节点配置
5. 点击"保存工作流"按钮

**预期结果**：
- 显示"工作流注册成功"提示
- 后端已注册该工作流定义

#### 4.3 测试执行

1. 点击"测试执行"按钮
2. 在弹出的对话框中：
   - 工作流数据：`{}`（或保持默认）
   - 引用标识：`test-001`（可选）
3. 点击"确定"

**预期结果**：
- 显示"工作流已启动，实例ID: xxx"
- 自动跳转到实例监控页面

### 5. 测试实例监控

#### 5.1 查看实例列表

在实例监控页面，应该能看到：
- 刚刚启动的工作流实例
- 状态显示为"运行中"或"已完成"
- 创建时间、完成时间等信息

#### 5.2 查看执行详情

1. 点击某个实例的"查看详情"按钮
2. 右侧抽屉显示实例详情和执行指针

**预期看到的执行指针**：
- 时间线显示步骤执行状态
- "等待5秒"步骤：
  - 初始状态：`Sleeping`（休眠中）
  - 5秒后：`Complete`（已完成）
- 显示开始时间、结束时间、睡眠到时间

#### 5.3 实时监控

1. 开启"自动刷新"开关
2. 观察执行指针状态每 3 秒自动更新

### 6. 测试复杂工作流

#### 6.1 设计条件分支工作流

**场景：根据条件执行不同延迟**

1. 创建新工作流ID：`test-condition-workflow`
2. 拖拽节点并连线：
   - 节点1：If（条件判断）
     - Condition: `true`（简化测试，实际可配置数据表达式）
   - 节点2：Delay（延迟3秒）
     - Period: `00:00:03`
   - 连接：If → Delay
3. 保存并测试执行

#### 6.2 设计循环工作流

**场景：遍历集合**

1. 创建工作流ID：`test-foreach-workflow`
2. 拖拽 Foreach 节点：
   - Collection: `[1,2,3]`（JSON 数组）
   - RunParallel: `true`
3. 在 Foreach 内部连接一个 Delay 节点
4. 保存并测试

**预期结果**：
- 执行指针显示多个并行执行的分支
- 每个分支代表集合中的一个元素

### 7. 测试工作流控制操作

#### 7.1 挂起工作流

1. 启动一个包含长时间延迟的工作流（如 30 秒）
2. 在实例列表中，点击"挂起"按钮

**预期结果**：
- 状态变更为"已挂起"
- 执行指针停止更新

#### 7.2 恢复工作流

1. 点击"恢复"按钮

**预期结果**：
- 状态变更为"运行中"
- 工作流继续执行

#### 7.3 终止工作流

1. 点击"终止"按钮

**预期结果**：
- 状态变更为"已终止"
- 工作流不再执行

### 8. 使用 HTTP 文件测试（可选）

如果您使用 REST Client 或类似工具，可以直接测试 API：

```http
# 1. 登录获取 Token
POST http://localhost:5000/api/v1/auth/token
Content-Type: application/json
X-Tenant-Id: 00000000-0000-0000-0000-000000000001

{
  "username": "admin",
  "password": "P@ssw0rd!"
}

# 2. 获取步骤类型
GET http://localhost:5000/api/v1/workflows/step-types
Authorization: Bearer {{accessToken}}
X-Tenant-Id: 00000000-0000-0000-0000-000000000001

# 3. 注册工作流
POST http://localhost:5000/api/v1/workflows/definitions
Content-Type: application/json
Authorization: Bearer {{accessToken}}
X-Tenant-Id: 00000000-0000-0000-0000-000000000001

{
  "workflowId": "test-api-workflow",
  "version": 1,
  "definitionJson": "{\"Id\":\"test-api-workflow\",\"Version\":1,\"Steps\":[{\"Id\":\"step1\",\"Name\":\"延迟5秒\",\"StepType\":\"Delay\",\"Inputs\":{\"Period\":\"00:00:05\"}}]}"
}

# 4. 启动工作流
POST http://localhost:5000/api/v1/workflows/instances
Content-Type: application/json
Authorization: Bearer {{accessToken}}
X-Tenant-Id: 00000000-0000-0000-0000-000000000001

{
  "workflowId": "test-api-workflow",
  "version": 1,
  "data": {},
  "reference": "api-test-001"
}

# 5. 获取执行指针
GET http://localhost:5000/api/v1/workflows/instances/{{instanceId}}/pointers
Authorization: Bearer {{accessToken}}
X-Tenant-Id: 00000000-0000-0000-0000-000000000001
```

完整的测试用例请参考：`src/backend/Atlas.WebApi/Bosch.http/Workflow.http`

## 支持的步骤类型

### 时间控制类

| 类型 | 名称 | 参数 | 说明 |
|------|------|------|------|
| Delay | 延迟 | Period (timespan) | 延迟指定时长后继续 |
| WaitFor | 等待事件 | EventName (string), EventKey (string), EffectiveDate (datetime) | 等待外部事件发生 |

### 控制流类

| 类型 | 名称 | 参数 | 说明 |
|------|------|------|------|
| If | 条件判断 | Condition (bool) | 如果条件为真则执行子步骤 |
| While | 循环 | Condition (bool) | 当条件为真时循环执行 |
| Foreach | 遍历 | Collection (array), RunParallel (bool) | 遍历集合并对每个元素执行子步骤 |
| Decide | 分支决策 | - | 根据结果选择不同分支 |
| Recur | 重复执行 | Interval (timespan), StopCondition (bool) | 按间隔重复执行直到满足停止条件 |

### 容器类

| 类型 | 名称 | 参数 | 说明 |
|------|------|------|------|
| Sequence | 顺序容器 | - | 按顺序执行多个子步骤 |

## 已知限制

1. **表达式支持**：当前 Condition 参数需要配置为简单的布尔值或数据路径表达式，暂不支持复杂的动态表达式。建议在实际使用时扩展为支持 DynamicExpression 库。

2. **数据类型转换**：从前端传递的参数是字符串格式，DSL DefinitionLoader 会自动转换。复杂类型（如集合）需要使用 JSON 格式字符串。

3. **容器步骤的子步骤配置**：当前设计器主要支持顺序连接的步骤，对于需要嵌套子步骤的容器类型（如 If、While、Foreach），需要在前端设计器中增强嵌套支持。

4. **持久化提供者**：当前使用内存持久化（InMemoryPersistenceProvider），重启服务后工作流实例数据会丢失。生产环境建议切换到基于 SqlSugar 的持久化提供者。

## 故障排查

### 问题：工作流注册成功但无法启动

**原因**：工作流定义 JSON 格式不正确或步骤类型不存在。

**解决**：
1. 检查 DSL v1 格式是否正确
2. 确认 StepType 拼写正确（如 `Delay` 而不是 `delay`）
3. 查看后端日志中的详细错误信息

### 问题：执行指针不更新

**原因**：WorkflowHost 未启动或工作流卡在某个步骤。

**解决**：
1. 检查后端日志确认 WorkflowHost 是否成功启动
2. 查看实例详情中的执行指针状态
3. 如果状态为 `WaitingForEvent`，需要发布相应的外部事件
4. 如果状态为 `Failed`，查看错误消息

### 问题：前端页面无法加载步骤类型

**原因**：API 调用失败或未登录。

**解决**：
1. 打开浏览器开发者工具查看网络请求
2. 确认 Token 和 Tenant-Id 请求头正确
3. 检查后端服务是否正常运行

## 后续改进建议

1. **持久化**：实现基于 SqlSugar 的 IPersistenceProvider，支持数据库持久化和多租户隔离。

2. **表达式引擎**：集成 DynamicExpression 或类似库，支持复杂的条件表达式和数据映射。

3. **设计器增强**：
   - 支持容器步骤的嵌套可视化
   - 添加撤销/重做功能
   - 支持流程模板和快速复制
   - 添加流程验证（如检测循环依赖、孤立节点等）

4. **监控增强**：
   - 添加执行统计图表（耗时分布、成功率等）
   - 支持历史实例查询和筛选
   - 添加告警规则（如执行超时、失败率过高）

5. **步骤库扩展**：
   - 创建自定义业务步骤（如发送邮件、调用 API、数据库操作等）
   - 支持步骤参数的可视化配置（下拉框、日期选择器等）

## 总结

✅ **后端集成完成**：
- 0 编译错误，0 编译警告
- 所有 API 端点已实现
- WorkflowHost 自动启动/停止
- DSL 支持动态工作流注册

✅ **前端集成完成**：
- 工作流设计器支持拖拽和参数配置
- 实例监控页面支持实时状态更新
- 路由和菜单已集成

✅ **测试就绪**：
- HTTP 测试文件已更新
- 提供完整的端到端测试步骤
- 支持 8 种 Primitive 步骤类型

本次集成成功将 WorkflowCore 通用工作流引擎接入到 Atlas 安全平台，为后续的复杂业务流程自动化奠定了基础。

