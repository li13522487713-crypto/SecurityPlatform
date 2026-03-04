import type {
  TreeNode,
  ConditionNode,
  DynamicConditionNode,
  ParallelConditionNode,
  ParallelNode,
  ApproveNode,
  InclusiveNode,
  RouteNode,
  CallProcessNode,
  TimerNode
} from '@/types/approval-tree';

export interface ApprovalTreeValidationIssue {
  code: string;
  message: string;
  severity: 'error' | 'warning';
  nodeId?: string;
}

export class ApprovalTreeValidator {
  /**
   * 连线完整性检查
   */
  static checkCompleteness(rootNode: TreeNode): { valid: boolean; errors: string[]; issues: ApprovalTreeValidationIssue[] } {
    const issues: ApprovalTreeValidationIssue[] = [];
    const addError = (code: string, message: string, nodeId?: string) => {
      issues.push({ code, message, severity: 'error', nodeId });
    };
    const addWarning = (code: string, message: string, nodeId?: string) => {
      issues.push({ code, message, severity: 'warning', nodeId });
    };
    let hasApproveNode = false;
    const parallelGroupIds = new Set<string>();
    
    const checkNode = (node: TreeNode, path: string) => {
      // 检查节点本身配置
      if (node.nodeType === 'approve') {
        const n = node as ApproveNode;
        hasApproveNode = true;
        if (!n.assigneeValue && (!n.approverConfig || n.approverConfig.nodeApproveList.length === 0)) {
          addError('APPROVER_REQUIRED', `${path}: 审批人未配置`, n.id);
          n.error = true;
        } else {
          n.error = false;
        }
      }
      
      if (node.nodeType === 'condition' || node.nodeType === 'dynamicCondition' || node.nodeType === 'parallelCondition' || node.nodeType === 'inclusive') {
        const n = node as ConditionNode | DynamicConditionNode | ParallelConditionNode | InclusiveNode;
        if (n.conditionNodes.length < 2) {
          addError('CONDITION_BRANCH_MIN', `${path}: 分支节点至少需要2个分支`, n.id);
        }

        n.conditionNodes.forEach((branch, idx) => {
          const hasCondition =
            branch.isDefault ||
            (branch.conditionGroups !== undefined && branch.conditionGroups.length > 0) || // 新版条件组
            (branch.conditionRule !== undefined); // 旧版兼容
          if (!hasCondition) {
            addError('CONDITION_RULE_REQUIRED', `${path}.分支${idx + 1}: 条件规则未配置`, branch.id);
          }
          if (branch.childNode) {
            checkNode(branch.childNode, `${path}.分支${idx + 1}`);
          } else if (!branch.isDefault) {
            addWarning('CONDITION_BRANCH_EMPTY', `${path}.分支${idx + 1}: 分支未配置后续节点`, branch.id);
          }
        });
      }

      if (node.nodeType === 'parallel') {
        const n = node as ParallelNode;
        if (!n.groupId) {
          addWarning('PARALLEL_GROUP_ID_MISSING', `${path}: 并行块缺少 groupId 标识`, n.id);
        } else if (parallelGroupIds.has(n.groupId)) {
          addError('PARALLEL_GROUP_DUPLICATED', `${path}: 并行块 groupId 重复`, n.id);
        } else {
          parallelGroupIds.add(n.groupId);
        }

        if (n.parallelNodes.length < 2) {
          addError('PARALLEL_BRANCH_MIN', `${path}: 并行审批至少需要2个分支`, n.id);
        }
        n.parallelNodes.forEach((child, idx) => {
          checkNode(child, `${path}.并行分支${idx + 1}`);
        });
        if (!n.childNode) {
          addError('PARALLEL_JOIN_REQUIRED', `${path}: 并行审批需要聚合节点`, n.id);
        }
      }

      if (node.nodeType === 'route') {
        const n = node as RouteNode;
        if (!n.routeTargetNodeId) {
          addError('ROUTE_TARGET_REQUIRED', `${path}: 路由目标节点未配置`, n.id);
          n.error = true;
        } else {
          n.error = false;
        }
      }

      if (node.nodeType === 'callProcess') {
        const n = node as CallProcessNode;
        if (!n.callProcessId) {
          addError('CALL_PROCESS_REQUIRED', `${path}: 子流程未配置`, n.id);
          n.error = true;
        } else {
          n.error = false;
        }
      }

      if (node.nodeType === 'timer') {
        const n = node as TimerNode;
        if (!n.timerConfig || (n.timerConfig.type === 'duration' && !n.timerConfig.duration) || (n.timerConfig.type === 'date' && !n.timerConfig.date)) {
          addError('TIMER_CONFIG_INVALID', `${path}: 定时器配置无效`, n.id);
          n.error = true;
        } else {
          n.error = false;
        }
      }
      
      if ('childNode' in node && node.childNode) {
        checkNode(node.childNode, path);
      }
    };
    
    checkNode(rootNode, '根节点');
    if (!hasApproveNode) {
      addError('APPROVE_NODE_REQUIRED', '至少配置一个审批节点');
    }

    const errors = issues
      .filter((item) => item.severity === 'error')
      .map((item) => item.message);
    
    return {
      valid: errors.length === 0,
      errors,
      issues,
    };
  }
}
