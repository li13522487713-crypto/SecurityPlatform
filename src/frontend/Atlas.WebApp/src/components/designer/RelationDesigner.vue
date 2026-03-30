<template>
  <div class="relation-designer">
    <!-- 工具栏 -->
    <div class="rd-toolbar">
      <a-space wrap>
        <span class="rd-title">{{ t("relationDesigner.title") }}</span>
        <a-divider type="vertical" />
        <a-button
          type="primary"
          :loading="saving"
          @click="handleSave"
        >
          {{ t("relationDesigner.save") }}
        </a-button>
        <a-button @click="fitView">
          <template #icon><FullscreenExitOutlined /></template>
          {{ t("relationDesigner.fitView") }}
        </a-button>
        <a-button danger @click="clearCanvas">
          {{ t("relationDesigner.clearCanvas") }}
        </a-button>
      </a-space>
    </div>

    <!-- 主体：侧栏 + 画布 -->
    <div class="rd-body">
      <!-- 左侧实体列表 -->
      <div class="rd-sidebar">
        <div class="sidebar-title">{{ t("relationDesigner.entities") }}</div>
        <a-input
          v-model:value="searchKeyword"
          :placeholder="t('relationDesigner.searchPlaceholder')"
          size="small"
          style="margin: 8px"
          allow-clear
        />
        <a-spin :spinning="loadingTables">
          <div class="entity-list">
            <div
              v-for="tbl in filteredTables"
              :key="tbl.tableKey"
              class="entity-item"
              draggable="true"
              @dragstart="(e) => onDragStart(e, tbl)"
            >
              <TableOutlined class="entity-icon" />
              <span>{{ tbl.displayName || tbl.tableKey }}</span>
              <a-tag size="small" style="margin-left: auto">{{ tbl.tableKey }}</a-tag>
            </div>
            <a-empty
              v-if="filteredTables.length === 0 && !loadingTables"
              :description="t('common.noData')"
              :image="false"
            />
          </div>
        </a-spin>
      </div>

      <!-- 右侧 VueFlow 画布 -->
      <div class="rd-canvas" @dragover.prevent @drop="onDrop">
        <VueFlow
          v-model:nodes="nodes"
          v-model:edges="edges"
          :default-viewport="{ zoom: 1 }"
          :min-zoom="0.3"
          :max-zoom="2"
          fit-view-on-init
          @connect="onConnect"
          @edge-click="onEdgeClick"
        >
          <Background pattern-color="#aaa" :gap="16" />
          <Controls />
          <MiniMap />

          <!-- 自定义节点 -->
          <template #node-tableNode="nodeProps">
            <TableEntityNode
              v-bind="nodeProps"
              @remove="removeNode(nodeProps.id)"
            />
          </template>
        </VueFlow>

        <!-- 提示 -->
        <div v-if="nodes.length === 0" class="canvas-hint">
          <InboxOutlined style="font-size: 32px; color: #ccc" />
          <p>{{ t("relationDesigner.dropHint") }}</p>
        </div>
      </div>
    </div>

    <!-- 关系配置弹窗 -->
    <RelationConfigModal
      v-if="pendingEdge !== null"
      v-model:open="configModalOpen"
      :source-table-key="pendingEdge.sourceTableKey"
      :target-table-key="pendingEdge.targetTableKey"
      :initial-value="pendingEdge.definition"
      @confirm="onRelationConfirm"
      @cancel="onRelationCancel"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import {
  TableOutlined,
  FullscreenExitOutlined,
  InboxOutlined
} from "@ant-design/icons-vue";
import {
  VueFlow,
  useVueFlow,
  MarkerType
} from "@vue-flow/core";
import { Background } from "@vue-flow/background";
import { Controls } from "@vue-flow/controls";
import { MiniMap } from "@vue-flow/minimap";
import type { Connection, Edge, Node } from "@vue-flow/core";
import "@vue-flow/core/dist/style.css";
import "@vue-flow/core/dist/theme-default.css";
import "@vue-flow/controls/dist/style.css";
import "@vue-flow/minimap/dist/style.css";
import type { DynamicTableListItem, DynamicRelationDefinition } from "@/types/dynamic-tables";
import { getAllDynamicTables, getDynamicTableRelations, setDynamicTableRelations } from "@/services/dynamic-tables";
import TableEntityNode from "./TableEntityNode.vue";
import RelationConfigModal from "./RelationConfigModal.vue";

const props = defineProps<{
  appId?: string;
}>();

const { t } = useI18n();
const { fitView } = useVueFlow();

// ---- 状态 ----
const loadingTables = ref(false);
const saving = ref(false);
const searchKeyword = ref("");
const allTables = ref<DynamicTableListItem[]>([]);

const nodes = ref<Node[]>([]);
const edges = ref<Edge[]>([]);

// 待配置的边（连接时弹窗）
interface PendingEdge {
  id: string;
  sourceTableKey: string;
  targetTableKey: string;
  definition?: DynamicRelationDefinition;
}
const pendingEdge = ref<PendingEdge | null>(null);
const configModalOpen = ref(false);

// ---- 计算 ----
const filteredTables = computed(() => {
  const kw = searchKeyword.value.trim().toLowerCase();
  if (!kw) return allTables.value;
  return allTables.value.filter(
    (t) =>
      t.tableKey.toLowerCase().includes(kw) ||
      (t.displayName ?? "").toLowerCase().includes(kw)
  );
});

// 已加载关系缓存：tableKey -> relations
const relationsCache = ref<Map<string, DynamicRelationDefinition[]>>(new Map());

// ---- 初始化 ----
onMounted(async () => {
  await loadTables();
});

async function loadTables() {
  loadingTables.value = true;
  try {
    allTables.value = await getAllDynamicTables();
  } catch {
    message.error(t("relationDesigner.loadFailed"));
  } finally {
    loadingTables.value = false;
  }
}

/** 加载指定表的已有关系并放入缓存，返回该表的关系列表 */
async function loadRelationsForTable(tableKey: string): Promise<DynamicRelationDefinition[]> {
  if (relationsCache.value.has(tableKey)) {
    return relationsCache.value.get(tableKey)!;
  }
  try {
    const relations = await getDynamicTableRelations(tableKey);
    relationsCache.value.set(tableKey, relations);
    return relations;
  } catch {
    return [];
  }
}

/** 当画布上有新表节点时，添加与其他已在画布节点之间的已有关系连线 */
async function syncEdgesForNewNode(newTableKey: string) {
  const canvasKeys = new Set(nodes.value.map((n) => n.id));

  // 加载新表的出向关系
  const outgoing = await loadRelationsForTable(newTableKey);
  for (const rel of outgoing) {
    if (!canvasKeys.has(rel.relatedTableKey)) continue;
    const edgeId = `${newTableKey}-${rel.relatedTableKey}-existing`;
    if (edges.value.some((e) => e.id === edgeId)) continue;
    edges.value = [
      ...edges.value,
      {
        id: edgeId,
        source: newTableKey,
        target: rel.relatedTableKey,
        label: buildEdgeLabel(rel),
        markerEnd: MarkerType.ArrowClosed,
        data: { definition: rel }
      }
    ];
  }

  // 检查画布上其他表是否有指向新表的关系
  for (const key of canvasKeys) {
    if (key === newTableKey) continue;
    const rels = await loadRelationsForTable(key);
    for (const rel of rels) {
      if (rel.relatedTableKey !== newTableKey) continue;
      const edgeId = `${key}-${newTableKey}-existing`;
      if (edges.value.some((e) => e.id === edgeId)) continue;
      edges.value = [
        ...edges.value,
        {
          id: edgeId,
          source: key,
          target: newTableKey,
          label: buildEdgeLabel(rel),
          markerEnd: MarkerType.ArrowClosed,
          data: { definition: rel }
        }
      ];
    }
  }
}

// ---- 拖拽添加节点 ----
function onDragStart(e: DragEvent, tbl: DynamicTableListItem) {
  e.dataTransfer?.setData("tableKey", tbl.tableKey);
}

function onDrop(e: DragEvent) {
  const tableKey = e.dataTransfer?.getData("tableKey");
  if (!tableKey) return;

  const alreadyInCanvas = nodes.value.some((n) => n.id === tableKey);
  if (alreadyInCanvas) {
    message.warning(t("relationDesigner.alreadyOnCanvas"));
    return;
  }

  const tbl = allTables.value.find((t) => t.tableKey === tableKey);
  if (!tbl) return;

  const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
  const x = e.clientX - rect.left;
  const y = e.clientY - rect.top;

  const newNode: Node = {
    id: tableKey,
    type: "tableNode",
    position: { x, y },
    data: { table: tbl }
  };
  // @ts-expect-error TS2589: Vue Flow Node generic causes excessively deep type instantiation with Vue reactivity
  nodes.value.push(newNode);
  void syncEdgesForNewNode(tableKey);
}

// ---- 连线触发关系配置弹窗 ----
function onConnect(connection: Connection) {
  if (!connection.source || !connection.target) return;
  if (connection.source === connection.target) return;

  const edgeId = `${connection.source}-${connection.target}-${Date.now()}`;
  pendingEdge.value = {
    id: edgeId,
    sourceTableKey: connection.source,
    targetTableKey: connection.target
  };
  configModalOpen.value = true;
}

function onRelationConfirm(definition: DynamicRelationDefinition) {
  if (!pendingEdge.value) return;

  const label = buildEdgeLabel(definition);
  edges.value = [
    ...edges.value,
    {
      id: pendingEdge.value.id,
      source: pendingEdge.value.sourceTableKey,
      target: pendingEdge.value.targetTableKey,
      label,
      markerEnd: MarkerType.ArrowClosed,
      data: { definition }
    }
  ];
  pendingEdge.value = null;
  configModalOpen.value = false;
}

function onRelationCancel() {
  pendingEdge.value = null;
  configModalOpen.value = false;
}

// 点击边，可重新编辑
function onEdgeClick(params: { edge: Edge }) {
  const edge = params.edge;
  pendingEdge.value = {
    id: edge.id,
    sourceTableKey: edge.source,
    targetTableKey: edge.target,
    definition: edge.data?.definition as DynamicRelationDefinition | undefined
  };
  // 先移除旧边，弹窗确认后重新添加
  edges.value = edges.value.filter((e) => e.id !== edge.id);
  configModalOpen.value = true;
}

function removeNode(nodeId: string) {
  nodes.value = nodes.value.filter((n) => n.id !== nodeId);
  edges.value = edges.value.filter(
    (e) => e.source !== nodeId && e.target !== nodeId
  );
  relationsCache.value.delete(nodeId);
}

function clearCanvas() {
  nodes.value = [];
  edges.value = [];
  relationsCache.value.clear();
}

// ---- 保存 ----
async function handleSave() {
  saving.value = true;
  const errors: string[] = [];
  try {
    // 按源表聚合关系
    const byTable = new Map<string, DynamicRelationDefinition[]>();
    for (const edge of edges.value) {
      const def = edge.data?.definition as DynamicRelationDefinition | undefined;
      if (!def) continue; // 跳过未配置的边（不应出现，但防御性处理）
      const list = byTable.get(edge.source) ?? [];
      list.push(def);
      byTable.set(edge.source, list);
    }

    // 逐表顺序保存，保证失败时可定位到具体表
    const canvasTableKeys = nodes.value.map((n) => n.id);
    for (const key of canvasTableKeys) {
      try {
        await setDynamicTableRelations(key, { relations: byTable.get(key) ?? [] });
        // 清除本表缓存，下次重新加载
        relationsCache.value.delete(key);
      } catch (err) {
        errors.push(`${key}: ${(err as Error).message}`);
      }
    }

    if (errors.length === 0) {
      message.success(t("relationDesigner.saveSuccess"));
    } else {
      message.error(t("relationDesigner.savePartialFailed", { detail: errors.join(" | ") }));
    }
  } catch (err) {
    message.error((err as Error).message || t("relationDesigner.saveFailed"));
  } finally {
    saving.value = false;
  }
}

// ---- 辅助 ----
function buildEdgeLabel(def: DynamicRelationDefinition): string {
  const mult = def.multiplicity ?? "OneToMany";
  const labels: Record<string, string> = {
    OneToOne: "1:1",
    OneToMany: "1:N",
    ManyToMany: "M:N"
  };
  return `${labels[mult] ?? mult} (${def.relationType})`;
}
</script>

<style scoped>
.relation-designer {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #fff;
}

.rd-toolbar {
  padding: 8px 16px;
  border-bottom: 1px solid #f0f0f0;
  background: #fafafa;
  flex-shrink: 0;
}

.rd-title {
  font-weight: 600;
  font-size: 14px;
}

.rd-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.rd-sidebar {
  width: 240px;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  background: #fafafa;
  flex-shrink: 0;
}

.sidebar-title {
  padding: 12px 16px 4px;
  font-weight: 600;
  font-size: 13px;
  color: #555;
}

.entity-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.entity-item {
  padding: 7px 12px;
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: grab;
  font-size: 13px;
  color: #333;
  transition: background 0.15s;
}

.entity-item:hover {
  background: #e6f7ff;
}

.entity-icon {
  color: #1890ff;
  flex-shrink: 0;
}

.rd-canvas {
  flex: 1;
  position: relative;
  overflow: hidden;
}

.canvas-hint {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  text-align: center;
  color: #bbb;
  pointer-events: none;
  user-select: none;
}
</style>
