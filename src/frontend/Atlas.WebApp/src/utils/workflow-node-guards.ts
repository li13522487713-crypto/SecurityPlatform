/**
 * 审批流节点类型守卫工具函数
 */
import type {
  TreeNode,
  ConditionBranch,
  ApproveNode,
  CopyNode,
  StartNode,
  EndNode,
  ConditionNode,
  DynamicConditionNode,
  ParallelConditionNode,
  ParallelNode,
  InclusiveNode,
  RouteNode,
  CallProcessNode,
  TimerNode,
  TriggerNode
} from "@/types/approval-tree";

export function isApproveNode(n: TreeNode | ConditionBranch): n is ApproveNode {
  return "nodeType" in n && n.nodeType === "approve";
}

export function isCopyNode(n: TreeNode | ConditionBranch): n is CopyNode {
  return "nodeType" in n && n.nodeType === "copy";
}

export function isStartNode(n: TreeNode | ConditionBranch): n is StartNode {
  return "nodeType" in n && n.nodeType === "start";
}

export function isEndNode(n: TreeNode | ConditionBranch): n is EndNode {
  return "nodeType" in n && n.nodeType === "end";
}

export function isConditionNode(n: TreeNode | ConditionBranch): n is ConditionNode {
  return "nodeType" in n && n.nodeType === "condition";
}

export function isDynamicConditionNode(n: TreeNode | ConditionBranch): n is DynamicConditionNode {
  return "nodeType" in n && n.nodeType === "dynamicCondition";
}

export function isParallelConditionNode(n: TreeNode | ConditionBranch): n is ParallelConditionNode {
  return "nodeType" in n && n.nodeType === "parallelCondition";
}

export function isParallelNode(n: TreeNode | ConditionBranch): n is ParallelNode {
  return "nodeType" in n && n.nodeType === "parallel";
}

export function isConditionBranch(n: TreeNode | ConditionBranch): n is ConditionBranch {
  return "branchName" in n;
}

export function isInclusiveNode(n: TreeNode | ConditionBranch): n is InclusiveNode {
  return "nodeType" in n && n.nodeType === "inclusive";
}

export function isRouteNode(n: TreeNode | ConditionBranch): n is RouteNode {
  return "nodeType" in n && n.nodeType === "route";
}

export function isCallProcessNode(n: TreeNode | ConditionBranch): n is CallProcessNode {
  return "nodeType" in n && n.nodeType === "callProcess";
}

export function isTimerNode(n: TreeNode | ConditionBranch): n is TimerNode {
  return "nodeType" in n && n.nodeType === "timer";
}

export function isTriggerNode(n: TreeNode | ConditionBranch): n is TriggerNode {
  return "nodeType" in n && n.nodeType === "trigger";
}
