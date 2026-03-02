<template>
  <div class="dd-designer">
    <div ref="containerRef" class="dd-designer-canvas"></div>

    <!-- 缩放控制栏 -->
    <div class="dd-zoom-toolbar">
      <button class="dd-zoom-btn" @click="zoomOut" title="缩小（Ctrl + -）">
        <MinusOutlined />
      </button>
      <span class="dd-zoom-value">{{ zoomPercent }}%</span>
      <button class="dd-zoom-btn" @click="zoomIn" title="放大（Ctrl + +）">
        <PlusOutlined />
      </button>
      <button class="dd-zoom-btn" @click="zoomFit" title="适应画布（Ctrl + 0）">
        <CompressOutlined />
      </button>
    </div>

    <!-- 节点右键菜单 -->
    <a-dropdown
      v-model:open="nodeMenuVisible"
      :trigger="['contextmenu']"
      @open-change="handleNodeMenuOpenChange"
    >
      <div
        :style="{
          position: 'absolute',
          left: nodeMenuPos.x + 'px',
          top: nodeMenuPos.y + 'px',
          width: '1px',
          height: '1px'
        }"
      ></div>
      <template #overlay>
        <a-menu @click="handleNodeMenuClick">
          <a-menu-item key="edit">
            <EditOutlined /> 编辑节点
          </a-menu-item>
          <a-menu-item key="copy" :disabled="!contextNode">
            <CopyOutlined /> 复制节点 <span class="menu-shortcut">Ctrl+C</span>
          </a-menu-item>
          <a-menu-divider />
          <a-menu-item key="delete" :disabled="!contextNode || contextNode.nodeType === 'start' || contextNode.nodeType === 'end'" danger>
            <DeleteOutlined /> 删除节点 <span class="menu-shortcut">Delete</span>
          </a-menu-item>
          <a-menu-divider />
          <a-menu-item key="view-detail">
            <EyeOutlined /> 查看详情
          </a-menu-item>
        </a-menu>
      </template>
    </a-dropdown>

    <!-- 画布右键菜单 -->
    <a-dropdown
      v-model:open="canvasMenuVisible"
      :trigger="['contextmenu']"
      @open-change="handleCanvasMenuOpenChange"
    >
      <div
        :style="{
          position: 'absolute',
          left: canvasMenuPos.x + 'px',
          top: canvasMenuPos.y + 'px',
          width: '1px',
          height: '1px'
        }"
      ></div>
      <template #overlay>
        <a-menu @click="handleCanvasMenuClick">
          <a-menu-item key="paste" :disabled="!copiedNode">
            <CodeOutlined /> 粘贴节点 <span class="menu-shortcut">Ctrl+V</span>
          </a-menu-item>
          <a-menu-item key="select-all">
            <SelectOutlined /> 全选 <span class="menu-shortcut">Ctrl+A</span>
          </a-menu-item>
          <a-menu-divider />
          <a-menu-item key="export-json">
            <ExportOutlined /> 导出JSON
          </a-menu-item>
          <a-menu-divider />
          <a-menu-item key="clear" danger>
            <ClearOutlined /> 清空画布
          </a-menu-item>
        </a-menu>
      </template>
    </a-dropdown>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch, computed, nextTick } from 'vue';
import { message, Modal } from 'ant-design-vue';
import { Graph } from '@antv/x6';
import {
  MinusOutlined,
  PlusOutlined,
  CompressOutlined,
  EditOutlined,
  CopyOutlined,
  DeleteOutlined,
  EyeOutlined,
  CodeOutlined,
  SelectOutlined,
  ExportOutlined,
  ClearOutlined,
} from '@ant-design/icons-vue';
import type { ApprovalFlowTree, TreeNode, ConditionBranch, NodeType } from '@/types/approval-tree';
import { registerAllShapes } from './shapes/register';
import { syncGraphFromTree, highlightNode } from './sync';

// Simplified type to avoid TS2589 deep type instantiation on recursive TreeNode union
interface ContextMenuNode {
  id: string;
  nodeType: NodeType;
  label?: string;
  nodeName?: string;
  [key: string]: unknown;
}

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

// ── 右键菜单状态 ──
const nodeMenuVisible = ref(false);
const nodeMenuPos = ref({ x: 0, y: 0 });
const contextNode = ref<ContextMenuNode | null>(null);

const canvasMenuVisible = ref(false);
const canvasMenuPos = ref({ x: 0, y: 0 });

// ── 复制粘贴状态 ──
const copiedNode = ref<TreeNode | null>(null);

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

  // ── 右键菜单事件 ──

  // 节点右键菜单
  graph.on('node:contextmenu', ({ e, node }: { e: MouseEvent; node: any }) => {
    e.preventDefault();
    const nodeData = node.getData();
    contextNode.value = nodeData as ContextMenuNode;
    nodeMenuPos.value = { x: e.clientX, y: e.clientY };
    nodeMenuVisible.value = true;
    canvasMenuVisible.value = false;
  });

  // 画布右键菜单
  graph.on('blank:contextmenu', ({ e }: { e: MouseEvent }) => {
    e.preventDefault();
    canvasMenuPos.value = { x: e.clientX, y: e.clientY };
    canvasMenuVisible.value = true;
    nodeMenuVisible.value = false;
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

// ── 右键菜单处理 ──
function handleNodeMenuOpenChange(visible: boolean) {
  if (!visible) {
    contextNode.value = null;
  }
}

function handleCanvasMenuOpenChange(visible: boolean) {
  // 画布菜单关闭时不需要特殊处理
}

function handleNodeMenuClick({ key }: { key: string }) {
  nodeMenuVisible.value = false;

  switch (key) {
    case 'edit':
      handleEditNode();
      break;
    case 'copy':
      handleCopyNode();
      break;
    case 'delete':
      handleDeleteNode();
      break;
    case 'view-detail':
      handleViewNodeDetail();
      break;
  }
}

function handleCanvasMenuClick({ key }: { key: string }) {
  canvasMenuVisible.value = false;

  switch (key) {
    case 'paste':
      handlePasteNode();
      break;
    case 'select-all':
      handleSelectAll();
      break;
    case 'export-json':
      handleExportJson();
      break;
    case 'clear':
      handleClearCanvas();
      break;
  }
}

// ── 右键菜单操作 ──
function handleEditNode() {
  if (contextNode.value) {
    emit('selectNode', contextNode.value as any);
    message.success('已选中节点，可在右侧属性面板编辑');
  }
}

function handleCopyNode() {
  const node = contextNode.value || (props.selectedNodeId ? findNodeById(props.selectedNodeId) : null);

  if (!node) {
    message.warning('请先选择要复制的节点');
    return;
  }

  const nodeData = node as any;
  if (nodeData.nodeType === 'start' || nodeData.nodeType === 'end') {
    message.warning('开始和结束节点不能复制');
    return;
  }

  // 深拷贝节点数据
  if ('branches' in nodeData) {
    // 条件分支不支持复制
    message.warning('条件分支不支持复制');
    return;
  }

  copiedNode.value = JSON.parse(JSON.stringify(node)) as TreeNode;
  message.success('节点已复制');
}

function findNodeById(nodeId: string): any {
  // 简化实现：遍历树查找节点
  function traverse(node: any, depth = 0): any {
    if (depth > 50) return null; // 防止无限递归
    if (node.id === nodeId) return node;
    if (node.childNode) {
      const result = traverse(node.childNode, depth + 1);
      if (result) return result;
    }
    if (node.branches) {
      for (const branch of node.branches) {
        if (branch.id === nodeId) return branch;
        if (branch.childNode) {
          const result = traverse(branch.childNode, depth + 1);
          if (result) return result;
        }
      }
    }
    return null;
  }
  return traverse(props.flowTree.rootNode);
}

function handlePasteNode() {
  if (!copiedNode.value) {
    message.warning('剪贴板中没有节点');
    return;
  }

  // 生成新ID
  const nodeData = copiedNode.value as any;
  const nodeType = nodeData.nodeType || 'approve';

  // 粘贴到当前选中节点的后面，如果没有选中则粘贴到根节点
  const parentId = props.selectedNodeId || props.flowTree.rootNode.id;

  emit('addNode', parentId, nodeType);
  message.success('节点已粘贴');
}

function handleDeleteNode() {
  const node = contextNode.value || (props.selectedNodeId ? findNodeById(props.selectedNodeId) : null);

  if (!node) {
    message.warning('请先选择要删除的节点');
    return;
  }

  const nodeData = node as any;
  if (nodeData.nodeType === 'start' || nodeData.nodeType === 'end') {
    message.warning('开始和结束节点不能删除');
    return;
  }

  const nodeLabel = nodeData.label || '此节点';

  Modal.confirm({
    title: '确认删除',
    content: `确定要删除节点"${nodeLabel}"吗？`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    onOk() {
      if ('branches' in nodeData) {
        emit('deleteConditionBranch', node.id);
      } else {
        emit('deleteNode', node.id);
      }
      message.success('节点已删除');
    },
  });
}

function handleViewNodeDetail() {
  if (contextNode.value) {
    emit('selectNode', contextNode.value as any);
    message.info('查看节点详情');
  }
}

function handleSelectAll() {
  message.info('全选功能（流程树结构暂不支持多选）');
}

function handleExportJson() {
  const flowData = JSON.stringify(props.flowTree, null, 2);
  const blob = new Blob([flowData], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `approval-flow-${Date.now()}.json`;
  a.click();
  URL.revokeObjectURL(url);
  message.success('流程JSON已导出');
}

function handleClearCanvas() {
  Modal.confirm({
    title: '确认清空画布',
    content: '清空画布将删除所有节点（开始和结束节点除外），此操作不可恢复！',
    okText: '清空',
    okType: 'danger',
    cancelText: '取消',
    onOk() {
      // 这里需要通过emit通知父组件清空画布
      // 由于当前架构限制，我们只能提示用户手动清空
      message.warning('请通过删除各个节点来清空画布');
    },
  });
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

// ── 键盘快捷键 ──
function handleKeyDown(e: KeyboardEvent) {
  const target = e.target as HTMLElement;
  // 如果焦点在输入框中，不响应快捷键
  if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
    return;
  }

  const isCtrlOrCmd = e.ctrlKey || e.metaKey;

  if (isCtrlOrCmd && e.key === 'c') {
    e.preventDefault();
    handleCopyNode();
  } else if (isCtrlOrCmd && e.key === 'v') {
    e.preventDefault();
    handlePasteNode();
  } else if (e.key === 'Delete' || e.key === 'Backspace') {
    e.preventDefault();
    handleDeleteNode();
  } else if (isCtrlOrCmd && e.key === 'a') {
    e.preventDefault();
    handleSelectAll();
  } else if (isCtrlOrCmd && (e.key === '=' || e.key === '+')) {
    e.preventDefault();
    zoomIn();
  } else if (isCtrlOrCmd && (e.key === '-' || e.key === '_')) {
    e.preventDefault();
    zoomOut();
  } else if (isCtrlOrCmd && e.key === '0') {
    e.preventDefault();
    zoomFit();
  }
}

// ── Lifecycle ──
onMounted(() => {
  initGraph();
  window.addEventListener('keydown', handleKeyDown);
});

onBeforeUnmount(() => {
  graphRef.value?.dispose();
  window.removeEventListener('keydown', handleKeyDown);
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

/* 右键菜单快捷键提示 */
:deep(.menu-shortcut) {
  margin-left: 12px;
  font-size: 12px;
  color: #8c8c8c;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
}
</style>
