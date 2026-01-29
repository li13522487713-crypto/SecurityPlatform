export type NodeType = 'start' | 'approve' | 'copy' | 'condition' | 'end';

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
  assigneeType: 0 | 1 | 2;  // 用户/角色/部门负责人
  assigneeValue: string;
  approvalMode: 'all' | 'any' | 'sequential';
  childNode?: TreeNode;
}

// 抄送节点
export interface CopyNode extends TreeNodeBase {
  nodeType: 'copy';
  copyToUsers: string[];
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

export interface ConditionBranch {
  id: string;
  branchName: string;
  conditionRule?: ConditionRule;
  childNode?: TreeNode;
  isDefault?: boolean;  // 默认分支（兜底）
}

export interface ConditionRule {
  field: string;
  operator: 'equals' | 'notEquals' | 'greaterThan' | 'lessThan' | 'greaterThanOrEqual' | 'lessThanOrEqual' | 'in' | 'contains' | 'startsWith' | 'endsWith';
  value: any;
}

// 结束节点
export interface EndNode extends TreeNodeBase {
  nodeType: 'end';
}

export type TreeNode = StartNode | ApproveNode | CopyNode | ConditionNode | EndNode;

// 完整流程定义
export interface ApprovalFlowTree {
  flowId?: string;
  flowName: string;
  rootNode: StartNode;
}
