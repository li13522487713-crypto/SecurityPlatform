# PRD: Phase 1 - 平台架构与业务场景加速 (Architecture & Acceleration)

## 1. 业务目标
基于等保2.0合规要求以及业务复杂化，对本次涉及的7个高级特性的基础设施层进行增强，构建坚固的资源拦截鉴权网与详尽的审计留存机制。核心为：**越权防御（RBAC细粒度拦截）和 安全审计追踪（Audit Trails）**。

## 2. 功能需求

### 2.1 细粒度资源拦截鉴权层 (Strict Resource Authorization Layer)
- **需求描述**: 当开启数据预览、执行SQL直连查询（SQLQueryService）、发起审批流程动作、或动态表单定义管理时，进行除路由外、细至“资源ID级别”的鉴权。
- **拦截方式**:
  - 实现基于 `IAuthorizationFilter` 或在 Service 层实现的资源级效验拦截。
  - 特别是针对上文 `TenantDataSourcesController` 中增加的数据直连能力，必须进行等级鉴别。

### 2.2 特权操作安全审计 (Security Audit Trails for Phase 1 Features)
- **需求描述**: 新增 Phase 1 内相关的特性动作写入到 `AuditTrail` 表中，以满足等保2.0的要求（操作行为不可抵赖，操作记录详尽）。
- **具体记录范围**:
  - 动态表单配置的更改 (`FormDefinition` 的修改和发布)。
  - 高级直连数据源的SQL执行请求 (包含原始SQL片段和操作状态)。
  - 其他全局级别的核心 `DynamicTable` 元数据定义的增删改。
- **技术实现**: 使用当前的 `IAuditTrailService`，拦截或者在 Service 实现内手动写审计日志。

## 3. 实现计划

### 3.1 Backend (后端)
1. **统一鉴权网拦截器**: 
   - 检查已有的鉴权模型（如果存在属性 `[Authorize]`），针对 `TenantDataSourcesController.PreviewQuery` 补齐相应的 `PermissionRequirement`。
   - 实现或确保 `SqlQueryService` 中带有细粒度权限断言。
2. **审计集成**: 
   - 在 `FormDefinitionCommandService` 的 Update / Publish 方法中调用审计日志服务，记录 "Form Schema Changed"。
   - 在 `SqlQueryService` 的执行中记录日志（包含被发起的SQL预览日志和租户ID），对于敏感数据操作不留存具体返回数据，只留存操作意图与成功与否。

## 4. 安全合规说明
满足《等保2.0基本要求 第八级 安全计算环境》:
- 应对用户操作行为进行审计跟踪，包括事件发生的日期、时间、用户标识。
- 该机制与系统中已有的 `AuditTrails` 表（SqlSugar）直接打通，保持原有的防篡改逻辑。
