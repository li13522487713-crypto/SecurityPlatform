# AntFlow.net 与 Atlas 审批流能力对照清单

## 一、运行时操作/按钮能力对照

| AntFlow.net 操作类型 | Atlas 当前状态 | 优先级 | 说明 |
|---------------------|---------------|--------|------|
| BUTTON_TYPE_SUBMIT (1) 流程提交 | ✅ 已实现 | - | `ApprovalRuntimeCommandService.StartAsync` |
| BUTTON_TYPE_RESUBMIT (2) 重新提交 | ❌ 缺失 | 高 | 打回后重新提交流程 |
| BUTTON_TYPE_AGREE (3) 同意 | ✅ 已实现 | - | `ApprovalRuntimeCommandService.ApproveTaskAsync` |
| BUTTON_TYPE_DIS_AGREE (4) 不同意 | ✅ 已实现 | - | `ApprovalRuntimeCommandService.RejectTaskAsync`（但当前直接终止流程） |
| BUTTON_TYPE_VIEW_BUSINESS_PROCESS (5) 查看流程详情 | ✅ 已实现 | - | `ApprovalRuntimeQueryService.GetInstanceByIdAsync` |
| BUTTON_TYPE_ABANDON (7) 作废 | ⚠️ 部分实现 | 中 | 当前有 `CancelInstanceAsync`，但缺少"作废"语义 |
| BUTTON_TYPE_UNDERTAKE (10) 承办 | ❌ 缺失 | 低 | 承办任务 |
| BUTTON_TYPE_CHANGE_ASSIGNEE (11) 变更处理人 | ❌ 缺失 | 高 | 变更当前节点处理人 |
| BUTTON_TYPE_STOP (12) 终止 | ⚠️ 部分实现 | 中 | 当前有 `CancelInstanceAsync`，但缺少"终止"语义 |
| BUTTON_TYPE_FORWARD (15) 转发 | ❌ 缺失 | 低 | 转发任务 |
| BUTTON_TYPE_BACK_TO_MODIFY (18) 打回修改 | ❌ 缺失 | 高 | 打回发起人修改 |
| BUTTON_TYPE_JP (19) 加批 | ❌ 缺失 | 中 | 加批操作 |
| BUTTON_TYPE_ZB (21) 转办 | ❌ 缺失 | 高 | 转办任务给他人 |
| BUTTON_TYPE_CHOOSE_ASSIGNEE (22) 自选审批人 | ❌ 缺失 | 中 | 自选审批人 |
| BUTTON_TYPE_BACK_TO_ANY_NODE (23) 退回任意节点 | ❌ 缺失 | 高 | 退回至指定节点 |
| BUTTON_TYPE_REMOVE_ASSIGNEE (24) 减签 | ❌ 缺失 | 中 | 当前节点减签 |
| BUTTON_TYPE_ADD_ASSIGNEE (25) 加签 | ❌ 缺失 | 高 | 当前节点加签 |
| BUTTON_TYPE_CHANGE_FUTURE_ASSIGNEE (26) 变更未来节点处理人 | ❌ 缺失 | 中 | 变更未来节点处理人 |
| BUTTON_TYPE_REMOVE_FUTURE_ASSIGNEE (27) 未来节点减签 | ❌ 缺失 | 低 | 未来节点减签 |
| BUTTON_TYPE_ADD_FUTURE_ASSIGNEE (28) 未来节点加签 | ❌ 缺失 | 低 | 未来节点加签 |
| BUTTON_TYPE_PROCESS_DRAW_BACK (29) 流程撤回 | ❌ 缺失 | 高 | 发起人撤回流程 |
| BUTTON_TYPE_SAVE_DRAFT (30) 保存草稿 | ❌ 缺失 | 低 | 保存草稿 |
| BUTTON_TYPE_RECOVER_TO_HIS (31) 恢复已结束流程 | ❌ 缺失 | 低 | 恢复已结束流程 |
| BUTTON_TYPE_DRAW_BACK_AGREE (32) 撤销同意 | ❌ 缺失 | 高 | 撤销已同意的审批 |
| BUTTON_TYPE_PROCESS_MOVE_AHEAD (33) 流程推进 | ❌ 缺失 | 低 | 管理员推进流程 |

## 二、核心引擎能力对照

| 能力项 | AntFlow.net | Atlas 当前状态 | 优先级 |
|--------|------------|---------------|--------|
| 流程定义管理 | ✅ 完整 | ✅ 已实现 | - |
| 流程版本管理 | ✅ 完整 | ✅ 已实现 | - |
| 流程实例管理 | ✅ 完整 | ✅ 基础实现 | - |
| 多节点流转 | ✅ 支持 | ❌ 仅单节点 | 高 |
| 条件分支 | ✅ 支持 | ⚠️ 配置支持但未执行 | 高 |
| 会签（全部通过） | ✅ 支持 | ⚠️ 配置支持但未执行 | 高 |
| 或签（任一通过） | ✅ 支持 | ⚠️ 配置支持但未执行 | 高 |
| 顺序会签 | ✅ 支持 | ❌ 缺失 | 中 |
| 并行网关 | ✅ 支持 | ❌ 缺失 | 中 |
| 流程变量 | ✅ 支持 | ❌ 缺失 | 中 |
| 节点执行记录 | ✅ 支持 | ⚠️ 部分（历史事件） | 中 |
| 缺失审批人策略 | ✅ 支持（不允许/跳过/转管理员） | ❌ 缺失 | 中 |

## 三、业务集成能力对照

| 能力项 | AntFlow.net | Atlas 当前状态 | 优先级 |
|--------|------------|---------------|--------|
| 表单操作适配器 (IFormOperationAdaptor) | ✅ 完整 | ❌ 缺失 | 中 |
| 业务数据回调 | ✅ 完整 | ❌ 缺失 | 中 |
| 外部系统接入 | ✅ 支持 | ❌ 缺失 | 低 |

## 四、实施优先级

### P0（核心功能，必须实现）
1. 多节点流转引擎
2. 条件分支执行
3. 会签/或签策略执行
4. 撤回（BUTTON_TYPE_PROCESS_DRAW_BACK）
5. 打回修改（BUTTON_TYPE_BACK_TO_MODIFY）
6. 退回任意节点（BUTTON_TYPE_BACK_TO_ANY_NODE）
7. 重新提交（BUTTON_TYPE_RESUBMIT）
8. 转办（BUTTON_TYPE_ZB）
9. 加签（BUTTON_TYPE_ADD_ASSIGNEE）
10. 变更处理人（BUTTON_TYPE_CHANGE_ASSIGNEE）
11. 撤销同意（BUTTON_TYPE_DRAW_BACK_AGREE）

### P1（重要功能，建议实现）
1. 减签（BUTTON_TYPE_REMOVE_ASSIGNEE）
2. 顺序会签
3. 流程变量
4. 缺失审批人策略
5. 加批（BUTTON_TYPE_JP）
6. 自选审批人（BUTTON_TYPE_CHOOSE_ASSIGNEE）

### P2（可选功能，按需实现）
1. 承办（BUTTON_TYPE_UNDERTAKE）
2. 转发（BUTTON_TYPE_FORWARD）
3. 变更未来节点处理人（BUTTON_TYPE_CHANGE_FUTURE_ASSIGNEE）
4. 未来节点加签/减签
5. 保存草稿（BUTTON_TYPE_SAVE_DRAFT）
6. 恢复已结束流程（BUTTON_TYPE_RECOVER_TO_HIS）
7. 流程推进（BUTTON_TYPE_PROCESS_MOVE_AHEAD）
8. 并行网关
9. 表单操作适配器

## 五、实施路径

1. **阶段一（核心引擎）**：实现多节点流转、条件分支、会签/或签
2. **阶段二（运行时操作）**：实现 P0 优先级的所有运行时操作
3. **阶段三（增强功能）**：实现 P1 优先级功能
4. **阶段四（扩展功能）**：实现 P2 优先级功能（按需）
