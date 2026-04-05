import type {
  AmisFormPayload,
  ApprovalDefinitionJson,
  ApprovalDefinitionMeta,
  ApprovalNode,
  ConditionBranch as DefinitionBranch,
  LfFormPayload,
} from '@/types/approval-definition';
import type {
  ApprovalFlowTree,
  ConditionBranch,
  NodeType,
  TreeNode,
} from '@/types/approval-tree';

let idSeed = 0;
const generateId = (prefix: string) => `${prefix}_${Date.now().toString(36)}_${(idSeed++).toString(36)}`;

const deepClone = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T;

const treeNodeToDefinition = (node: TreeNode): ApprovalNode => {
  const base = {
    ...(node as unknown as Record<string, unknown>),
    nodeId: node.id,
    nodeType: node.nodeType,
    nodeName: node.nodeName,
  } as Record<string, unknown>;

  delete base.id;

  if ('childNode' in node) {
    base.childNode = node.childNode ? treeNodeToDefinition(node.childNode) : undefined;
  }

  if (
    node.nodeType === 'condition' ||
    node.nodeType === 'dynamicCondition' ||
    node.nodeType === 'parallelCondition' ||
    node.nodeType === 'inclusive'
  ) {
    base.conditionNodes = node.conditionNodes.map((branch) => ({
      id: branch.id,
      branchName: branch.branchName,
      isDefault: branch.isDefault,
      conditionRule: branch.conditionRule as unknown,
      childNode: branch.childNode ? treeNodeToDefinition(branch.childNode) : undefined,
    }));
  }

  if (node.nodeType === 'parallel') {
    base.parallelNodes = node.parallelNodes.map((parallelNode) => treeNodeToDefinition(parallelNode));
  }

  return base as unknown as ApprovalNode;
};

const definitionNodeToTree = (input: ApprovalNode): TreeNode => {
  const nodeType = (input.nodeType ?? 'approve') as NodeType;
  const id = String((input as unknown as Record<string, unknown>).nodeId ?? generateId('node'));
  const nodeName = String(input.nodeName ?? nodeType);
  const source = input as unknown as Record<string, unknown>;

  const base = {
    ...source,
    id,
    nodeType,
    nodeName,
  } as Record<string, unknown>;

  delete base.nodeId;
  delete base.childNode;
  delete base.conditionNodes;
  delete base.parallelNodes;

  const childNode = input.childNode ? definitionNodeToTree(input.childNode) : undefined;

  if (
    nodeType === 'condition' ||
    nodeType === 'dynamicCondition' ||
    nodeType === 'parallelCondition' ||
    nodeType === 'inclusive'
  ) {
    const sourceBranches = (input.conditionNodes ?? []) as DefinitionBranch[];
    const conditionNodes: ConditionBranch[] = sourceBranches.map((branch, index) => ({
      id: String(branch.id ?? generateId('branch')),
      branchName: String(branch.branchName ?? `条件分支${index + 1}`),
      isDefault: Boolean(branch.isDefault),
      conditionRule: branch.conditionRule as ConditionBranch['conditionRule'],
      childNode: branch.childNode ? definitionNodeToTree(branch.childNode) : undefined,
    }));

    return {
      ...(base as object),
      id,
      nodeType,
      nodeName,
      conditionNodes,
      childNode,
    } as TreeNode;
  }

  if (nodeType === 'parallel') {
    const parallelNodes = (input.parallelNodes ?? []).map((node) => definitionNodeToTree(node));
    return {
      ...(base as object),
      id,
      nodeType,
      nodeName,
      parallelNodes,
      childNode,
    } as TreeNode;
  }

  if (
    nodeType === 'start' ||
    nodeType === 'approve' ||
    nodeType === 'copy' ||
    nodeType === 'callProcess' ||
    nodeType === 'timer' ||
    nodeType === 'trigger'
  ) {
    return {
      ...(base as object),
      id,
      nodeType,
      nodeName,
      childNode,
    } as TreeNode;
  }

  return {
    ...(base as object),
    id,
    nodeType,
    nodeName,
  } as TreeNode;
};

export class ApprovalTreeConverter {
  static createDefaultTree(): ApprovalFlowTree {
    return {
      flowName: '',
      rootNode: {
        id: generateId('node'),
        nodeType: 'start',
        nodeName: '发起人',
        childNode: {
          id: generateId('node'),
          nodeType: 'end',
          nodeName: '结束',
        },
      },
    };
  }

  static treeToDefinitionJson(
    tree: ApprovalFlowTree,
    meta?: ApprovalDefinitionMeta,
    lfForm?: LfFormPayload,
    amisForm?: AmisFormPayload,
  ): string {
    const definition: ApprovalDefinitionJson = {
      meta: {
        flowName: meta?.flowName ?? tree.flowName,
        description: meta?.description,
        category: meta?.category,
        visibilityScope: meta?.visibilityScope,
        isQuickEntry: meta?.isQuickEntry,
        isLowCodeFlow: meta?.isLowCodeFlow,
      },
      lfForm,
      amisForm,
      nodes: {
        rootNode: treeNodeToDefinition(tree.rootNode),
      },
    };
    return JSON.stringify(definition);
  }

  static definitionJsonToState(json: string): {
    tree: ApprovalFlowTree;
    meta?: ApprovalDefinitionMeta;
    lfForm?: LfFormPayload;
    amisForm?: AmisFormPayload;
  } {
    if (!json || json.trim() === '' || json.trim() === '{}') {
      return { tree: this.createDefaultTree() };
    }

    try {
      const parsed = JSON.parse(json) as Partial<ApprovalDefinitionJson>;
      if (parsed.nodes?.rootNode) {
        const rootNode = definitionNodeToTree(parsed.nodes.rootNode);
        const tree: ApprovalFlowTree = {
          flowName: parsed.meta?.flowName ?? '',
          rootNode: rootNode.nodeType === 'start' ? rootNode : this.createDefaultTree().rootNode,
        };
        return {
          tree,
          meta: parsed.meta ? deepClone(parsed.meta) : undefined,
          lfForm: parsed.lfForm ? deepClone(parsed.lfForm) : undefined,
          amisForm: parsed.amisForm ? deepClone(parsed.amisForm) : undefined,
        };
      }
    } catch {
      return { tree: this.createDefaultTree() };
    }

    return { tree: this.createDefaultTree() };
  }

  static definitionJsonToTree(json: string): ApprovalFlowTree {
    return this.definitionJsonToState(json).tree;
  }
}
