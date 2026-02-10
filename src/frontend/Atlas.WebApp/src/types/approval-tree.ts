import type {
  JsonValue,
  ApproverConfig,
  ButtonPermissionConfig,
  FormPermissionConfig,
  NoticeConfig,
  ConditionOperator
} from './approval-definition';

export type NodeType =
  | 'start'
  | 'approve'
  | 'copy'
  | 'condition'
  | 'parallel'
  | 'dynamicCondition'
  | 'parallelCondition'
  | 'end'
  | 'inclusive'
  | 'route'
  | 'callProcess'
  | 'timer'
  | 'trigger';

export interface TreeNodeBase {
  id: string;
  nodeType: NodeType;
  nodeName: string;
  error?: boolean;  // 配置错误标记
}

// 开始节点
export interface StartNode extends TreeNodeBase {
  nodeType: 'start';
  childNode?: TreeNode;
}

// 审批节点
export interface ApproveNode extends TreeNodeBase {
  nodeType: 'approve';
  assigneeType: 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7;  // 用户/角色/部门负责人/HRBP/直属领导/层级领导/发起人/发起人自选
  assigneeValue: string;
  approvalMode: 'all' | 'any' | 'sequential' | 'vote';
  noHeaderAction?: 0 | 1 | 2; // 无审批人策略：不允许/跳过/转管理员
  approverConfig?: ApproverConfig;
  buttonPermissionConfig?: ButtonPermissionConfig;
  formPermissionConfig?: FormPermissionConfig;
  noticeConfig?: NoticeConfig;
  childNode?: TreeNode;

  // 高级设置
  timeoutEnabled?: boolean;
  timeoutHours?: number;
  timeoutMinutes?: number;
  timeoutAction?: 'none' | 'autoApprove' | 'autoReject' | 'autoSkip';
  reminderIntervalHours?: number;
  maxReminderCount?: number;
  deduplicationType?: 'none' | 'skipSame' | 'global';
  excludeUserIds?: string[];
  excludeRoleCodes?: string[];

  // 新增属性
  voteWeight?: number;
  votePassRate?: number;
  rejectStrategy?: 'toPrevious' | 'toInitiator' | 'toAnyNode';
  reApproveStrategy?: 'continue' | 'backToRejectNode';
  groupStrategy?: 'claim' | 'allParticipate';
  callAi?: boolean;
  aiConfig?: string;
}

// 抄送节点
export interface CopyNode extends TreeNodeBase {
  nodeType: 'copy';
  copyToUsers: string[];
  formPermissionConfig?: FormPermissionConfig;
  childNode?: TreeNode;
}

// 条件节点（分支）
export interface ConditionNode extends TreeNodeBase {
  nodeType: 'condition';
  conditionNodes: ConditionBranch[];  // 多个分支
  childNode?: TreeNode; // 条件节点本身通常没有直接的后续节点，后续节点在分支中，或者汇聚后的节点。
                        // 但在 AntFlow 设计中，条件节点汇聚后可以有后续节点。
                        // 这里我们假设条件节点汇聚后连接到 childNode。
                        // 如果 AntFlow 树结构设计是分支汇聚后自动结束或连接到父级的 next，则需要调整。
                        // 根据计划文档：
                        // "条件节点添加后，会产生分支结构... 条件节点能够嵌套子节点列表... 实现条件内再分支的递归结构"
                        // 通常树形结构设计器（如 GitHub StavinLi/Workflow）中，ConditionNode 包含 conditionNodes，
                        // 而汇聚后的节点通常作为 ConditionNode 的 childNode 存在，或者由父级控制。
                        // 让我们参考计划中的定义：
                        // export interface ConditionNode extends TreeNodeBase {
                        //   nodeType: 'condition';
                        //   conditionNodes: ConditionBranch[];
                        // }
                        // 计划中 ConditionNode 没有 childNode，这意味着条件分支汇聚后就结束了？
                        // 或者汇聚后的节点挂在哪里？
                        // 在钉钉审批流中，条件分支汇聚后，可以继续添加节点。这个节点通常属于 ConditionNode 的“后续”。
                        // 让我们给 ConditionNode 加上 childNode 以支持汇聚后的流程。
}

export interface DynamicConditionNode extends TreeNodeBase {
  nodeType: 'dynamicCondition';
  conditionNodes: ConditionBranch[];
  childNode?: TreeNode;
}

export interface ParallelConditionNode extends TreeNodeBase {
  nodeType: 'parallelCondition';
  conditionNodes: ConditionBranch[];
  childNode?: TreeNode;
}

export interface ParallelNode extends TreeNodeBase {
  nodeType: 'parallel';
  parallelNodes: TreeNode[];
  childNode?: TreeNode;
}

export interface ConditionBranch {
  id: string;
  branchName: string;
  conditionRule?: ConditionRule; // 兼容旧版
  conditionGroups?: ConditionGroup[]; // 新版条件组（OR关系）
  priority?: number; // 优先级
  childNode?: TreeNode;
  isDefault?: boolean;  // 默认分支（兜底）
}

export interface ConditionGroup {
  conditions: ConditionExpression[]; // 组内条件（AND关系）
}

export interface ConditionExpression {
  field: string;
  operator: ConditionOperator;
  value: JsonValue;
  fieldType?: string; // 字段类型，用于UI渲染
}

export interface ConditionRule {
  field: string;
  operator: ConditionOperator;
  value: JsonValue;
}

// 结束节点
export interface EndNode extends TreeNodeBase {
  nodeType: 'end';
}

// 包容分支节点
export interface InclusiveNode extends TreeNodeBase {
  nodeType: 'inclusive';
  conditionNodes: ConditionBranch[];
  childNode?: TreeNode;
}

// 路由节点
export interface RouteNode extends TreeNodeBase {
  nodeType: 'route';
  routeTargetNodeId?: string; // 目标节点ID
  // 路由节点通常是终点，但也可以作为中间节点（如果只是经过）
  // 但在 FlowLong 中，路由节点是重定向，所以通常没有直接的 childNode，而是跳转到 target
}

// 子流程节点
export interface CallProcessNode extends TreeNodeBase {
  nodeType: 'callProcess';
  callProcessId?: string; // 子流程定义ID
  callAsync?: boolean; // 是否异步
  childNode?: TreeNode;
}

// 定时器节点
export interface TimerNode extends TreeNodeBase {
  nodeType: 'timer';
  timerConfig?: {
    type: 'duration' | 'date'; // 时长 或 具体时间
    duration?: number; // 秒
    date?: string; // ISO string
  };
  childNode?: TreeNode;
}

// 触发器节点
export interface TriggerNode extends TreeNodeBase {
  nodeType: 'trigger';
  triggerType?: 'immediate' | 'scheduled';
  triggerConfig?: any;
  childNode?: TreeNode;
}

export type TreeNode =
  | StartNode
  | ApproveNode
  | CopyNode
  | ConditionNode
  | DynamicConditionNode
  | ParallelConditionNode
  | ParallelNode
  | EndNode
  | InclusiveNode
  | RouteNode
  | CallProcessNode
  | TimerNode
  | TriggerNode;

// 完整流程定义
export interface ApprovalFlowTree {
  flowId?: string;
  flowName: string;
  rootNode: StartNode;
}
