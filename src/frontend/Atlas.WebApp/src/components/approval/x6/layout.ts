/**
 * 钉钉风格审批流树形自动布局引擎
 *
 * 两遍遍历：
 *  1. 自底向上：计算每个子树的宽度
 *  2. 自顶向下：根据宽度分配 x/y 坐标
 *
 * 产出 LayoutNode[] 和 LayoutEdge[]，供 sync.ts 生成 X6 节点/边。
 */

import type {
  ApprovalFlowTree,
  TreeNode,
  ConditionNode,
  ConditionBranch,
  DynamicConditionNode,
  ParallelConditionNode,
  ParallelNode,
  InclusiveNode,
} from '@/types/approval-tree';

// ── 布局常量 ──────────────────────────────────────────────
export const LAYOUT = {
  /** 普通节点（start / approve / copy）卡片宽度 */
  NODE_W: 220,
  /** 普通节点卡片高度 */
  NODE_H: 72,
  /** 条件头（"添加条件"按钮行）高度 */
  COND_HEADER_H: 44,
  /** 条件分支卡片高度 */
  COND_BRANCH_H: 72,
  /** 结束节点高度 */
  END_H: 36,
  /** "+" 添加按钮节点宽度 */
  ADD_BTN_W: 240,
  /** "+" 添加按钮节点高度（含上下留白） */
  ADD_BTN_H: 56,
  /** 条件分支之间水平间距 */
  BRANCH_GAP: 50,
  /** 条件分支区域顶部/底部连线的垂直空间 */
  BRANCH_VERT_PAD: 20,
  /** 非条件节点间因为有 AddButton 而产生的垂直间距 */
  VERT_GAP: 0, // AddButton 自身含间距，此处无额外间距
};

// ── 产出类型 ──────────────────────────────────────────────

export type LayoutShapeType =
  | 'start'
  | 'approve'
  | 'copy'
  | 'condition-header'
  | 'condition-branch'
  | 'parallel-container'
  | 'end'
  | 'add-button'
  | 'parallel'
  | 'inclusive'
  | 'route'
  | 'callProcess'
  | 'timer'
  | 'trigger';

export interface LayoutNode {
  id: string;
  /** X6 shape 类型 */
  shapeType: LayoutShapeType;
  x: number;
  y: number;
  width: number;
  height: number;
  /** 业务数据，透传给 Vue Shape */
  data: Record<string, unknown>;
}

export interface LayoutEdge {
  id: string;
  source: string;
  target: string;
  /** 条件分支/并行分支的 label */
  label?: string;
  /** 用于控制连线路由的顶点 */
  vertices?: Array<{ x: number; y: number }>;
}

export interface LayoutResult {
  nodes: LayoutNode[];
  edges: LayoutEdge[];
  /** 整棵树的总宽度 */
  totalWidth: number;
  /** 整棵树的总高度 */
  totalHeight: number;
}

// ── 内部辅助结构 ──────────────────────────────────────────

interface SubtreeMetrics {
  /** 子树整体宽度 */
  width: number;
  /** 子树整体高度 */
  height: number;
}

// ── 工具函数 ──────────────────────────────────────────────

let _edgeCounter = 0;
const edgeId = () => `__edge_${++_edgeCounter}`;
let _addBtnCounter = 0;
const addBtnId = (parentId: string) => `__add_${parentId}_${++_addBtnCounter}`;

function isConditionLike(
  node: TreeNode,
): node is ConditionNode | DynamicConditionNode | ParallelConditionNode | InclusiveNode {
  return (
    node.nodeType === 'condition' ||
    node.nodeType === 'dynamicCondition' ||
    node.nodeType === 'parallelCondition' ||
    node.nodeType === 'inclusive'
  );
}

function isParallel(node: TreeNode): node is ParallelNode {
  return node.nodeType === 'parallel';
}

function nodeHeight(node: TreeNode): number {
  if (node.nodeType === 'end') return LAYOUT.END_H;
  return LAYOUT.NODE_H;
}

// ── 第一遍：自底向上计算子树宽度 & 高度 ──────────────────

function measureChain(node: TreeNode | undefined): SubtreeMetrics {
  if (!node) return { width: 0, height: 0 };

  let w = LAYOUT.NODE_W;
  let h = nodeHeight(node);

  // 条件类节点
  if (isConditionLike(node)) {
    const condNode = node as ConditionNode;
    const branchMetrics = condNode.conditionNodes.map((branch) =>
      measureBranch(branch),
    );

    const totalBranchW =
      branchMetrics.reduce((sum, m) => sum + m.width, 0) +
      Math.max(0, branchMetrics.length - 1) * LAYOUT.BRANCH_GAP;

    const maxBranchH = branchMetrics.reduce(
      (max, m) => Math.max(max, m.height),
      0,
    );

    w = Math.max(LAYOUT.NODE_W, totalBranchW);

    // 条件头(44) + 上连线区(20) + 分支内容(maxBranchH) + 下连线区(20) + 汇聚后AddButton(56)
    h =
      LAYOUT.COND_HEADER_H +
      LAYOUT.BRANCH_VERT_PAD +
      maxBranchH +
      LAYOUT.BRANCH_VERT_PAD +
      LAYOUT.ADD_BTN_H;

    // 加上后续 childNode 链
    if (node.childNode) {
      const childMetrics = measureChain(node.childNode);
      h += childMetrics.height;
      w = Math.max(w, childMetrics.width);
    }

    return { width: w, height: h };
  }

  // 并行审批节点
  if (isParallel(node)) {
    const parallelNode = node as ParallelNode;
    const branchMetrics = parallelNode.parallelNodes.map((bn) =>
      measureChain(bn),
    );

    const totalBranchW =
      branchMetrics.reduce((sum, m) => sum + Math.max(m.width, LAYOUT.NODE_W), 0) +
      Math.max(0, branchMetrics.length - 1) * LAYOUT.BRANCH_GAP;

    const maxBranchH = branchMetrics.reduce(
      (max, m) => Math.max(max, m.height),
      0,
    );

    w = Math.max(LAYOUT.NODE_W, totalBranchW);
    h =
      LAYOUT.NODE_H + // parallel 节点标题
      LAYOUT.BRANCH_VERT_PAD +
      maxBranchH +
      LAYOUT.BRANCH_VERT_PAD +
      LAYOUT.ADD_BTN_H;

    if (node.childNode) {
      const childMetrics = measureChain(node.childNode);
      h += childMetrics.height;
      w = Math.max(w, childMetrics.width);
    }

    return { width: w, height: h };
  }

  // 普通节点: 自身高度 + AddButton高度 + childNode链
  // (非 end 节点才加 AddButton)
  if (node.nodeType !== 'end') {
    h += LAYOUT.ADD_BTN_H;
  }

  if ('childNode' in node && node.childNode) {
    const childMetrics = measureChain(node.childNode);
    h += childMetrics.height;
    w = Math.max(w, childMetrics.width);
  }

  return { width: w, height: h };
}

function measureBranch(branch: ConditionBranch): SubtreeMetrics {
  let w = LAYOUT.NODE_W;
  // 分支标题卡片 + AddButton + child chain
  let h = LAYOUT.COND_BRANCH_H + LAYOUT.ADD_BTN_H;

  if (branch.childNode) {
    const childMetrics = measureChain(branch.childNode);
    h += childMetrics.height;
    w = Math.max(w, childMetrics.width);
  }

  return { width: w, height: h };
}

// ── 第二遍：自顶向下分配坐标 ──────────────────────────────

function layoutChain(
  node: TreeNode | undefined,
  centerX: number,
  startY: number,
  nodes: LayoutNode[],
  edges: LayoutEdge[],
  parentNodeId: string | null,
): { lastNodeId: string | null; endY: number } {
  if (!node) return { lastNodeId: parentNodeId, endY: startY };

  const x = centerX - LAYOUT.NODE_W / 2;
  let y = startY;

  // ── 条件类节点 ──
  if (isConditionLike(node)) {
    return layoutCondition(node, centerX, y, nodes, edges, parentNodeId);
  }

  // ── 并行审批节点 ──
  if (isParallel(node)) {
    return layoutParallel(node as ParallelNode, centerX, y, nodes, edges, parentNodeId);
  }

  // ── 普通节点 ──
  const h = nodeHeight(node);
  const shapeType: LayoutShapeType =
    node.nodeType === 'end'
      ? 'end'
      : (node.nodeType as LayoutShapeType);

  nodes.push({
    id: node.id,
    shapeType,
    x,
    y,
    width: LAYOUT.NODE_W,
    height: h,
    data: { ...node } as unknown as Record<string, unknown>,
  });

  // 从父节点连线
  if (parentNodeId) {
    edges.push({ id: edgeId(), source: parentNodeId, target: node.id });
  }

  y += h;

  // End 节点无后续
  if (node.nodeType === 'end') {
    return { lastNodeId: node.id, endY: y };
  }

  // 添加 AddButton
  const btnId = addBtnId(node.id);
  nodes.push({
    id: btnId,
    shapeType: 'add-button',
    x: centerX - LAYOUT.ADD_BTN_W / 2,
    y,
    width: LAYOUT.ADD_BTN_W,
    height: LAYOUT.ADD_BTN_H,
    data: { parentId: node.id },
  });
  edges.push({ id: edgeId(), source: node.id, target: btnId });
  y += LAYOUT.ADD_BTN_H;

  // 递归 childNode
  if ('childNode' in node && node.childNode) {
    const child = layoutChain(
      node.childNode,
      centerX,
      y,
      nodes,
      edges,
      btnId,
    );
    return child;
  }

  return { lastNodeId: btnId, endY: y };
}

function layoutCondition(
  node: ConditionNode | DynamicConditionNode | ParallelConditionNode | InclusiveNode,
  centerX: number,
  startY: number,
  nodes: LayoutNode[],
  edges: LayoutEdge[],
  parentNodeId: string | null,
): { lastNodeId: string | null; endY: number } {
  let y = startY;

  // ── 条件头节点 ──
  const headerX = centerX - LAYOUT.NODE_W / 2;
  nodes.push({
    id: node.id,
    shapeType: 'condition-header',
    x: headerX,
    y,
    width: LAYOUT.NODE_W,
    height: LAYOUT.COND_HEADER_H,
    data: { ...node } as unknown as Record<string, unknown>,
  });
  if (parentNodeId) {
    edges.push({ id: edgeId(), source: parentNodeId, target: node.id });
  }

  y += LAYOUT.COND_HEADER_H + LAYOUT.BRANCH_VERT_PAD;

  // ── 计算每个分支宽度并分配水平位置 ──
  const branches = node.conditionNodes;
  const branchWidths = branches.map((b) => Math.max(measureBranch(b).width, LAYOUT.NODE_W));
  const branchHeights = branches.map((b) => measureBranch(b).height);
  const maxBranchH = Math.max(...branchHeights, 0);

  const totalW =
    branchWidths.reduce((s, w) => s + w, 0) +
    Math.max(0, branches.length - 1) * LAYOUT.BRANCH_GAP;

  let bx = centerX - totalW / 2;
  const branchEndIds: string[] = [];
  const branchEndYs: number[] = [];

  branches.forEach((branch, i) => {
    const bCenter = bx + branchWidths[i] / 2;
    const bStartX = bCenter - LAYOUT.NODE_W / 2;
    const bStartY = y;

    // 分支卡片（注入 _branchIndex / _totalBranches / _conditionNodeId 供 UI 使用）
    nodes.push({
      id: branch.id,
      shapeType: 'condition-branch',
      x: bStartX,
      y: bStartY,
      width: LAYOUT.NODE_W,
      height: LAYOUT.COND_BRANCH_H,
      data: {
        ...branch,
        parentConditionId: node.id,
        _branchIndex: i + 1,
        _totalBranches: branches.length,
        _conditionNodeId: node.id,
      } as unknown as Record<string, unknown>,
    });

    // 从条件头到分支的连线（用顶点实现折线）
    const headerCenterY = startY + LAYOUT.COND_HEADER_H;
    edges.push({
      id: edgeId(),
      source: node.id,
      target: branch.id,
      label: branch.branchName,
      vertices: [
        { x: centerX, y: headerCenterY + LAYOUT.BRANCH_VERT_PAD / 2 },
        { x: bCenter, y: headerCenterY + LAYOUT.BRANCH_VERT_PAD / 2 },
      ],
    });

    // 分支内的 AddButton + child chain
    let innerY = bStartY + LAYOUT.COND_BRANCH_H;
    const addId = addBtnId(branch.id);
    nodes.push({
      id: addId,
      shapeType: 'add-button',
      x: bCenter - LAYOUT.ADD_BTN_W / 2,
      y: innerY,
      width: LAYOUT.ADD_BTN_W,
      height: LAYOUT.ADD_BTN_H,
      data: { parentId: branch.id },
    });
    edges.push({ id: edgeId(), source: branch.id, target: addId });
    innerY += LAYOUT.ADD_BTN_H;

    if (branch.childNode) {
      const result = layoutChain(
        branch.childNode,
        bCenter,
        innerY,
        nodes,
        edges,
        addId,
      );
      branchEndIds.push(result.lastNodeId || addId);
      branchEndYs.push(result.endY);
    } else {
      branchEndIds.push(addId);
      branchEndYs.push(innerY);
    }

    bx += branchWidths[i] + LAYOUT.BRANCH_GAP;
  });

  // 汇聚后的位置 = 分支区起始 + 最大分支高度 + 底部间距
  const mergeY = y + maxBranchH + LAYOUT.BRANCH_VERT_PAD;

  // 汇聚后 AddButton
  const mergeAddId = addBtnId(node.id + '_merge');
  nodes.push({
    id: mergeAddId,
    shapeType: 'add-button',
    x: centerX - LAYOUT.ADD_BTN_W / 2,
    y: mergeY,
    width: LAYOUT.ADD_BTN_W,
    height: LAYOUT.ADD_BTN_H,
    data: { parentId: node.id },
  });

  // 各分支末尾连到汇聚 AddButton（折线汇聚）
  branchEndIds.forEach((endId, i) => {
    const branchCenterX =
      centerX -
      totalW / 2 +
      branchWidths.slice(0, i).reduce((s, w) => s + w + LAYOUT.BRANCH_GAP, 0) +
      branchWidths[i] / 2;

    edges.push({
      id: edgeId(),
      source: endId,
      target: mergeAddId,
      vertices: [
        { x: branchCenterX, y: mergeY - LAYOUT.BRANCH_VERT_PAD / 2 },
        { x: centerX, y: mergeY - LAYOUT.BRANCH_VERT_PAD / 2 },
      ],
    });
  });

  let nextY = mergeY + LAYOUT.ADD_BTN_H;

  // 条件节点的 childNode（汇聚后）
  if (node.childNode) {
    return layoutChain(node.childNode, centerX, nextY, nodes, edges, mergeAddId);
  }

  return { lastNodeId: mergeAddId, endY: nextY };
}

function layoutParallel(
  node: ParallelNode,
  centerX: number,
  startY: number,
  nodes: LayoutNode[],
  edges: LayoutEdge[],
  parentNodeId: string | null,
): { lastNodeId: string | null; endY: number } {
  let y = startY;

  // 并行节点头
  nodes.push({
    id: node.id,
    shapeType: 'parallel',
    x: centerX - LAYOUT.NODE_W / 2,
    y,
    width: LAYOUT.NODE_W,
    height: LAYOUT.NODE_H,
    data: { ...node } as unknown as Record<string, unknown>,
  });
  if (parentNodeId) {
    edges.push({ id: edgeId(), source: parentNodeId, target: node.id });
  }

  y += LAYOUT.NODE_H + LAYOUT.BRANCH_VERT_PAD;

  // 并行分支
  const branchMetrics = node.parallelNodes.map((bn) => measureChain(bn));
  const branchWidths = branchMetrics.map((m) => Math.max(m.width, LAYOUT.NODE_W));
  const maxBranchH = Math.max(...branchMetrics.map((m) => m.height), 0);

  const totalW =
    branchWidths.reduce((s, w) => s + w, 0) +
    Math.max(0, node.parallelNodes.length - 1) * LAYOUT.BRANCH_GAP;

  const mergeY = y + maxBranchH + LAYOUT.BRANCH_VERT_PAD;
  nodes.push({
    id: `${node.id}__parallel_container`,
    shapeType: 'parallel-container',
    x: centerX - totalW / 2 - 28,
    y: y - 12,
    width: totalW + 56,
    height: mergeY + LAYOUT.ADD_BTN_H - y + 24,
    data: {
      nodeId: node.id,
      title: `${node.nodeName || '并行审批'}域`,
    },
  });

  let bx = centerX - totalW / 2;
  const branchEndIds: string[] = [];

  node.parallelNodes.forEach((branch, i) => {
    const bCenter = bx + branchWidths[i] / 2;
    const result = layoutChain(branch, bCenter, y, nodes, edges, null);

    // 从 parallel 节点到分支第一个节点的连线
    edges.push({
      id: edgeId(),
      source: node.id,
      target: branch.id,
      vertices: [
        { x: centerX, y: startY + LAYOUT.NODE_H + LAYOUT.BRANCH_VERT_PAD / 2 },
        { x: bCenter, y: startY + LAYOUT.NODE_H + LAYOUT.BRANCH_VERT_PAD / 2 },
      ],
    });

    branchEndIds.push(result.lastNodeId || branch.id);
    bx += branchWidths[i] + LAYOUT.BRANCH_GAP;
  });

  // 汇聚 AddButton
  const mergeAddId = addBtnId(node.id + '_merge');
  nodes.push({
    id: mergeAddId,
    shapeType: 'add-button',
    x: centerX - LAYOUT.ADD_BTN_W / 2,
    y: mergeY,
    width: LAYOUT.ADD_BTN_W,
    height: LAYOUT.ADD_BTN_H,
    data: { parentId: node.id },
  });

  branchEndIds.forEach((endId, i) => {
    const branchCenterX =
      centerX -
      totalW / 2 +
      branchWidths.slice(0, i).reduce((s, w) => s + w + LAYOUT.BRANCH_GAP, 0) +
      branchWidths[i] / 2;

    edges.push({
      id: edgeId(),
      source: endId,
      target: mergeAddId,
      vertices: [
        { x: branchCenterX, y: mergeY - LAYOUT.BRANCH_VERT_PAD / 2 },
        { x: centerX, y: mergeY - LAYOUT.BRANCH_VERT_PAD / 2 },
      ],
    });
  });

  let nextY = mergeY + LAYOUT.ADD_BTN_H;

  if (node.childNode) {
    return layoutChain(node.childNode, centerX, nextY, nodes, edges, mergeAddId);
  }

  return { lastNodeId: mergeAddId, endY: nextY };
}

// ── 入口 ──────────────────────────────────────────────────

export function computeLayout(tree: ApprovalFlowTree): LayoutResult {
  // 重置计数器
  _edgeCounter = 0;
  _addBtnCounter = 0;

  const metrics = measureChain(tree.rootNode);
  const centerX = Math.max(metrics.width, LAYOUT.NODE_W) / 2 + 50; // 留左侧边距

  const nodes: LayoutNode[] = [];
  const edges: LayoutEdge[] = [];

  const result = layoutChain(tree.rootNode, centerX, 50, nodes, edges, null);

  return {
    nodes,
    edges,
    totalWidth: Math.max(metrics.width + 100, LAYOUT.NODE_W + 100),
    totalHeight: result.endY + 50,
  };
}
