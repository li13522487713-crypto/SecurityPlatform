import { ref } from 'vue';
import type {
  ApprovalFlowTree,
  TreeNode,
  ConditionNode,
  ConditionBranch,
  ApproveNode,
  CopyNode,
  StartNode,
  DynamicConditionNode,
  ParallelConditionNode,
  ParallelNode,
  InclusiveNode,
  RouteNode,
  CallProcessNode,
  TimerNode,
  TriggerNode
} from '@/types/approval-tree';
import { nanoid } from 'nanoid';
import { ApprovalTreeValidator } from '@/utils/approval-tree-validator';
import { useApprovalTreeHistory } from './useApprovalTreeHistory';

export function useApprovalTree() {
  const flowTree = ref<ApprovalFlowTree>({
    flowName: '',
    rootNode: {
      id: nanoid(),
      nodeType: 'start',
      nodeName: '发起人',
      childNode: {
        id: nanoid(),
        nodeType: 'end',
        nodeName: '结束'
      }
    }
  });
  
  const selectedNode = ref<TreeNode | ConditionBranch | null>(null);
  const { pushState, undo, redo, canUndo, canRedo } = useApprovalTreeHistory();
  
  // 类型守卫：检查节点是否有 childNode
  const hasChildNode = (
    node: TreeNode | ConditionBranch
  ): node is StartNode | ApproveNode | CopyNode | ConditionNode | DynamicConditionNode | ParallelConditionNode | ParallelNode | InclusiveNode | CallProcessNode | TimerNode | TriggerNode | ConditionBranch => {
    if (!('nodeType' in node)) {
      // ConditionBranch 总是有 childNode
      return true;
    }
    // TreeNode: 路由与结束节点不维护 childNode
    return node.nodeType !== 'end' && node.nodeType !== 'route';
  };
  
  // 辅助：递归查找节点或分支
  type FindResult = { node?: TreeNode; parent?: TreeNode | ConditionBranch; branch?: ConditionBranch };
  
  const findNodeOrBranch = (
    current: any, 
    targetId: string
  ): FindResult | null => {
    if (current.id === targetId) {
        return { node: current };
    }

    // 检查 childNode
    if ('childNode' in current && current.childNode) {
        if (current.childNode.id === targetId) {
            return { node: current.childNode, parent: current };
        }
        const found = findNodeOrBranch(current.childNode, targetId);
        if (found) return found;
    }

    // 检查 conditionNodes
    if (current.nodeType === 'condition' || current.nodeType === 'dynamicCondition' || current.nodeType === 'parallelCondition' || current.nodeType === 'inclusive') {
        const conditionNode = current as ConditionNode | DynamicConditionNode | ParallelConditionNode | InclusiveNode;
        for (const branch of conditionNode.conditionNodes) {
            if (branch.id === targetId) {
                return { branch, parent: conditionNode };
            }
            if (branch.childNode) {
                if (branch.childNode.id === targetId) {
                    return { node: branch.childNode, parent: branch };
                }
                const found = findNodeOrBranch(branch.childNode, targetId);
                if (found) return found;
            }
        }
    }

    // 检查 parallelNodes
    if (current.nodeType === 'parallel') {
        const parallelNode = current as ParallelNode;
        for (const branch of parallelNode.parallelNodes) {
            if (branch.id === targetId) {
                return { node: branch, parent: parallelNode };
            }
            const found = findNodeOrBranch(branch, targetId);
            if (found) return found;
        }
    }

    return null;
  };

  /**
   * 在指定节点后添加新节点
   * parentId: 可以是 TreeNode.id 或 ConditionBranch.id
   */
  const addNode = (parentId: string, newNodeType: string) => {
    const result = findNodeOrBranch(flowTree.value.rootNode, parentId);
    if (!result) {
        console.warn('Parent node not found', parentId);
        return;
    }

    const { node, branch } = result;
    const target = node || branch; // target 是父节点（TreeNode 或 ConditionBranch）

    if (!target) return;

    // 创建新节点
    const newNode = createNode(newNodeType);

    // 插入逻辑：
    // newNode.childNode = target.childNode
    // target.childNode = newNode
    
    if (hasChildNode(target) && hasChildNode(newNode)) {
        const targetWithChild = target as any; // 简化类型断言
        const newNodeWithChild = newNode as any;
        newNodeWithChild.childNode = targetWithChild.childNode;
        targetWithChild.childNode = newNode;
    }
    
    pushState(flowTree.value);
  };
  
  /**
   * 删除节点
   */
  const deleteNode = (nodeId: string) => {
    const result = findNodeOrBranch(flowTree.value.rootNode, nodeId);
    if (!result || !result.parent) {
        console.warn('Node not found or is root', nodeId);
        return;
    }

    const { node, parent } = result;
    if (!node) return;

    if (hasChildNode(parent)) {
        if (hasChildNode(node)) {
            parent.childNode = node.childNode;
        } else {
            parent.childNode = undefined;
        }
    }
    
    pushState(flowTree.value);
  };
  
  /**
   * 更新节点
   */
  const updateNode = (updatedNode: TreeNode | ConditionBranch) => {
    const result = findNodeOrBranch(flowTree.value.rootNode, updatedNode.id);
    if (!result) return;
    
    // 如果是节点
    if (result.node) {
        // 更新属性
        Object.assign(result.node, updatedNode);
    } 
    // 如果是分支
    else if (result.branch) {
        Object.assign(result.branch, updatedNode);
    }
    
    pushState(flowTree.value);
  };

  /**
   * 添加条件分支
   */
  const addConditionBranch = (conditionNodeId: string) => {
    const result = findNodeOrBranch(flowTree.value.rootNode, conditionNodeId);
    if (!result || !result.node || !['condition', 'inclusive'].includes(result.node.nodeType)) return;
    
    const conditionNode = result.node as ConditionNode | InclusiveNode;
    
    conditionNode.conditionNodes.push({
      id: nanoid(),
      branchName: `条件${conditionNode.conditionNodes.length + 1}`,
      conditionRule: undefined,
      childNode: undefined 
    });
    
    pushState(flowTree.value);
  };

  /**
   * 删除条件分支
   * 当删除后只剩 1 个分支时，自动将该分支的子链合并到父节点链上，
   * 并移除整个条件/包容节点（与 FlowLong 行为一致）。
   */
  const deleteConditionBranch = (branchId: string) => {
      const result = findNodeOrBranch(flowTree.value.rootNode, branchId);
      if (!result || !result.branch || !result.parent) return;

      const conditionNode = result.parent as ConditionNode | InclusiveNode;
      const index = conditionNode.conditionNodes.findIndex(b => b.id === branchId);
      if (index > -1) {
          conditionNode.conditionNodes.splice(index, 1);
      }

      // 如果只剩 1 个分支，将其子链提升合并到条件节点所在位置
      if (conditionNode.conditionNodes.length <= 1) {
          const survivingBranch = conditionNode.conditionNodes[0];
          // 找到条件节点的父节点
          const parentResult = findNodeOrBranch(flowTree.value.rootNode, conditionNode.id);
          // 条件节点自身在 parentResult.node 中，它的父在哪里？
          // 需要额外找一次：遍历寻找 "谁的 childNode === conditionNode"
          const condParent = findParentOf(flowTree.value.rootNode, conditionNode.id);
          if (condParent) {
              // 存活分支的子链
              const survivingChild = survivingBranch?.childNode;
              // 条件节点后续的 childNode
              const conditionTail = conditionNode.childNode;
              // 拼接：survivingChild → ... → conditionTail → conditionNode.childNode 后续
              if (survivingChild) {
                  // 找到 survivingChild 链的末尾
                  let tail: any = survivingChild;
                  while (tail.childNode) tail = tail.childNode;
                  tail.childNode = conditionTail;
                  (condParent as any).childNode = survivingChild;
              } else {
                  (condParent as any).childNode = conditionTail;
              }
          }
      }

      pushState(flowTree.value);
  };

  /**
   * 辅助：找到某个节点的父节点（谁的 childNode.id === targetId）
   */
  const findParentOf = (current: any, targetId: string): any | null => {
      if ('childNode' in current && current.childNode) {
          if (current.childNode.id === targetId) return current;
          const found = findParentOf(current.childNode, targetId);
          if (found) return found;
      }
      if (current.conditionNodes) {
          for (const branch of current.conditionNodes) {
              if (branch.childNode) {
                  if (branch.childNode.id === targetId) return branch;
                  const found = findParentOf(branch.childNode, targetId);
                  if (found) return found;
              }
          }
      }
      if (current.parallelNodes) {
          for (const pNode of current.parallelNodes) {
              if (pNode.id === targetId) return current;
              const found = findParentOf(pNode, targetId);
              if (found) return found;
          }
      }
      return null;
  };

  /**
   * 移动条件分支（左/右排序）
   * direction: 'left' 表示向前（index-1），'right' 表示向后（index+1）
   */
  const moveBranch = (conditionNodeId: string, branchId: string, direction: 'left' | 'right') => {
      const result = findNodeOrBranch(flowTree.value.rootNode, conditionNodeId);
      if (!result || !result.node) return;

      const conditionNode = result.node as ConditionNode | InclusiveNode;
      if (!conditionNode.conditionNodes) return;

      const branches = conditionNode.conditionNodes;
      const index = branches.findIndex(b => b.id === branchId);
      if (index < 0) return;

      const targetIndex = direction === 'left' ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= branches.length) return;

      // 交换
      [branches[index], branches[targetIndex]] = [branches[targetIndex], branches[index]];
      pushState(flowTree.value);
  };
  
  /**
   * 校验流程完整性
   */
  const validateFlow = (): { valid: boolean; errors: string[] } => {
    return ApprovalTreeValidator.checkCompleteness(flowTree.value.rootNode as any);
  };
  
  const createNode = (nodeType: string): TreeNode => {
    const base = {
        id: nanoid(),
        nodeName: '',
    };

    switch (nodeType) {
        case 'approve':
            return {
                ...base,
                nodeType: 'approve',
                nodeName: '审批人',
                assigneeType: 0,
                assigneeValue: '',
                approvalMode: 'all',
                approverConfig: {
                    setType: 0,
                    signType: 1,
                    noHeaderAction: 0,
                    nodeApproveList: []
                }
            } as ApproveNode;
        case 'copy':
            return {
                ...base,
                nodeType: 'copy',
                nodeName: '抄送人',
                copyToUsers: []
            } as CopyNode;
        case 'condition':
            return {
                ...base,
                nodeType: 'condition',
                nodeName: '条件分支',
                conditionNodes: [
                    { id: nanoid(), branchName: '条件1', childNode: undefined },
                    { id: nanoid(), branchName: '条件2', childNode: undefined }
                ]
            } as ConditionNode;
        case 'dynamicCondition':
            return {
                ...base,
                nodeType: 'dynamicCondition',
                nodeName: '动态条件',
                conditionNodes: [
                    { id: nanoid(), branchName: '动态条件1', childNode: undefined },
                    { id: nanoid(), branchName: '动态条件2', childNode: undefined }
                ]
            } as DynamicConditionNode;
        case 'parallelCondition':
            return {
                ...base,
                nodeType: 'parallelCondition',
                nodeName: '条件并行',
                conditionNodes: [
                    { id: nanoid(), branchName: '并行条件1', childNode: undefined },
                    { id: nanoid(), branchName: '并行条件2', childNode: undefined }
                ]
            } as ParallelConditionNode;
        case 'parallel':
            return {
                ...base,
                nodeType: 'parallel',
                nodeName: '并行审批',
                parallelNodes: [
                    {
                        id: nanoid(),
                        nodeType: 'approve',
                        nodeName: '并行审批人1',
                        assigneeType: 0,
                        assigneeValue: '',
                        approvalMode: 'all',
                        approverConfig: {
                            setType: 0,
                            signType: 1,
                            noHeaderAction: 0,
                            nodeApproveList: []
                        }
                    } as ApproveNode,
                    {
                        id: nanoid(),
                        nodeType: 'approve',
                        nodeName: '并行审批人2',
                        assigneeType: 0,
                        assigneeValue: '',
                        approvalMode: 'all',
                        approverConfig: {
                            setType: 0,
                            signType: 1,
                            noHeaderAction: 0,
                            nodeApproveList: []
                        }
                    } as ApproveNode
                ],
                childNode: {
                    id: nanoid(),
                    nodeType: 'approve',
                    nodeName: '并行聚合审批人',
                    assigneeType: 0,
                    assigneeValue: '',
                    approvalMode: 'all',
                    approverConfig: {
                        setType: 0,
                        signType: 1,
                        noHeaderAction: 0,
                        nodeApproveList: []
                    }
                } as ApproveNode
            } as ParallelNode;
        case 'inclusive':
            return {
                ...base,
                nodeType: 'inclusive',
                nodeName: '包容分支',
                conditionNodes: [
                    { id: nanoid(), branchName: '条件1', childNode: undefined },
                    { id: nanoid(), branchName: '条件2', childNode: undefined }
                ]
            } as InclusiveNode;
        case 'route':
            return {
                ...base,
                nodeType: 'route',
                nodeName: '路由节点',
                routeTargetNodeId: ''
            } as RouteNode;
        case 'callProcess':
            return {
                ...base,
                nodeType: 'callProcess',
                nodeName: '子流程',
                callProcessId: '',
                callAsync: false
            } as CallProcessNode;
        case 'timer':
            return {
                ...base,
                nodeType: 'timer',
                nodeName: '定时器',
                timerConfig: { type: 'duration', duration: 0 }
            } as TimerNode;
        case 'trigger':
            return {
                ...base,
                nodeType: 'trigger',
                nodeName: '触发器',
                triggerType: 'immediate',
                triggerConfig: {}
            } as TriggerNode;
        default:
            throw new Error(`Unknown node type: ${nodeType}`);
    }
  };
  
  const selectNode = (target: TreeNode | ConditionBranch | null) => {
      selectedNode.value = target;
  };
  
  const handleUndo = () => {
      const prev = undo();
      if (prev) flowTree.value = prev;
  };

  const handleRedo = () => {
      const next = redo();
      if (next) flowTree.value = next;
  };

  return {
    flowTree,
    selectedNode,
    addNode,
    deleteNode,
    updateNode,
    addConditionBranch,
    deleteConditionBranch,
    moveBranch,
    selectNode,
    validateFlow,
    undo: handleUndo,
    redo: handleRedo,
    canUndo,
    canRedo,
    pushState
  };
}
