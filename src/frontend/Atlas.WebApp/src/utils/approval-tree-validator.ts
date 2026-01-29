import type { TreeNode, ConditionNode } from '@/types/approval-tree';

export class ApprovalTreeValidator {
  /**
   * 连线完整性检查
   */
  static checkCompleteness(rootNode: TreeNode): { valid: boolean; errors: string[] } {
    const errors: string[] = [];
    
    const checkNode = (node: TreeNode, path: string) => {
      // 检查节点本身配置
      if (node.nodeType === 'approve') {
        const n = node as any;
        if (!n.assigneeValue) {
          errors.push(`${path}: 审批人未配置`);
          n.error = true;
        } else {
          n.error = false;
        }
      }
      
      if (node.nodeType === 'condition') {
        const n = node as ConditionNode;
        if (n.conditionNodes.length < 2) {
          errors.push(`${path}: 条件节点至少需要2个分支`);
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
      
      if ('childNode' in node && (node as any).childNode) {
        checkNode((node as any).childNode, path);
      }
    };
    
    checkNode(rootNode, '根节点');
    
    return {
      valid: errors.length === 0,
      errors
    };
  }
}
