/**
 * Tree -> X6 Graph 同步引擎
 *
 * 每次 flowTree 发生变化时调用 syncGraphFromTree()：
 *  1. 调用 layout.computeLayout 计算所有节点/边的坐标
 *  2. 全量清空 Graph 并重新添加节点/边（简单可靠）
 */

import type { Graph } from '@antv/x6';
import type { ApprovalFlowTree } from '@/types/approval-tree';
import { computeLayout, type LayoutNode, type LayoutEdge } from './layout';
import { shapeNameFromType } from './shapes/register';

/**
 * 将 ApprovalFlowTree 同步渲染到 X6 Graph。
 * 全量替换策略：清空旧节点/边 → 计算布局 → 添加新节点/边。
 */
export function syncGraphFromTree(graph: Graph, tree: ApprovalFlowTree) {
  // 批量更新：先清空再添加
  graph.clearCells({ silent: true });

  // 计算布局
  const layout = computeLayout(tree);

  // 添加节点
  layout.nodes.forEach((ln: LayoutNode) => {
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
  });

  // 添加边
  layout.edges.forEach((le: LayoutEdge) => {
    graph.addEdge({
      id: le.id,
      source: le.source,
      target: le.target,
      vertices: le.vertices,
      connector: { name: 'rounded', args: { radius: 6 } },
      router: le.vertices && le.vertices.length > 0
        ? undefined
        : { name: 'normal' },
      attrs: {
        line: {
          stroke: '#cacaca',
          strokeWidth: 2,
          targetMarker: null,
        },
      },
      zIndex: -1,
    });
  });

  // 居中内容
  graph.centerContent();
}

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
export function highlightNode(graph: Graph, nodeId: string | null, prevId: string | null) {
  // 移除上一个选中状态
  if (prevId) {
    const prev = graph.getCellById(prevId);
    if (prev && prev.isNode()) {
      const prevData = prev.getData() || {};
      prev.setData({ ...prevData, _selected: false }, { silent: true });
      // 通知 vue shape 刷新
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
