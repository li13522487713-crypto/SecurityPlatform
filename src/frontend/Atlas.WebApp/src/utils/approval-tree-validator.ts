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

export class ApprovalTreeValidator {
  /**
   * 连线完整性检查
   */
  static checkCompleteness(rootNode: TreeNode): { valid: boolean; errors: string[] } {
    const errors: string[] = [];
    let hasApproveNode = false;
    
    const checkNode = (node: TreeNode, path: string) => {
      // 检查节点本身配置
      if (node.nodeType === 'approve') {
        const n = node as ApproveNode;
        hasApproveNode = true;
        if (!n.assigneeValue && (!n.approverConfig || n.approverConfig.nodeApproveList.length === 0)) {
          errors.push(`${path}: 审批人未配置`);
          n.error = true;
        } else {
          n.error = false;
        }
      }
      
      if (node.nodeType === 'condition' || node.nodeType === 'dynamicCondition' || node.nodeType === 'parallelCondition' || node.nodeType === 'inclusive') {
        const n = node as ConditionNode | DynamicConditionNode | ParallelConditionNode | InclusiveNode;
        if (n.conditionNodes.length < 2) {
          errors.push(`${path}: 分支节点至少需要2个分支`);
        }

        n.conditionNodes.forEach((branch, idx) => {
          if (!branch.conditionRule && !branch.isDefault) {
            errors.push(`${path}.分支${idx + 1}: 条件规则未配置`);
          }
          if (branch.childNode) {
            checkNode(branch.childNode, `${path}.分支${idx + 1}`);
          }
        });
      }

      if (node.nodeType === 'parallel') {
        const n = node as ParallelNode;
        if (n.parallelNodes.length < 2) {
          errors.push(`${path}: 并行审批至少需要2个分支`);
        }
        n.parallelNodes.forEach((child, idx) => {
          checkNode(child, `${path}.并行分支${idx + 1}`);
        });
        if (!n.childNode) {
          errors.push(`${path}: 并行审批需要聚合节点`);
        }
      }

      if (node.nodeType === 'route') {
        const n = node as RouteNode;
        if (!n.routeTargetNodeId) {
          errors.push(`${path}: 路由目标节点未配置`);
          n.error = true;
        } else {
          n.error = false;
        }
      }

      if (node.nodeType === 'callProcess') {
        const n = node as CallProcessNode;
        if (!n.callProcessId) {
          errors.push(`${path}: 子流程未配置`);
          n.error = true;
        } else {
          n.error = false;
        }
      }

      if (node.nodeType === 'timer') {
        const n = node as TimerNode;
        if (!n.timerConfig || (n.timerConfig.type === 'duration' && !n.timerConfig.duration) || (n.timerConfig.type === 'date' && !n.timerConfig.date)) {
          errors.push(`${path}: 定时器配置无效`);
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
      errors.push('至少配置一个审批节点');
    }
    
    return {
      valid: errors.length === 0,
      errors
    };
  }
}
