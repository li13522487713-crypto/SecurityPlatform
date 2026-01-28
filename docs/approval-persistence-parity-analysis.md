# 审批模块持久化模型对齐分析（AntFlow 对照）

## 当前 Atlas 已有实体

1. **ApprovalFlowDefinition** - 流程定义（对应 `t_bpmn_conf`）
2. **ApprovalProcessInstance** - 流程实例（对应 `bpm_flowruninfo`）
3. **ApprovalTask** - 审批任务（对应 `bpm_af_task`）
4. **ApprovalHistoryEvent** - 历史事件（审计追溯）
5. **ApprovalNodeExecution** - 节点执行记录（对应 `bpm_process_node_record`）
6. **ApprovalProcessVariable** - 流程变量（对应 `t_bpm_variable`）
7. **ApprovalTaskTransfer** - 转办记录（对应 `bpm_flowrun_entrust`）
8. **ApprovalTaskAssigneeChange** - 加签/减签记录
9. **ApprovalDepartmentLeader** - 部门负责人映射

## AntFlow 存在但 Atlas 缺失的表

### 1. 按钮配置表（必须结构化落库）

**AntFlow 表：**
- `t_bpmn_view_page_button` - 流程级别的按钮配置（发起人视图/审批人视图）
- `t_bpmn_node_button_conf` - 节点级别的按钮配置

**决策：**
- **流程级别按钮配置**：结构化落库（需要按流程查询、权限控制）
- **节点级别按钮配置**：放入 `DefinitionJson`（节点属性的一部分）

**实现：** `ApprovalFlowButtonConfig` 实体

### 2. 通知模板表（必须结构化落库）

**AntFlow 表：**
- `t_bpmn_conf_notice_template` - 流程通知模板主表
- `t_bpmn_conf_notice_template_detail` - 通知模板详情
- `t_information_template` - 消息模板（系统级）

**决策：**
- **流程通知模板**：结构化落库（需要独立管理和复用）
- **系统消息模板**：结构化落库（跨流程复用）

**实现：** `ApprovalNotificationTemplate` 实体（简化版，先支持流程级）

### 3. 外部回调记录表（必须结构化落库）

**AntFlow 表：**
- `t_out_side_bpm_call_back_record` - 外部系统回调记录/重试队列

**决策：**
- **必须结构化落库**：需要重试队列、状态跟踪、幂等性保护

**实现：** `ApprovalExternalCallbackRecord` 实体

### 4. 版本信息表（可选，先放入 DefinitionJson）

**AntFlow 表：**
- `t_sys_version` - 系统版本信息

**决策：**
- **先放入 DefinitionJson 元数据**：`ApprovalFlowDefinition.Version` 已存在
- **如需版本历史**：后续可扩展为独立表

**暂不实现**

### 5. 流程操作记录表（必须结构化落库 - 幂等性）

**AntFlow 表：**
- `bpm_process_operation` - 流程操作记录（用于幂等性保护）

**决策：**
- **必须结构化落库**：关键操作的幂等键与重复提交保护

**实现：** `ApprovalOperationRecord` 实体（记录操作类型 + 幂等键）

## 实施优先级

### Phase 1（立即实现）
1. ✅ `ApprovalOperationRecord` - 操作记录/幂等性保护
2. ✅ `ApprovalFlowButtonConfig` - 按钮配置（流程级）

### Phase 2（后续扩展）
3. `ApprovalNotificationTemplate` - 通知模板
4. `ApprovalExternalCallbackRecord` - 外部回调记录

## 放入 DefinitionJson 的配置

以下配置**不需要**独立表，放入 `DefinitionJson` 的节点配置中：
- 节点级别的按钮配置（每个节点可用的操作）
- 节点条件规则详情（已有 `ApprovalNodeExecution` 记录执行状态）
- 节点审批人策略详情（已有 `ApprovalTask` 记录实际分配）
