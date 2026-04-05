import { computed, ref, shallowRef } from 'vue';
import type { ApprovalFlowTree, ConditionBranch, NodeType, TreeNode } from '@/types/approval-tree';

type ApprovalTreeValidationIssue = {
  code: string;
  message: string;
  severity: 'error' | 'warning';
  nodeId?: string;
};

let idSeed = 0;
const nextId = (prefix: string) => `${prefix}_${Date.now().toString(36)}_${(idSeed++).toString(36)}`;

const cloneTree = (value: ApprovalFlowTree): ApprovalFlowTree =>
  JSON.parse(JSON.stringify(value)) as ApprovalFlowTree;
const cloneNode = (value: TreeNode): TreeNode => JSON.parse(JSON.stringify(value)) as TreeNode;
const cloneBranch = (value: ConditionBranch): ConditionBranch =>
  JSON.parse(JSON.stringify(value)) as ConditionBranch;
const isTreeNode = (value: TreeNode | ConditionBranch): value is TreeNode =>
  Object.prototype.hasOwnProperty.call(value, 'nodeType');

const createBranch = (index: number, isDefault = false): ConditionBranch => ({
  id: nextId('branch'),
  branchName: isDefault ? '默认分支' : `条件分支${index}`,
  isDefault,
});

const createNode = (nodeType: NodeType): TreeNode => {
  const id = nextId('node');
  switch (nodeType) {
    case 'start':
      return { id, nodeType: 'start', nodeName: '发起人' };
    case 'approve':
      return {
        id,
        nodeType: 'approve',
        nodeName: '审批节点',
        assigneeType: 0,
        assigneeValue: '',
        approvalMode: 'any',
      };
    case 'copy':
      return { id, nodeType: 'copy', nodeName: '抄送节点', copyToUsers: [] };
    case 'condition':
      return {
        id,
        nodeType: 'condition',
        nodeName: '条件分支',
        conditionNodes: [createBranch(1), createBranch(2, true)],
      };
    case 'dynamicCondition':
      return {
        id,
        nodeType: 'dynamicCondition',
        nodeName: '动态条件',
        conditionNodes: [createBranch(1), createBranch(2, true)],
      };
    case 'parallelCondition':
      return {
        id,
        nodeType: 'parallelCondition',
        nodeName: '并行条件',
        conditionNodes: [createBranch(1), createBranch(2, true)],
      };
    case 'parallel':
      return {
        id,
        nodeType: 'parallel',
        nodeName: '并行分支',
        parallelNodes: [],
      };
    case 'inclusive':
      return {
        id,
        nodeType: 'inclusive',
        nodeName: '包容分支',
        conditionNodes: [createBranch(1), createBranch(2, true)],
      };
    case 'route':
      return { id, nodeType: 'route', nodeName: '路由节点' };
    case 'callProcess':
      return { id, nodeType: 'callProcess', nodeName: '子流程', callAsync: false };
    case 'timer':
      return {
        id,
        nodeType: 'timer',
        nodeName: '定时节点',
        timerConfig: { type: 'duration', duration: 3600 },
      };
    case 'trigger':
      return {
        id,
        nodeType: 'trigger',
        nodeName: '触发器',
        triggerType: 'immediate',
        triggerConfig: {},
      };
    case 'end':
    default:
      return { id, nodeType: 'end', nodeName: '结束' };
  }
};

const createDefaultTree = (): ApprovalFlowTree => ({
  flowName: '新建审批流',
  rootNode: {
    id: nextId('node'),
    nodeType: 'start',
    nodeName: '发起人',
    childNode: {
      id: nextId('node'),
      nodeType: 'end',
      nodeName: '结束',
    },
  },
});

const getChildNode = (node: TreeNode): TreeNode | undefined => {
  switch (node.nodeType) {
    case 'start':
    case 'approve':
    case 'copy':
    case 'condition':
    case 'dynamicCondition':
    case 'parallelCondition':
    case 'parallel':
    case 'inclusive':
    case 'callProcess':
    case 'timer':
    case 'trigger':
      return node.childNode;
    default:
      return undefined;
  }
};

const setChildNode = (node: TreeNode, childNode: TreeNode | undefined): boolean => {
  switch (node.nodeType) {
    case 'start':
    case 'approve':
    case 'copy':
    case 'condition':
    case 'dynamicCondition':
    case 'parallelCondition':
    case 'parallel':
    case 'inclusive':
    case 'callProcess':
    case 'timer':
    case 'trigger':
      node.childNode = childNode;
      return true;
    default:
      return false;
  }
};

const traverseNodes = (node: TreeNode, visitor: (current: TreeNode) => boolean): boolean => {
  if (visitor(node)) {
    return true;
  }

  if (
    (node.nodeType === 'condition' ||
      node.nodeType === 'dynamicCondition' ||
      node.nodeType === 'parallelCondition' ||
      node.nodeType === 'inclusive') &&
    node.conditionNodes.length
  ) {
    for (const branch of node.conditionNodes) {
      if (branch.childNode && traverseNodes(branch.childNode, visitor)) {
        return true;
      }
    }
  }

  if (node.nodeType === 'parallel' && node.parallelNodes.length) {
    for (const parallelNode of node.parallelNodes) {
      if (traverseNodes(parallelNode, visitor)) {
        return true;
      }
    }
  }

  const child = getChildNode(node);
  return child ? traverseNodes(child, visitor) : false;
};

const findNodeById = (root: TreeNode, nodeId: string): TreeNode | null => {
  let result: TreeNode | null = null;
  traverseNodes(root, (current) => {
    if (current.id === nodeId) {
      result = current;
      return true;
    }
    return false;
  });
  return result;
};

const findBranchById = (
  root: TreeNode,
  branchId: string,
): { parentNode: TreeNode; branch: ConditionBranch; index: number } | null => {
  let result: { parentNode: TreeNode; branch: ConditionBranch; index: number } | null = null;
  traverseNodes(root, (current) => {
    if (
      current.nodeType === 'condition' ||
      current.nodeType === 'dynamicCondition' ||
      current.nodeType === 'parallelCondition' ||
      current.nodeType === 'inclusive'
    ) {
      const index = current.conditionNodes.findIndex((branch) => branch.id === branchId);
      if (index >= 0) {
        result = { parentNode: current, branch: current.conditionNodes[index], index };
        return true;
      }
    }
    return false;
  });
  return result;
};

export function useApprovalTree() {
  const treeRef = shallowRef<ApprovalFlowTree>(createDefaultTree());
  const selectedIdRef = ref<string | null>(null);

  const historyRef = shallowRef<ApprovalFlowTree[]>([]);
  const historyIndexRef = ref(-1);

  const pushSnapshot = (snapshot?: ApprovalFlowTree) => {
    const source = snapshot ?? treeRef.value;
    const cloned = cloneTree(source);
    historyRef.value = historyRef.value.slice(0, historyIndexRef.value + 1);
    historyRef.value.push(cloned);
    historyIndexRef.value = historyRef.value.length - 1;
  };

  const applyTreeMutation = (mutator: (tree: ApprovalFlowTree) => void) => {
    const nextTree = cloneTree(treeRef.value);
    mutator(nextTree);
    treeRef.value = nextTree;
    pushSnapshot(nextTree);
  };

  const selectedNode = computed<TreeNode | ConditionBranch | null>({
    get: () => {
      const selectedId = selectedIdRef.value;
      if (!selectedId) {
        return null;
      }
      const treeNode = findNodeById(treeRef.value.rootNode, selectedId);
      if (treeNode) {
        return treeNode;
      }
      const branchRef = findBranchById(treeRef.value.rootNode, selectedId);
      return branchRef ? branchRef.branch : null;
    },
    set: (value) => {
      selectedIdRef.value = value?.id ?? null;
    },
  });

  const flowTree = treeRef;

  const canUndo = computed(() => historyIndexRef.value > 0);
  const canRedo = computed(() => historyIndexRef.value < historyRef.value.length - 1);

  const addNode = (parentId: string, newNodeType: string) => {
    const normalizedType = (newNodeType as NodeType) || 'approve';
    applyTreeMutation((tree) => {
      const newNode = createNode(normalizedType);
      const targetNode = findNodeById(tree.rootNode, parentId);
      if (targetNode) {
        const currentChild = getChildNode(targetNode);
        setChildNode(newNode, currentChild);
        if (!setChildNode(targetNode, newNode)) {
          return;
        }
        selectedIdRef.value = newNode.id;
        return;
      }

      const branchRef = findBranchById(tree.rootNode, parentId);
      if (!branchRef) {
        return;
      }
      const branchChild = branchRef.branch.childNode;
      setChildNode(newNode, branchChild);
      branchRef.branch.childNode = newNode;
      selectedIdRef.value = newNode.id;
    });
  };

  const deleteNode = (nodeId: string) => {
    if (treeRef.value.rootNode.id === nodeId) {
      return;
    }

    applyTreeMutation((tree) => {
      const removeRecursively = (current: TreeNode): boolean => {
        const directChild = getChildNode(current);
        if (directChild && directChild.id === nodeId) {
          const replacement = getChildNode(directChild);
          setChildNode(current, replacement);
          return true;
        }

        if (
          current.nodeType === 'condition' ||
          current.nodeType === 'dynamicCondition' ||
          current.nodeType === 'parallelCondition' ||
          current.nodeType === 'inclusive'
        ) {
          for (const branch of current.conditionNodes) {
            if (branch.childNode?.id === nodeId) {
              branch.childNode = getChildNode(branch.childNode);
              return true;
            }
            if (branch.childNode && removeRecursively(branch.childNode)) {
              return true;
            }
          }
        }

        if (current.nodeType === 'parallel') {
          const targetIndex = current.parallelNodes.findIndex((item) => item.id === nodeId);
          if (targetIndex >= 0) {
            current.parallelNodes.splice(targetIndex, 1);
            return true;
          }
          for (const parallelNode of current.parallelNodes) {
            if (removeRecursively(parallelNode)) {
              return true;
            }
          }
        }

        return directChild ? removeRecursively(directChild) : false;
      };

      if (removeRecursively(tree.rootNode) && selectedIdRef.value === nodeId) {
        selectedIdRef.value = null;
      }
    });
  };

  const updateNode = (updatedNode: TreeNode | ConditionBranch) => {
    applyTreeMutation((tree) => {
      if (isTreeNode(updatedNode)) {
        const replaceNode = (current: TreeNode): TreeNode => {
          if (current.id === updatedNode.id) {
            return cloneNode(updatedNode);
          }

          const next = cloneNode(current);
          if (
            (next.nodeType === 'condition' ||
              next.nodeType === 'dynamicCondition' ||
              next.nodeType === 'parallelCondition' ||
              next.nodeType === 'inclusive') &&
            next.conditionNodes.length
          ) {
            next.conditionNodes = next.conditionNodes.map((branch) => ({
              ...branch,
              childNode: branch.childNode ? replaceNode(branch.childNode) : undefined,
            }));
          }
          if (next.nodeType === 'parallel' && next.parallelNodes.length) {
            next.parallelNodes = next.parallelNodes.map((parallelNode) => replaceNode(parallelNode));
          }
          const child = getChildNode(next);
          if (child) {
            setChildNode(next, replaceNode(child));
          }
          return next;
        };

        tree.rootNode = replaceNode(tree.rootNode) as typeof tree.rootNode;
        selectedIdRef.value = updatedNode.id;
        return;
      }

      const replaceBranch = (current: TreeNode): TreeNode => {
        const next = cloneNode(current);
        if (
          (next.nodeType === 'condition' ||
            next.nodeType === 'dynamicCondition' ||
            next.nodeType === 'parallelCondition' ||
            next.nodeType === 'inclusive') &&
          next.conditionNodes.length
        ) {
          next.conditionNodes = next.conditionNodes.map((branch) => {
            if (branch.id === updatedNode.id) {
              return {
                ...cloneBranch(updatedNode),
                childNode: updatedNode.childNode ?? branch.childNode,
              };
            }
            return {
              ...branch,
              childNode: branch.childNode ? replaceBranch(branch.childNode) : undefined,
            };
          });
        }

        if (next.nodeType === 'parallel' && next.parallelNodes.length) {
          next.parallelNodes = next.parallelNodes.map((parallelNode) => replaceBranch(parallelNode));
        }

        const child = getChildNode(next);
        if (child) {
          setChildNode(next, replaceBranch(child));
        }
        return next;
      };

      tree.rootNode = replaceBranch(tree.rootNode) as typeof tree.rootNode;
      selectedIdRef.value = updatedNode.id;
    });
  };

  const addConditionBranch = (conditionNodeId: string) => {
    applyTreeMutation((tree) => {
      const target = findNodeById(tree.rootNode, conditionNodeId);
      if (
        !target ||
        (target.nodeType !== 'condition' &&
          target.nodeType !== 'dynamicCondition' &&
          target.nodeType !== 'parallelCondition' &&
          target.nodeType !== 'inclusive')
      ) {
        return;
      }
      const nextIndex = target.conditionNodes.length + 1;
      const branch = createBranch(nextIndex);
      target.conditionNodes.push(branch);
      selectedIdRef.value = branch.id;
    });
  };

  const deleteConditionBranch = (branchId: string) => {
    applyTreeMutation((tree) => {
      const branchRef = findBranchById(tree.rootNode, branchId);
      if (!branchRef) {
        return;
      }
      if (
        branchRef.parentNode.nodeType !== 'condition' &&
        branchRef.parentNode.nodeType !== 'dynamicCondition' &&
        branchRef.parentNode.nodeType !== 'parallelCondition' &&
        branchRef.parentNode.nodeType !== 'inclusive'
      ) {
        return;
      }

      if (branchRef.parentNode.conditionNodes.length <= 1) {
        return;
      }
      branchRef.parentNode.conditionNodes.splice(branchRef.index, 1);
      if (selectedIdRef.value === branchId) {
        selectedIdRef.value = branchRef.parentNode.id;
      }
    });
  };

  const moveBranch = (conditionNodeId: string, branchId: string, direction: 'left' | 'right') => {
    applyTreeMutation((tree) => {
      const target = findNodeById(tree.rootNode, conditionNodeId);
      if (
        !target ||
        (target.nodeType !== 'condition' &&
          target.nodeType !== 'dynamicCondition' &&
          target.nodeType !== 'parallelCondition' &&
          target.nodeType !== 'inclusive')
      ) {
        return;
      }

      const index = target.conditionNodes.findIndex((branch) => branch.id === branchId);
      if (index < 0) {
        return;
      }
      const offset = direction === 'left' ? -1 : 1;
      const swapIndex = index + offset;
      if (swapIndex < 0 || swapIndex >= target.conditionNodes.length) {
        return;
      }
      const current = target.conditionNodes[index];
      target.conditionNodes[index] = target.conditionNodes[swapIndex];
      target.conditionNodes[swapIndex] = current;
    });
  };

  const selectNode = (target: TreeNode | ConditionBranch | null) => {
    selectedIdRef.value = target?.id ?? null;
  };

  const validateFlow = (): { valid: boolean; errors: string[]; issues: ApprovalTreeValidationIssue[] } => {
    const errors: string[] = [];
    const issues: ApprovalTreeValidationIssue[] = [];

    const seen = new Set<string>();
    const validateNode = (node: TreeNode) => {
      if (seen.has(node.id)) {
        return;
      }
      seen.add(node.id);

      if (!node.nodeName?.trim()) {
        errors.push(`节点名称不能为空: ${node.id}`);
        issues.push({
          code: 'NODE_NAME_REQUIRED',
          message: '节点名称不能为空',
          severity: 'error',
          nodeId: node.id,
        });
      }

      if (node.nodeType !== 'end' && node.nodeType !== 'route') {
        const child = getChildNode(node);
        if (!child && node.nodeType !== 'condition' && node.nodeType !== 'dynamicCondition' && node.nodeType !== 'parallelCondition' && node.nodeType !== 'inclusive' && node.nodeType !== 'parallel') {
          issues.push({
            code: 'NEXT_NODE_MISSING',
            message: '该节点缺少后续节点',
            severity: 'warning',
            nodeId: node.id,
          });
        }
      }

      if (
        node.nodeType === 'condition' ||
        node.nodeType === 'dynamicCondition' ||
        node.nodeType === 'parallelCondition' ||
        node.nodeType === 'inclusive'
      ) {
        if (node.conditionNodes.length === 0) {
          errors.push(`条件分支不能为空: ${node.id}`);
          issues.push({
            code: 'BRANCH_MISSING',
            message: '条件节点至少需要一个分支',
            severity: 'error',
            nodeId: node.id,
          });
        }
        node.conditionNodes.forEach((branch) => {
          if (!branch.childNode) {
            issues.push({
              code: 'BRANCH_CHILD_MISSING',
              message: `分支“${branch.branchName}”未配置后续节点`,
              severity: 'warning',
              nodeId: node.id,
            });
          }
        });
      }

      if (node.nodeType === 'parallel' && node.parallelNodes.length === 0) {
        issues.push({
          code: 'PARALLEL_EMPTY',
          message: '并行节点尚未配置子分支',
          severity: 'warning',
          nodeId: node.id,
        });
      }
    };

    traverseNodes(treeRef.value.rootNode, (current) => {
      validateNode(current);
      return false;
    });

    return {
      valid: !issues.some((issue) => issue.severity === 'error'),
      errors,
      issues,
    };
  };

  const undo = () => {
    if (!canUndo.value) {
      return;
    }
    historyIndexRef.value -= 1;
    treeRef.value = cloneTree(historyRef.value[historyIndexRef.value]);
  };

  const redo = () => {
    if (!canRedo.value) {
      return;
    }
    historyIndexRef.value += 1;
    treeRef.value = cloneTree(historyRef.value[historyIndexRef.value]);
  };

  const pushState = (state?: ApprovalFlowTree) => {
    pushSnapshot(state);
  };

  if (historyRef.value.length === 0) {
    pushSnapshot();
  }

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
    undo,
    redo,
    canUndo,
    canRedo,
    pushState,
  };
}
