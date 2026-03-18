/**
 * Pinia Store: 审批流设计器状态管理
 *
 * 作为审批流设计器的 Single Source of Truth，管理流程树、选中节点、
 * 校验错误、撤销/重做历史等状态。
 *
 * 架构原则：
 * - 所有用户操作（添加/删除/修改节点）必须通过 Store Action 修改状态树
 * - X6 画布仅作为渲染引擎，不持有业务状态
 * - 属性面板通过 Store 读写节点数据
 */
import { defineStore } from 'pinia';
import { nanoid } from 'nanoid';
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
  TriggerNode,
} from '@/types/approval-tree';
import { ApprovalTreeValidator } from '@/utils/approval-tree-validator';
import type { ApprovalTreeValidationIssue } from '@/utils/approval-tree-validator';

// ── 常量 ──
const MAX_HISTORY = 50;

// ── 类型 ──
type NodeOrBranch = TreeNode | ConditionBranch;
interface FindResult {
  node?: TreeNode;
  parent?: NodeOrBranch;
  branch?: ConditionBranch;
}

// ── 审批人类型标签映射 ──
const ASSIGNEE_TYPE_LABELS: Record<number, string> = {
  0: '指定人员',
  1: '指定角色',
  2: '部门负责人',
  3: '逐级领导',
  4: '指定层级',
  5: '直属领导',
  6: '发起人',
  7: 'HRBP',
  8: '发起人自选',
  9: '业务字段取人',
  10: '外部传入人员',
};

// ── 审批方式标签映射 ──
const APPROVAL_MODE_LABELS: Record<string, string> = {
  all: '会签',
  any: '或签',
  sequential: '顺序签',
  vote: '票签',
};

// ── 辅助函数（纯函数，不依赖 Store 实例）──

function createDefaultTree(): ApprovalFlowTree {
  return {
    flowName: '',
    rootNode: {
      id: nanoid(),
      nodeType: 'start',
      nodeName: '发起人',
      childNode: {
        id: nanoid(),
        nodeType: 'end',
        nodeName: '结束',
      },
    },
  };
}

function deepClone<T>(obj: T): T {
  return JSON.parse(JSON.stringify(obj));
}

/** 类型守卫：检查节点是否可包含 childNode */
function hasChildNode(
  node: NodeOrBranch
): node is
  | StartNode
  | ApproveNode
  | CopyNode
  | ConditionNode
  | DynamicConditionNode
  | ParallelConditionNode
  | ParallelNode
  | InclusiveNode
  | CallProcessNode
  | TimerNode
  | TriggerNode
  | ConditionBranch {
  if (!('nodeType' in node)) {
    // ConditionBranch 总是有 childNode
    return true;
  }
  return node.nodeType !== 'end' && node.nodeType !== 'route';
}

/** 递归查找节点或分支 */
function findNodeOrBranch(current: NodeOrBranch, targetId: string): FindResult | null {
  if (current.id === targetId) {
    if ('nodeType' in current) {
      return { node: current };
    }
    return { branch: current };
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
  if (
    'nodeType' in current &&
    (current.nodeType === 'condition' ||
      current.nodeType === 'dynamicCondition' ||
      current.nodeType === 'parallelCondition' ||
      current.nodeType === 'inclusive')
  ) {
    const conditionNode = current as
      | ConditionNode
      | DynamicConditionNode
      | ParallelConditionNode
      | InclusiveNode;
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
  if ('nodeType' in current && current.nodeType === 'parallel') {
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
}

/** 找到某个节点的父节点 */
function findParentOf(current: NodeOrBranch, targetId: string): NodeOrBranch | null {
  if ('childNode' in current && current.childNode) {
    if (current.childNode.id === targetId) return current;
    const found = findParentOf(current.childNode, targetId);
    if (found) return found;
  }
  if ('conditionNodes' in current && current.conditionNodes) {
    for (const branch of current.conditionNodes) {
      if (branch.childNode) {
        if (branch.childNode.id === targetId) return branch;
        const found = findParentOf(branch.childNode, targetId);
        if (found) return found;
      }
    }
  }
  if ('parallelNodes' in current && current.parallelNodes) {
    for (const pNode of current.parallelNodes) {
      if (pNode.id === targetId) return current;
      const found = findParentOf(pNode, targetId);
      if (found) return found;
    }
  }
  return null;
}

/** 计算审批节点展示标签 */
function computeAssigneeLabel(node: ApproveNode): string {
  const typeLabel = ASSIGNEE_TYPE_LABELS[node.assigneeType] ?? '未知类型';

  if (!node.assigneeValue) {
    return `${typeLabel}（未设置）`;
  }

  // 对于用户/角色类型，展示值的数量
  if (node.assigneeType === 0 || node.assigneeType === 1) {
    const values = node.assigneeValue.split(',').filter(Boolean);
    return `${typeLabel}（${values.length}人）`;
  }

  // 对于层级类型
  if (node.assigneeType === 2 || node.assigneeType === 5) {
    return `${typeLabel}（${node.assigneeValue}级）`;
  }

  return typeLabel;
}

/** 计算条件分支摘要 */
function computeConditionSummary(branch: ConditionBranch): string {
  if (branch.isDefault) {
    return '默认条件（兜底）';
  }

  // 优先使用新版 conditionGroups
  if (branch.conditionGroups && branch.conditionGroups.length > 0) {
    const groupSummaries = branch.conditionGroups.map((group) => {
      const parts = group.conditions.map(
        (c) => `${c.field} ${c.operator} ${String(c.value ?? '')}`
      );
      return parts.join(' 且 ');
    });
    return groupSummaries.join(' 或 ');
  }

  // 兼容旧版 conditionRule
  if (branch.conditionRule) {
    return `${branch.conditionRule.field} ${branch.conditionRule.operator} ${String(branch.conditionRule.value ?? '')}`;
  }

  return '未设置条件';
}

/** 创建新节点 */
function createNode(nodeType: string): TreeNode {
  const base = { id: nanoid(), nodeName: '' };

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
          nodeApproveList: [],
        },
      } as ApproveNode;

    case 'copy':
      return {
        ...base,
        nodeType: 'copy',
        nodeName: '抄送人',
        copyToUsers: [],
      } as CopyNode;

    case 'condition':
      return {
        ...base,
        nodeType: 'condition',
        nodeName: '条件分支',
        conditionNodes: [
          { id: nanoid(), branchName: '条件1', childNode: undefined },
          { id: nanoid(), branchName: '条件2', childNode: undefined },
        ],
      } as ConditionNode;

    case 'dynamicCondition':
      return {
        ...base,
        nodeType: 'dynamicCondition',
        nodeName: '动态条件',
        conditionNodes: [
          { id: nanoid(), branchName: '动态条件1', childNode: undefined },
          { id: nanoid(), branchName: '动态条件2', childNode: undefined },
        ],
      } as DynamicConditionNode;

    case 'parallelCondition':
      return {
        ...base,
        nodeType: 'parallelCondition',
        nodeName: '条件并行',
        conditionNodes: [
          { id: nanoid(), branchName: '并行条件1', childNode: undefined },
          { id: nanoid(), branchName: '并行条件2', childNode: undefined },
        ],
      } as ParallelConditionNode;

    case 'parallel': {
      const groupId = nanoid();
      return {
        ...base,
        nodeType: 'parallel',
        nodeName: '并行审批',
        groupId,
        parallelNodes: [
          {
            id: nanoid(),
            nodeType: 'approve',
            nodeName: '并行审批人1',
            assigneeType: 0,
            assigneeValue: '',
            approvalMode: 'all',
            approverConfig: { setType: 0, signType: 1, noHeaderAction: 0, nodeApproveList: [] },
          } as ApproveNode,
          {
            id: nanoid(),
            nodeType: 'approve',
            nodeName: '并行审批人2',
            assigneeType: 0,
            assigneeValue: '',
            approvalMode: 'all',
            approverConfig: { setType: 0, signType: 1, noHeaderAction: 0, nodeApproveList: [] },
          } as ApproveNode,
        ],
        childNode: {
          id: nanoid(),
          nodeType: 'approve',
          nodeName: '并行聚合审批人',
          assigneeType: 0,
          assigneeValue: '',
          approvalMode: 'all',
          approverConfig: { setType: 0, signType: 1, noHeaderAction: 0, nodeApproveList: [] },
        } as ApproveNode,
      } as ParallelNode;
    }

    case 'inclusive':
      return {
        ...base,
        nodeType: 'inclusive',
        nodeName: '包容分支',
        conditionNodes: [
          { id: nanoid(), branchName: '条件1', childNode: undefined },
          { id: nanoid(), branchName: '条件2', childNode: undefined },
        ],
      } as InclusiveNode;

    case 'route':
      return {
        ...base,
        nodeType: 'route',
        nodeName: '路由节点',
        routeTargetNodeId: '',
      } as RouteNode;

    case 'callProcess':
      return {
        ...base,
        nodeType: 'callProcess',
        nodeName: '子流程',
        callProcessId: '',
        callAsync: false,
      } as CallProcessNode;

    case 'timer':
      return {
        ...base,
        nodeType: 'timer',
        nodeName: '定时器',
        timerConfig: { type: 'duration', duration: 0 },
      } as TimerNode;

    case 'trigger':
      return {
        ...base,
        nodeType: 'trigger',
        nodeName: '触发器',
        triggerType: 'immediate',
        triggerConfig: {},
      } as TriggerNode;

    default:
      throw new Error(`Unknown node type: ${nodeType}`);
  }
}

// ── Store 定义 ──

// NOTE: Use `any` for flowTree in state interface to avoid "Type instantiation is
// excessively deep" error caused by the deeply recursive TreeNode union type.
// All public getters and actions still use properly typed signatures.
interface ApprovalFlowState {
  /** 流程树（唯一事实来源） */
  flowTree: any; // runtime: ApprovalFlowTree
  /** 当前选中的节点或分支 */
  selectedNodeId: string | null;
  /** 校验错误映射：nodeId → 错误消息列表 */
  validationErrors: Record<string, string[]>;
  /** 是否有未保存的修改 */
  isDirty: boolean;
  /** 撤销/重做历史栈 */
  _history: string[];
  /** 当前历史索引 */
  _historyIndex: number;
  /** 是否正在进行撤销/重做操作（防止重复推栈） */
  _isUndoRedoing: boolean;
}

export const useApprovalFlowStore = defineStore('approvalFlow', {
  state: (): ApprovalFlowState => ({
    flowTree: createDefaultTree(),
    selectedNodeId: null,
    validationErrors: {},
    isDirty: false,
    _history: [],
    _historyIndex: -1,
    _isUndoRedoing: false,
  }),

  getters: {
    /** 获取当前选中的节点或分支 */
    selectedNode(): TreeNode | ConditionBranch | null {
      if (!this.selectedNodeId) return null;
      const result = findNodeOrBranch(
        this.flowTree.rootNode as unknown as TreeNode,
        this.selectedNodeId
      );
      if (!result) return null;
      return result.node ?? result.branch ?? null;
    },

    /** 是否可撤销 */
    canUndo(): boolean {
      return this._historyIndex > 0;
    },

    /** 是否可重做 */
    canRedo(): boolean {
      return this._historyIndex < this._history.length - 1;
    },

    /** 获取节点的展示标签（用于 X6 Shape 渲染） */
    nodeDisplayLabels(): Record<string, string> {
      const labels: Record<string, string> = {};
      const walk = (node: NodeOrBranch) => {
        if ('nodeType' in node && node.nodeType === 'approve') {
          labels[node.id] = computeAssigneeLabel(node as ApproveNode);
        }
        if (!('nodeType' in node)) {
          // ConditionBranch
          labels[node.id] = computeConditionSummary(node as ConditionBranch);
        }
        if ('childNode' in node && node.childNode) {
          walk(node.childNode);
        }
        if (
          'conditionNodes' in node &&
          node.conditionNodes
        ) {
          for (const branch of node.conditionNodes) {
            labels[branch.id] = computeConditionSummary(branch);
            if (branch.childNode) walk(branch.childNode);
          }
        }
        if ('parallelNodes' in node && node.parallelNodes) {
          for (const pNode of node.parallelNodes) {
            walk(pNode);
          }
        }
      };
      walk(this.flowTree.rootNode as unknown as TreeNode);
      return labels;
    },

    /** 审批方式标签 */
    approvalModeLabel(): (mode: string) => string {
      return (mode: string) => APPROVAL_MODE_LABELS[mode] ?? mode;
    },

    /** 审批人类型标签 */
    assigneeTypeLabel(): (type: number) => string {
      return (type: number) => ASSIGNEE_TYPE_LABELS[type] ?? '未知';
    },
  },

  actions: {
    // ── 历史管理 ──

    /** 推送当前状态到历史栈 */
    _pushHistory() {
      if (this._isUndoRedoing) return;

      // 截断当前索引之后的历史
      if (this._historyIndex < this._history.length - 1) {
        this._history = this._history.slice(0, this._historyIndex + 1);
      }

      const snapshot = JSON.stringify(this.flowTree);
      this._history.push(snapshot);
      this._historyIndex++;

      // 限制历史数量
      if (this._history.length > MAX_HISTORY) {
        this._history.shift();
        this._historyIndex--;
      }

      this.isDirty = true;

      // 每次状态变更后触发实时校验
      this._runRealtimeValidation();
    },

    /**
     * 实时校验：在每次 flowTree 变更后自动运行
     * 将校验结果写入 validationErrors，供 X6 节点展示错误状态
     */
    _runRealtimeValidation() {
      const result = ApprovalTreeValidator.checkCompleteness(
        this.flowTree.rootNode as unknown as TreeNode
      );

      const errorMap: Record<string, string[]> = {};
      for (const issue of result.issues) {
        if (issue.nodeId) {
          if (!errorMap[issue.nodeId]) {
            errorMap[issue.nodeId] = [];
          }
          errorMap[issue.nodeId].push(issue.message);
        }
      }
      this.validationErrors = errorMap;
    },

    /** 撤销 */
    undo() {
      if (!this.canUndo) return;
      this._isUndoRedoing = true;
      this._historyIndex--;
      this.flowTree = JSON.parse(this._history[this._historyIndex]);
      this._isUndoRedoing = false;
    },

    /** 重做 */
    redo() {
      if (!this.canRedo) return;
      this._isUndoRedoing = true;
      this._historyIndex++;
      this.flowTree = JSON.parse(this._history[this._historyIndex]);
      this._isUndoRedoing = false;
    },

    // ── 状态初始化 ──

    /** 初始化/加载流程树 */
    initFlowTree(tree: ApprovalFlowTree) {
      this.flowTree = deepClone(tree);
      this.selectedNodeId = null;
      this.validationErrors = {};
      this.isDirty = false;
      this._history = [JSON.stringify(this.flowTree)];
      this._historyIndex = 0;
      this._isUndoRedoing = false;
    },

    /** 重置为默认空树 */
    resetToDefault() {
      this.initFlowTree(createDefaultTree());
    },

    // ── 节点选择 ──

    /** 选中节点或分支 */
    selectNode(nodeId: string | null) {
      this.selectedNodeId = nodeId;
    },

    // ── 节点操作 ──

    /** 在指定节点后添加新节点 */
    addNode(parentId: string, newNodeType: string) {
      const result = findNodeOrBranch(
        this.flowTree.rootNode as unknown as TreeNode,
        parentId
      );
      if (!result) {
        console.warn('[ApprovalFlowStore] Parent node not found', parentId);
        return;
      }

      const target = result.node ?? result.branch;
      if (!target) return;

      const newNode = createNode(newNodeType);

      if (hasChildNode(target) && hasChildNode(newNode)) {
        type WithChild = { childNode?: TreeNode };
        const t = target as unknown as WithChild;
        const n = newNode as unknown as WithChild;
        n.childNode = t.childNode;
        t.childNode = newNode;
      }

      this._pushHistory();
    },

    /** 删除节点 */
    deleteNode(nodeId: string) {
      const result = findNodeOrBranch(
        this.flowTree.rootNode as unknown as TreeNode,
        nodeId
      );
      if (!result || !result.parent) {
        console.warn('[ApprovalFlowStore] Node not found or is root', nodeId);
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

      // 如果删除的是选中节点，清除选中状态
      if (this.selectedNodeId === nodeId) {
        this.selectedNodeId = null;
      }

      this._pushHistory();
    },

    /** 更新节点属性 */
    updateNode(updatedNode: TreeNode | ConditionBranch) {
      const result = findNodeOrBranch(
        this.flowTree.rootNode as unknown as TreeNode,
        updatedNode.id
      );
      if (!result) return;

      if (result.node) {
        Object.assign(result.node, updatedNode);
      } else if (result.branch) {
        Object.assign(result.branch, updatedNode);
      }

      this._pushHistory();
    },

    /** 添加条件分支 */
    addConditionBranch(conditionNodeId: string) {
      const result = findNodeOrBranch(
        this.flowTree.rootNode as unknown as TreeNode,
        conditionNodeId
      );
      if (
        !result ||
        !result.node ||
        !['condition', 'inclusive', 'dynamicCondition', 'parallelCondition'].includes(
          result.node.nodeType
        )
      ) {
        return;
      }

      const conditionNode = result.node as
        | ConditionNode
        | InclusiveNode
        | DynamicConditionNode
        | ParallelConditionNode;

      conditionNode.conditionNodes.push({
        id: nanoid(),
        branchName: `条件${conditionNode.conditionNodes.length + 1}`,
        conditionRule: undefined,
        childNode: undefined,
      });

      this._pushHistory();
    },

    /** 删除条件分支 */
    deleteConditionBranch(branchId: string) {
      const result = findNodeOrBranch(
        this.flowTree.rootNode as unknown as TreeNode,
        branchId
      );
      if (!result || !result.branch || !result.parent) return;

      const conditionNode = result.parent as
        | ConditionNode
        | InclusiveNode
        | DynamicConditionNode
        | ParallelConditionNode;
      const index = conditionNode.conditionNodes.findIndex((b) => b.id === branchId);
      if (index > -1) {
        conditionNode.conditionNodes.splice(index, 1);
      }

      // 如果只剩 1 个分支，将其子链提升合并到条件节点所在位置
      if (conditionNode.conditionNodes.length <= 1) {
        const survivingBranch = conditionNode.conditionNodes[0];
        const condParent = findParentOf(
          this.flowTree.rootNode as unknown as TreeNode,
          conditionNode.id
        );
        if (condParent) {
          const survivingChild = survivingBranch?.childNode;
          const conditionTail = ('childNode' in conditionNode) ? conditionNode.childNode : undefined;

          if (survivingChild) {
            type WithChild = { childNode?: TreeNode };
            let tail: WithChild = survivingChild as unknown as WithChild;
            while (tail.childNode) {
              tail = tail.childNode as unknown as WithChild;
            }
            tail.childNode = conditionTail;
            (condParent as unknown as WithChild).childNode = survivingChild;
          } else {
            (condParent as unknown as { childNode?: TreeNode }).childNode = conditionTail;
          }
        }
      }

      // 如果删除的分支是选中的，清除选中
      if (this.selectedNodeId === branchId) {
        this.selectedNodeId = null;
      }

      this._pushHistory();
    },

    /** 移动条件分支（左/右排序） */
    moveBranch(conditionNodeId: string, branchId: string, direction: 'left' | 'right') {
      const result = findNodeOrBranch(
        this.flowTree.rootNode as unknown as TreeNode,
        conditionNodeId
      );
      if (!result || !result.node) return;

      const conditionNode = result.node as ConditionNode | InclusiveNode;
      if (!conditionNode.conditionNodes) return;

      const branches = conditionNode.conditionNodes;
      const index = branches.findIndex((b) => b.id === branchId);
      if (index < 0) return;

      const targetIndex = direction === 'left' ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= branches.length) return;

      [branches[index], branches[targetIndex]] = [branches[targetIndex], branches[index]];
      this._pushHistory();
    },

    // ── 校验 ──

    /** 校验流程完整性 */
    validateFlow(): {
      valid: boolean;
      errors: string[];
      issues: ApprovalTreeValidationIssue[];
    } {
      const result = ApprovalTreeValidator.checkCompleteness(
        this.flowTree.rootNode as unknown as TreeNode
      );

      // 更新校验错误映射
      const errorMap: Record<string, string[]> = {};
      for (const issue of result.issues) {
        if (issue.nodeId) {
          if (!errorMap[issue.nodeId]) {
            errorMap[issue.nodeId] = [];
          }
          errorMap[issue.nodeId].push(issue.message);
        }
      }
      this.validationErrors = errorMap;

      return result;
    },

    /** 标记已保存（清除 dirty 标记） */
    markSaved() {
      this.isDirty = false;
    },
  },
});
