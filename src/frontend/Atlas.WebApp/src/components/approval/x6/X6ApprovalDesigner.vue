<template>
  <div class="dd-designer">
    <div ref="containerRef" class="dd-designer-canvas"></div>

    <!-- 缩放控制栏 -->
    <div class="dd-zoom-toolbar">
      <button class="dd-zoom-btn" @click="zoomOut" title="缩小">
        <MinusOutlined />
      </button>
      <span class="dd-zoom-value">{{ zoomPercent }}%</span>
      <button class="dd-zoom-btn" @click="zoomIn" title="放大">
        <PlusOutlined />
      </button>
      <button class="dd-zoom-btn" @click="zoomFit" title="适应画布">
        <CompressOutlined />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch, computed, nextTick } from 'vue';
import { Graph } from '@antv/x6';
import {
  MinusOutlined,
  PlusOutlined,
  CompressOutlined,
} from '@ant-design/icons-vue';
import type { ApprovalFlowTree, TreeNode, ConditionBranch } from '@/types/approval-tree';
import { registerAllShapes } from './shapes/register';
import { syncGraphFromTree, highlightNode } from './sync';

const props = defineProps<{
  flowTree: ApprovalFlowTree;
  selectedNodeId: string | null;
}>();

const emit = defineEmits<{
  selectNode: [node: TreeNode | ConditionBranch | null];
  addNode: [parentId: string, nodeType: string];
  deleteNode: [nodeId: string];
  addConditionBranch: [nodeId: string];
  deleteConditionBranch: [branchId: string];
  moveBranch: [conditionNodeId: string, branchId: string, direction: 'left' | 'right'];
}>();

const containerRef = ref<HTMLElement>();
const graphRef = ref<Graph>();
const zoom = ref(1);
const zoomPercent = computed(() => Math.round(zoom.value * 100));

// ── Graph 初始化 ──
function initGraph() {
  if (!containerRef.value) return;

  registerAllShapes();

  const graph = new Graph({
    container: containerRef.value,
    autoResize: true,
    background: { color: '#f5f5f7' },
    grid: { visible: false },
    panning: {
      enabled: true,
      modifiers: [], // 允许任意拖拽平移
    },
    mousewheel: {
      enabled: true,
      zoomAtMousePosition: true,
      modifiers: ['ctrl', 'meta'],
      minScale: 0.3,
      maxScale: 3,
    },
    interacting: {
      nodeMovable: false,
      edgeMovable: false,
      edgeLabelMovable: false,
    },
    connecting: {
      allowBlank: false,
      allowLoop: false,
      allowNode: false,
      allowEdge: false,
    },
  });

  // ── 事件监听 ──

  // 节点选中
  graph.on('node:select' as string, ({ nodeData }: { nodeData: Record<string, unknown> }) => {
    emit('selectNode', nodeData as unknown as TreeNode);
  });

  // 分支选中
  graph.on('branch:select' as string, ({ branchData }: { branchData: Record<string, unknown> }) => {
    emit('selectNode', branchData as unknown as ConditionBranch);
  });

  // 添加节点
  graph.on(
    'addNode:select' as string,
    ({ parentId, nodeType }: { parentId: string; nodeType: string }) => {
      emit('addNode', parentId, nodeType);
    },
  );

  // 删除节点
  graph.on('node:delete' as string, ({ nodeId }: { nodeId: string }) => {
    emit('deleteNode', nodeId);
  });

  // 删除分支
  graph.on('branch:delete' as string, ({ branchId }: { branchId: string }) => {
    emit('deleteConditionBranch', branchId);
  });

  // 添加条件分支
  graph.on('condition:addBranch' as string, ({ nodeId }: { nodeId: string }) => {
    emit('addConditionBranch', nodeId);
  });

  // 移动条件分支（排序）
  graph.on('branch:move' as string, ({ conditionNodeId, branchId, direction }: { conditionNodeId: string; branchId: string; direction: 'left' | 'right' }) => {
    emit('moveBranch', conditionNodeId, branchId, direction);
  });

  // 缩放同步
  graph.on('scale', ({ sx }: { sx: number }) => {
    zoom.value = sx;
  });

  graphRef.value = graph;

  // 首次渲染
  renderTree();
}

// ── 渲染 ──
function renderTree() {
  if (!graphRef.value) return;
  syncGraphFromTree(graphRef.value, props.flowTree);
}

// ── 缩放 ──
function zoomIn() {
  if (!graphRef.value) return;
  graphRef.value.zoom(0.1);
  zoom.value = graphRef.value.zoom();
}

function zoomOut() {
  if (!graphRef.value) return;
  graphRef.value.zoom(-0.1);
  zoom.value = graphRef.value.zoom();
}

function zoomFit() {
  if (!graphRef.value) return;
  graphRef.value.zoomToFit({ padding: 40, maxScale: 1.5 });
  zoom.value = graphRef.value.zoom();
}

// ── Watchers ──
watch(
  () => props.flowTree,
  () => {
    nextTick(() => renderTree());
  },
  { deep: true },
);

watch(
  () => props.selectedNodeId,
  (newId, oldId) => {
    if (!graphRef.value) return;
    highlightNode(graphRef.value, newId, oldId ?? null);
  },
);

// ── Lifecycle ──
onMounted(() => {
  initGraph();
});

onBeforeUnmount(() => {
  graphRef.value?.dispose();
});

// 暴露方法供外部使用
defineExpose({
  zoomIn,
  zoomOut,
  zoomFit,
  getGraph: () => graphRef.value,
});
</script>

<style scoped>
.dd-designer {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.dd-designer-canvas {
  width: 100%;
  height: 100%;
}

.dd-zoom-toolbar {
  position: absolute;
  bottom: 16px;
  left: 16px;
  display: flex;
  align-items: center;
  gap: 4px;
  background: rgba(255, 255, 255, 0.95);
  border-radius: 8px;
  padding: 4px 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  z-index: 10;
  user-select: none;
}

.dd-zoom-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: none;
  background: transparent;
  cursor: pointer;
  border-radius: 4px;
  color: #595959;
  font-size: 14px;
  transition: all 0.2s;
}

.dd-zoom-btn:hover {
  background: #f0f0f0;
  color: #1677ff;
}

.dd-zoom-value {
  min-width: 40px;
  text-align: center;
  font-size: 12px;
  color: #595959;
  font-variant-numeric: tabular-nums;
}
</style>
