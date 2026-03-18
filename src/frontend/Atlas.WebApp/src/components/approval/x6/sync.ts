/**
 * Tree → X6 Graph 增量同步引擎 (v2)
 *
 * 核心改进：
 * - 首次渲染使用全量策略
 * - 后续变更通过 Diff 计算最小增量集，精确操作 X6 Graph
 * - 保留节点选中状态、X6 History 能力
 * - 公开接口签名不变，对外透明升级
 */

import type { Graph, Node, Cell } from '@antv/x6';
import type { ApprovalFlowTree } from '@/types/approval-tree';
import { computeLayout, type LayoutNode, type LayoutEdge } from './layout';
import { shapeNameFromType } from './shapes/register';

// ── 缓存：保存上次同步的布局结果用于 Diff ──
let _lastLayoutNodes: Map<string, LayoutNode> | null = null;
let _lastLayoutEdges: Map<string, LayoutEdge> | null = null;

// ── Diff 类型 ──

interface SyncDiff {
  addNodes: LayoutNode[];
  removeNodeIds: string[];
  updateNodes: Array<{ id: string; node: LayoutNode; changes: NodeChanges }>;
  addEdges: LayoutEdge[];
  removeEdgeIds: string[];
}

interface NodeChanges {
  position?: boolean;
  size?: boolean;
  data?: boolean;
  shape?: boolean;
}

// ── Diff 算法 ──

function computeDiff(
  oldNodes: Map<string, LayoutNode>,
  newNodes: Map<string, LayoutNode>,
  oldEdges: Map<string, LayoutEdge>,
  newEdges: Map<string, LayoutEdge>,
): SyncDiff {
  const diff: SyncDiff = {
    addNodes: [],
    removeNodeIds: [],
    updateNodes: [],
    addEdges: [],
    removeEdgeIds: [],
  };

  // 节点 Diff
  for (const [id, newNode] of newNodes) {
    const oldNode = oldNodes.get(id);
    if (!oldNode) {
      diff.addNodes.push(newNode);
    } else {
      const changes: NodeChanges = {};
      if (oldNode.x !== newNode.x || oldNode.y !== newNode.y) {
        changes.position = true;
      }
      if (oldNode.width !== newNode.width || oldNode.height !== newNode.height) {
        changes.size = true;
      }
      if (oldNode.shapeType !== newNode.shapeType) {
        changes.shape = true;
      }
      // 对比 data（浅比较关键字段）
      if (!shallowDataEqual(oldNode.data, newNode.data)) {
        changes.data = true;
      }
      if (changes.position || changes.size || changes.data || changes.shape) {
        diff.updateNodes.push({ id, node: newNode, changes });
      }
    }
  }

  for (const id of oldNodes.keys()) {
    if (!newNodes.has(id)) {
      diff.removeNodeIds.push(id);
    }
  }

  // 边 Diff：边的 ID 是动态生成的（__edge_N），不能直接按 ID 比对
  // 改用 source+target 作为唯一键
  const oldEdgeKeys = new Map<string, LayoutEdge>();
  for (const [, edge] of oldEdges) {
    oldEdgeKeys.set(`${edge.source}->${edge.target}`, edge);
  }
  const newEdgeKeys = new Map<string, LayoutEdge>();
  for (const [, edge] of newEdges) {
    newEdgeKeys.set(`${edge.source}->${edge.target}`, edge);
  }

  for (const [key, edge] of newEdgeKeys) {
    if (!oldEdgeKeys.has(key)) {
      diff.addEdges.push(edge);
    }
  }

  for (const [key, edge] of oldEdgeKeys) {
    if (!newEdgeKeys.has(key)) {
      diff.removeEdgeIds.push(edge.id);
    }
  }

  return diff;
}

/** 浅比较两个 data 对象的关键字段 */
function shallowDataEqual(
  a: Record<string, unknown>,
  b: Record<string, unknown>,
): boolean {
  // 比较核心标识字段
  const keys = [
    'nodeName', 'branchName', 'nodeType', 'assigneeType', 'assigneeValue',
    'approvalMode', 'error', '_selected', 'isDefault', 'parentId',
    'title', '_branchIndex', '_totalBranches',
  ];
  for (const key of keys) {
    if (a[key] !== b[key]) return false;
  }
  return true;
}

// ── 应用 Diff 到 X6 Graph ──

function applyDiff(graph: Graph, diff: SyncDiff, allNewEdges: LayoutEdge[]) {
  // 使用 batch 避免中间触发多次渲染
  graph.startBatch('sync');

  try {
    // 1. 删除边（先删边再删节点，避免悬挂引用）
    for (const edgeId of diff.removeEdgeIds) {
      const cell = graph.getCellById(edgeId);
      if (cell) {
        graph.removeCell(cell, { silent: true });
      }
    }

    // 2. 删除节点
    for (const nodeId of diff.removeNodeIds) {
      const cell = graph.getCellById(nodeId);
      if (cell) {
        graph.removeCell(cell, { silent: true });
      }
    }

    // 3. 更新节点（位置、大小、数据）
    for (const update of diff.updateNodes) {
      const cell = graph.getCellById(update.id);
      if (!cell || !cell.isNode()) {
        // Shape 类型变了或者节点不存在 → 按删除+新增处理
        if (cell) graph.removeCell(cell, { silent: true });
        addNodeToGraph(graph, update.node);
        continue;
      }

      const n = cell as Node;

      if (update.changes.shape) {
        // Shape 类型变更：需要先删后加
        graph.removeCell(cell, { silent: true });
        addNodeToGraph(graph, update.node);
        continue;
      }

      if (update.changes.position) {
        n.position(update.node.x, update.node.y, { silent: true });
      }
      if (update.changes.size) {
        n.resize(update.node.width, update.node.height, { silent: true });
      }
      if (update.changes.data) {
        n.setData(update.node.data, { overwrite: true });
      }
    }

    // 4. 添加新节点
    for (const ln of diff.addNodes) {
      addNodeToGraph(graph, ln);
    }

    // 5. 添加新边
    for (const le of diff.addEdges) {
      addEdgeToGraph(graph, le);
    }
  } finally {
    graph.stopBatch('sync');
  }
}

// ── 全量渲染（首次加载使用）──

function fullRender(graph: Graph, nodes: LayoutNode[], edges: LayoutEdge[]) {
  graph.clearCells({ silent: true });

  for (const ln of nodes) {
    addNodeToGraph(graph, ln);
  }

  for (const le of edges) {
    addEdgeToGraph(graph, le);
  }

  graph.centerContent();
}

// ── 节点/边添加辅助函数 ──

function addNodeToGraph(graph: Graph, ln: LayoutNode) {
  if (ln.shapeType === 'parallel-container') {
    graph.addNode({
      id: ln.id,
      shape: 'rect',
      x: ln.x,
      y: ln.y,
      width: ln.width,
      height: ln.height,
      attrs: {
        body: {
          fill: '#f6ffed',
          fillOpacity: 0.45,
          stroke: '#95de64',
          strokeWidth: 1.5,
          strokeDasharray: '6 4',
          rx: 12,
          ry: 12,
        },
        label: {
          text: String(ln.data.title || ''),
          fill: '#389e0d',
          fontSize: 12,
          refX: 12,
          refY: 14,
          textAnchor: 'start',
        },
      },
      data: ln.data,
      zIndex: -2,
    });
    return;
  }

  const nodeType = typeof ln.data.nodeType === 'string' ? String(ln.data.nodeType) : '';
  graph.addNode({
    id: ln.id,
    shape: shapeNameFromType(ln.shapeType),
    x: ln.x,
    y: ln.y,
    width: ln.width,
    height: ln.height,
    data: ln.data,
    ports: buildPorts(nodeType),
  });
}

function addEdgeToGraph(graph: Graph, le: LayoutEdge) {
  graph.addEdge({
    id: le.id,
    source: le.source,
    target: le.target,
    vertices: le.vertices,
    connector: { name: 'rounded', args: { radius: 6 } },
    router:
      le.vertices && le.vertices.length > 0 ? undefined : { name: 'normal' },
    attrs: {
      line: {
        stroke: '#cacaca',
        strokeWidth: 2,
        targetMarker: null,
      },
    },
    zIndex: -1,
  });
}

// ── 公开 API ──

/**
 * 将 ApprovalFlowTree 同步渲染到 X6 Graph。
 *
 * 增量策略：
 * - 首次调用（缓存为空）：全量渲染
 * - 后续调用：计算 Diff 并精确更新
 *
 * @param forceFullRender 强制全量渲染（例如加载新流程时）
 * @param displayLabels 由 Store getter 计算的节点展示标签 (nodeId → label)
 * @param validationErrors 由 Store 计算的节点校验错误 (nodeId → errorMessages[])
 */
export function syncGraphFromTree(
  graph: Graph,
  tree: ApprovalFlowTree,
  forceFullRender = false,
  displayLabels?: Record<string, string>,
  validationErrors?: Record<string, string[]>,
) {
  const layout = computeLayout(tree);

  // 注入 Store 计算的展示标签和校验错误到布局节点数据
  for (const n of layout.nodes) {
    if (displayLabels) {
      const label = displayLabels[n.id];
      if (label !== undefined) {
        n.data._displayLabel = label;
      }
    }
    if (validationErrors) {
      const errors = validationErrors[n.id];
      if (errors && errors.length > 0) {
        n.data.error = true;
        n.data._validationErrors = errors;
      } else {
        n.data.error = false;
        n.data._validationErrors = undefined;
      }
    }
  }

  // 构建新布局的查找映射
  const newNodeMap = new Map<string, LayoutNode>();
  for (const n of layout.nodes) {
    newNodeMap.set(n.id, n);
  }
  const newEdgeMap = new Map<string, LayoutEdge>();
  for (const e of layout.edges) {
    newEdgeMap.set(e.id, e);
  }

  if (forceFullRender || !_lastLayoutNodes || !_lastLayoutEdges) {
    // 首次渲染或强制全量
    fullRender(graph, layout.nodes, layout.edges);
  } else {
    // 增量更新
    const diff = computeDiff(_lastLayoutNodes, newNodeMap, _lastLayoutEdges, newEdgeMap);

    const hasChanges =
      diff.addNodes.length > 0 ||
      diff.removeNodeIds.length > 0 ||
      diff.updateNodes.length > 0 ||
      diff.addEdges.length > 0 ||
      diff.removeEdgeIds.length > 0;

    if (hasChanges) {
      applyDiff(graph, diff, layout.edges);
    }
  }

  // 更新缓存
  _lastLayoutNodes = newNodeMap;
  _lastLayoutEdges = newEdgeMap;
}

/**
 * 重置同步缓存。在切换流程定义时调用，确保下次同步使用全量渲染。
 */
export function resetSyncCache() {
  _lastLayoutNodes = null;
  _lastLayoutEdges = null;
}

// ── Ports 构建 ──

function buildPorts(nodeType: string) {
  if (!nodeType || nodeType === 'end') {
    return {
      groups: {
        in: {
          position: 'top',
          attrs: {
            circle: {
              r: 5,
              magnet: 'passive',
              stroke: '#1677ff',
              strokeWidth: 2,
              fill: '#fff',
            },
          },
        },
      },
      items: nodeType === 'end' ? [{ id: 'in', group: 'in' }] : [],
    };
  }

  const groups = {
    in: {
      position: 'top',
      attrs: {
        circle: {
          r: 5,
          magnet: 'passive',
          stroke: '#1677ff',
          strokeWidth: 2,
          fill: '#fff',
        },
      },
    },
    out: {
      position: 'bottom',
      attrs: {
        circle: {
          r: 5,
          magnet: true,
          stroke: '#1677ff',
          strokeWidth: 2,
          fill: '#fff',
        },
      },
    },
  };

  const items: Array<{ id: string; group: 'in' | 'out' }> = [];
  if (nodeType !== 'start') {
    items.push({ id: 'in', group: 'in' });
  }
  if (nodeType === 'route') {
    items.push({ id: 'out', group: 'out' });
  }

  return { groups, items };
}

/**
 * 高亮选中的节点
 */
export function highlightNode(
  graph: Graph,
  nodeId: string | null,
  prevId: string | null,
) {
  // 移除上一个选中状态
  if (prevId) {
    const prev = graph.getCellById(prevId);
    if (prev && prev.isNode()) {
      const prevData = prev.getData() || {};
      prev.setData({ ...prevData, _selected: false });
    }
  }

  // 设置当前选中
  if (nodeId) {
    const curr = graph.getCellById(nodeId);
    if (curr && curr.isNode()) {
      const currData = curr.getData() || {};
      curr.setData({ ...currData, _selected: true });
    }
  }
}
