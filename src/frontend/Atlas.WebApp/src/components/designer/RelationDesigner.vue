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
        <a-divider type="vertical" />
        <span>{{ t("relationDesigner.viewLabel") }}</span>
        <a-select
          v-model:value="selectedViewId"
          style="width: 220px"
          :options="viewOptions"
          @change="handleViewChange"
        />
        <a-button @click="openCreateViewModal">
          {{ t("relationDesigner.createView") }}
        </a-button>
        <a-button :disabled="!isCustomViewSelected" @click="saveCurrentLayoutToView">
          {{ t("relationDesigner.saveViewLayout") }}
        </a-button>
        <a-popconfirm
          :title="t('relationDesigner.deleteViewConfirm')"
          :disabled="!isCustomViewSelected"
          @confirm="deleteCurrentView"
        >
          <a-button danger :disabled="!isCustomViewSelected">
            {{ t("relationDesigner.deleteView") }}
          </a-button>
        </a-popconfirm>
        <a-button @click="relationDrawerOpen = true">
          {{ t("relationDesigner.relationList") }}
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
      :source-fields="pendingEdge.sourceFields"
      :target-fields="pendingEdge.targetFields"
      :initial-value="pendingEdge.definition"
      @confirm="onRelationConfirm"
      @cancel="onRelationCancel"
    />

    <a-drawer
      v-model:open="relationDrawerOpen"
      :title="t('relationDesigner.relationList')"
      width="560"
    >
      <a-empty v-if="relationRows.length === 0" :description="t('relationDesigner.noRelations')" />
      <a-table
        v-else
        :pagination="false"
        :data-source="relationRows"
        :columns="relationColumns"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'actions'">
            <a-space :size="4">
              <a-button size="small" type="link" @click="editRelationByRow(record)">
                {{ t("common.edit") }}
              </a-button>
              <a-popconfirm
                :title="t('relationDesigner.deleteRelationConfirm')"
                @confirm="removeRelationById(record.id)"
              >
                <a-button size="small" type="link" danger>
                  {{ t("common.delete") }}
                </a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-drawer>

    <a-modal
      v-model:open="createViewModalOpen"
      :title="t('relationDesigner.createViewModalTitle')"
      @ok="createViewFromCurrentLayout"
    >
      <a-input
        v-model:value="newViewName"
        :placeholder="t('relationDesigner.createViewNamePlaceholder')"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";
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
import type { DynamicFieldDefinition, DynamicRelationDefinition } from "@/types/dynamic-tables";
import {
  getAppScopedDynamicTables,
  getDynamicTableFieldsBatch,
  getDynamicTableRelations,
  setDynamicTableRelations,
  type AppScopedDynamicTableListItem
} from "@/services/dynamic-tables";
import TableEntityNode from "./TableEntityNode.vue";
import RelationConfigModal from "./RelationConfigModal.vue";

const OVERVIEW_VIEW_ID = "__overview__";

const props = defineProps<{
  appId?: string;
  initialViewId?: string;
}>();

const { t } = useI18n();
const { fitView } = useVueFlow();

// ---- 状态 ----
const loadingTables = ref(false);
const saving = ref(false);
const searchKeyword = ref("");
const allTables = ref<AppScopedDynamicTableListItem[]>([]);

const nodes = ref<Node[]>([]);
const edges = ref<Edge[]>([]);
const tableFieldCache = ref<Map<string, DynamicFieldDefinition[]>>(new Map());

// 待配置的边（连接时弹窗）
interface PendingEdge {
  id: string;
  sourceTableKey: string;
  targetTableKey: string;
  sourceFields: DynamicFieldDefinition[];
  targetFields: DynamicFieldDefinition[];
  editingEdgeId?: string;
  definition?: DynamicRelationDefinition;
}
const pendingEdge = ref<PendingEdge | null>(null);
const configModalOpen = ref(false);
const relationDrawerOpen = ref(false);
const createViewModalOpen = ref(false);
const newViewName = ref("");

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
const allRelationEdges = ref<Edge[]>([]);

interface RelationLayoutView {
  id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  layout: {
    nodes: Array<{ tableKey: string; x: number; y: number }>;
  };
}
const relationViews = ref<RelationLayoutView[]>([]);
const selectedViewId = ref<string>(OVERVIEW_VIEW_ID);

const isCustomViewSelected = computed(
  () => selectedViewId.value !== OVERVIEW_VIEW_ID && relationViews.value.some((view) => view.id === selectedViewId.value)
);
const viewOptions = computed(() => [
  { label: t("relationDesigner.overviewViewName"), value: OVERVIEW_VIEW_ID },
  ...relationViews.value.map((view) => ({ label: view.name, value: view.id }))
]);
const relationColumns = computed(() => [
  { title: t("relationDesigner.sourceTable"), dataIndex: "source", key: "source" },
  { title: t("relationDesigner.sourceField"), dataIndex: "sourceField", key: "sourceField" },
  { title: t("relationDesigner.targetTable"), dataIndex: "target", key: "target" },
  { title: t("relationDesigner.targetField"), dataIndex: "targetField", key: "targetField" },
  { title: t("relationDesigner.relationType"), dataIndex: "relationLabel", key: "relationLabel" },
  { title: t("common.actions"), key: "actions", width: 130 }
]);
const relationRows = computed(() =>
  (edges.value as unknown as Edge[]).map((edge) => {
    const definition = edge.data?.definition as DynamicRelationDefinition | undefined;
    return {
      id: edge.id,
      source: edge.source,
      target: edge.target,
      sourceField: definition?.sourceField ?? "",
      targetField: definition?.targetField ?? "",
      relationLabel: buildEdgeLabel(definition ?? emptyRelationDefinition(edge.target)),
      definition
    };
  })
);

// ---- 初始化 ----
onMounted(async () => {
  await loadTables();
});

watch(
  () => props.appId,
  () => {
    clearCanvas();
    void loadTables();
  }
);

watch(
  () => props.initialViewId,
  (nextViewId) => {
    if (!nextViewId) {
      return;
    }
    if (nextViewId === selectedViewId.value) {
      return;
    }
    if (nextViewId === OVERVIEW_VIEW_ID) {
      selectedViewId.value = OVERVIEW_VIEW_ID;
      applyOverviewLayout();
      return;
    }
    const view = relationViews.value.find((item) => item.id === nextViewId);
    if (view) {
      selectedViewId.value = view.id;
      applyViewLayout(view);
    }
  }
);

async function loadTables() {
  if (!props.appId) {
    allTables.value = [];
    return;
  }
  loadingTables.value = true;
  try {
    allTables.value = await getAppScopedDynamicTables(props.appId);
    await bootstrapRelationEdges();
    loadRelationViews();
    const preferredViewId = props.initialViewId?.trim();
    if (preferredViewId === OVERVIEW_VIEW_ID) {
      selectedViewId.value = OVERVIEW_VIEW_ID;
      applyOverviewLayout();
      return;
    }
    if (preferredViewId) {
      const preferred = relationViews.value.find((item) => item.id === preferredViewId);
      if (preferred) {
        selectedViewId.value = preferred.id;
        applyViewLayout(preferred);
        return;
      }
    }
    if (relationViews.value.length > 0) {
      selectedViewId.value = relationViews.value[0].id;
      applyViewLayout(relationViews.value[0]);
      return;
    }
    selectedViewId.value = OVERVIEW_VIEW_ID;
    applyOverviewLayout();
  } catch (error) {
    message.error((error as Error).message || t("relationDesigner.loadFailed"));
  } finally {
    loadingTables.value = false;
  }
}

async function bootstrapRelationEdges() {
  if (allTables.value.length === 0) {
    allRelationEdges.value = [];
    return;
  }

  const tableKeys = allTables.value.map((table) => table.tableKey);
  const relationPairs = await Promise.all(
    tableKeys.map(async (tableKey) => [tableKey, await loadRelationsForTable(tableKey)] as const)
  );

  const sourceToRelations = new Map<string, DynamicRelationDefinition[]>();
  const relatedTableKeySet = new Set<string>();
  for (const [tableKey, relations] of relationPairs) {
    sourceToRelations.set(tableKey, relations);
    for (const relation of relations) {
      relatedTableKeySet.add(relation.relatedTableKey);
    }
  }

  const edgeStore: Edge[] = [];
  for (const table of allTables.value) {
    const sourceTableKey = table.tableKey;
    const relations = sourceToRelations.get(sourceTableKey) ?? [];
    for (const relation of relations) {
      if (!allTables.value.some((item) => item.tableKey === relation.relatedTableKey)) {
        continue;
      }
      const edgeId = buildRelationIdentity(sourceTableKey, relation.relatedTableKey, relation);
      if (edgeStore.some((edge) => edge.id === edgeId)) {
        continue;
      }
      edgeStore.push({
        id: edgeId,
        source: sourceTableKey,
        target: relation.relatedTableKey,
        label: buildEdgeLabel(relation),
        markerEnd: MarkerType.ArrowClosed,
        data: { definition: relation }
      } as Edge);
    }
  }
  allRelationEdges.value = edgeStore;
}

function applyOverviewLayout() {
  const relatedTableKeySet = new Set<string>();
  for (const edge of allRelationEdges.value) {
    relatedTableKeySet.add(edge.source);
    relatedTableKeySet.add(edge.target);
  }
  const modelTables = allTables.value.filter((table) => relatedTableKeySet.has(table.tableKey));
  if (modelTables.length === 0) {
    nodes.value = [];
    edges.value = [];
    return;
  }
  const columns = Math.max(2, Math.ceil(Math.sqrt(modelTables.length)));
  const nextNodes = modelTables.map((table, index) => {
    const col = index % columns;
    const row = Math.floor(index / columns);
    return {
      id: table.tableKey,
      type: "tableNode",
      position: { x: 60 + col * 320, y: 40 + row * 220 },
      data: { table }
    } as Node;
  });
  nodes.value = nextNodes as typeof nodes.value;
  syncCanvasEdgesByVisibleNodes();
  void Promise.resolve().then(() => fitView());
}

function getVisibleTableKeySet(): Set<string> {
  const visible = new Set<string>();
  for (const node of nodes.value as unknown as Node[]) {
    visible.add(String(node.id));
  }
  return visible;
}

function syncCanvasEdgesByVisibleNodes() {
  const visible = getVisibleTableKeySet();
  const edgeStore = allRelationEdges.value as unknown as Array<{ source: string; target: string } & Edge>;
  const filtered = edgeStore.filter((edge) => visible.has(edge.source) && visible.has(edge.target));
  edges.value = filtered as unknown as typeof edges.value;
}

function getViewStorageKey(): string {
  return `atlas_relation_views_${props.appId ?? "default"}`;
}

function loadRelationViews() {
  relationViews.value = [];
  if (!props.appId) {
    return;
  }
  try {
    const raw = localStorage.getItem(getViewStorageKey());
    if (!raw) {
      return;
    }
    const parsed = JSON.parse(raw) as RelationLayoutView[];
    if (!Array.isArray(parsed)) {
      return;
    }
    relationViews.value = parsed.filter((view) => view && typeof view.id === "string" && typeof view.name === "string");
  } catch {
    relationViews.value = [];
  }
}

function saveRelationViews() {
  if (!props.appId) {
    return;
  }
  localStorage.setItem(getViewStorageKey(), JSON.stringify(relationViews.value));
}

function buildLayoutFromCurrentNodes() {
  return {
    nodes: (nodes.value as unknown as Node[]).map((node) => ({
      tableKey: String(node.id),
      x: node.position.x,
      y: node.position.y
    }))
  };
}

function applyViewLayout(view: RelationLayoutView) {
  const tableMap = new Map(allTables.value.map((table) => [table.tableKey, table]));
  const nextNodes: Node[] = view.layout.nodes
    .map((item) => {
      const table = tableMap.get(item.tableKey);
      if (!table) {
        return null;
      }
      return {
        id: table.tableKey,
        type: "tableNode",
        position: { x: item.x, y: item.y },
        data: { table }
      } as Node;
    })
    .filter((node): node is Node => node !== null);
  nodes.value = nextNodes as typeof nodes.value;
  syncCanvasEdgesByVisibleNodes();
  void Promise.resolve().then(() => fitView());
}

function handleViewChange(viewId: string) {
  if (viewId === OVERVIEW_VIEW_ID) {
    applyOverviewLayout();
    return;
  }
  const view = relationViews.value.find((item) => item.id === viewId);
  if (!view) {
    selectedViewId.value = OVERVIEW_VIEW_ID;
    applyOverviewLayout();
    return;
  }
  applyViewLayout(view);
}

function openCreateViewModal() {
  newViewName.value = "";
  createViewModalOpen.value = true;
}

function createViewFromCurrentLayout() {
  const name = newViewName.value.trim();
  if (!name) {
    message.warning(t("relationDesigner.createViewNameRequired"));
    return;
  }
  if (relationViews.value.some((view) => view.name === name)) {
    message.warning(t("relationDesigner.createViewNameDuplicate"));
    return;
  }
  const now = new Date().toISOString();
  const view: RelationLayoutView = {
    id: `view_${Date.now()}`,
    name,
    createdAt: now,
    updatedAt: now,
    layout: buildLayoutFromCurrentNodes()
  };
  relationViews.value = [view, ...relationViews.value];
  saveRelationViews();
  selectedViewId.value = view.id;
  createViewModalOpen.value = false;
  message.success(t("relationDesigner.createViewSuccess"));
}

function saveCurrentLayoutToView() {
  if (!isCustomViewSelected.value) {
    message.warning(t("relationDesigner.saveViewSelectFirst"));
    return;
  }
  const index = relationViews.value.findIndex((view) => view.id === selectedViewId.value);
  if (index < 0) {
    return;
  }
  relationViews.value[index] = {
    ...relationViews.value[index],
    updatedAt: new Date().toISOString(),
    layout: buildLayoutFromCurrentNodes()
  };
  relationViews.value = [...relationViews.value];
  saveRelationViews();
  message.success(t("relationDesigner.saveViewSuccess"));
}

function deleteCurrentView() {
  if (!isCustomViewSelected.value) {
    return;
  }
  relationViews.value = relationViews.value.filter((view) => view.id !== selectedViewId.value);
  saveRelationViews();
  selectedViewId.value = OVERVIEW_VIEW_ID;
  applyOverviewLayout();
  message.success(t("relationDesigner.deleteViewSuccess"));
}

async function ensureTableFields(tableKeys: string[]) {
  const missing = tableKeys.filter((tableKey) => !tableFieldCache.value.has(tableKey));
  if (missing.length === 0) {
    return;
  }
  const batch = await getDynamicTableFieldsBatch(missing);
  for (const [tableKey, fields] of Object.entries(batch)) {
    tableFieldCache.value.set(tableKey, fields);
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
  const canvasKeys = new Set<string>();
  for (const node of nodes.value) {
    canvasKeys.add(String(node.id));
  }

  // 加载新表的出向关系
  const outgoing = await loadRelationsForTable(newTableKey);
  for (const rel of outgoing) {
    if (!canvasKeys.has(rel.relatedTableKey)) continue;
    const edgeId = buildRelationIdentity(newTableKey, rel.relatedTableKey, rel);
    if (edges.value.some((e) => e.id === edgeId)) continue;
    const newEdge = {
      id: edgeId,
      source: newTableKey,
      target: rel.relatedTableKey,
      label: buildEdgeLabel(rel),
      markerEnd: MarkerType.ArrowClosed,
      data: { definition: rel }
    } as Edge;
    (edges.value as unknown as Edge[]).push(newEdge);
  }

  // 检查画布上其他表是否有指向新表的关系
  for (const key of canvasKeys) {
    if (key === newTableKey) continue;
    const rels = await loadRelationsForTable(key);
    for (const rel of rels) {
      if (rel.relatedTableKey !== newTableKey) continue;
      const edgeId = buildRelationIdentity(key, newTableKey, rel);
      if (edges.value.some((e) => e.id === edgeId)) continue;
      const newEdge = {
        id: edgeId,
        source: key,
        target: newTableKey,
        label: buildEdgeLabel(rel),
        markerEnd: MarkerType.ArrowClosed,
        data: { definition: rel }
      } as Edge;
      (edges.value as unknown as Edge[]).push(newEdge);
    }
  }
}

// ---- 拖拽添加节点 ----
function onDragStart(e: DragEvent, tbl: AppScopedDynamicTableListItem) {
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

  const newNode = {
    id: tableKey,
    type: "tableNode",
    position: { x, y },
    data: { table: tbl }
  } as Node;
  (nodes.value as unknown as Node[]).push(newNode);
  void syncEdgesForNewNode(tableKey);
}

// ---- 连线触发关系配置弹窗 ----
async function onConnect(connection: Connection) {
  if (!connection.source || !connection.target) return;
  if (connection.source === connection.target) return;

  try {
    await ensureTableFields([connection.source, connection.target]);
  } catch (error) {
    message.error((error as Error).message || t("relationDesigner.loadFieldsFailed"));
    return;
  }

  const sourceFields = tableFieldCache.value.get(connection.source) ?? [];
  const targetFields = tableFieldCache.value.get(connection.target) ?? [];
  if (sourceFields.length === 0 || targetFields.length === 0) {
    message.error(t("relationDesigner.fieldsRequired"));
    return;
  }

  const edgeId = `${connection.source}-${connection.target}-${Date.now()}`;
  pendingEdge.value = {
    id: edgeId,
    sourceTableKey: connection.source,
    targetTableKey: connection.target,
    sourceFields,
    targetFields
  };
  configModalOpen.value = true;
}

function onRelationConfirm(definition: DynamicRelationDefinition) {
  if (!pendingEdge.value) return;

  const label = buildEdgeLabel(definition);
  const editingEdgeId = pendingEdge.value.editingEdgeId;
  const relationIdentity = buildRelationIdentity(
    pendingEdge.value.sourceTableKey,
    pendingEdge.value.targetTableKey,
    definition
  );
  if (edges.value.some((edge) => edge.id === relationIdentity && edge.id !== editingEdgeId)) {
    message.warning(t("relationDesigner.duplicateRelation"));
    return;
  }

  const newEdge = {
    id: relationIdentity,
    source: pendingEdge.value.sourceTableKey,
    target: pendingEdge.value.targetTableKey,
    label,
    markerEnd: MarkerType.ArrowClosed,
    data: { definition }
  } as Edge;
  const relationEdgeStore = allRelationEdges.value as unknown as Edge[];
  const existingIndex = relationEdgeStore.findIndex((edge) => edge.id === relationIdentity);
  if (existingIndex >= 0) {
    relationEdgeStore.splice(existingIndex, 1, newEdge);
  } else {
    relationEdgeStore.push(newEdge);
  }
  if (editingEdgeId) {
    const edgeList = edges.value as unknown as Edge[];
    const index = edgeList.findIndex((edge) => edge.id === editingEdgeId);
    if (index >= 0) {
      edgeList.splice(index, 1, newEdge);
    } else {
      edgeList.push(newEdge);
    }
  } else {
    (edges.value as unknown as Edge[]).push(newEdge);
  }
  pendingEdge.value = null;
  configModalOpen.value = false;
}

function onRelationCancel() {
  pendingEdge.value = null;
  configModalOpen.value = false;
}

// 点击边，可重新编辑
async function onEdgeClick(params: { edge: Edge }) {
  const edge = params.edge;
  try {
    await ensureTableFields([edge.source, edge.target]);
  } catch (error) {
    message.error((error as Error).message || t("relationDesigner.loadFieldsFailed"));
    return;
  }

  pendingEdge.value = {
    id: `${edge.source}-${edge.target}-${Date.now()}`,
    sourceTableKey: edge.source,
    targetTableKey: edge.target,
    sourceFields: tableFieldCache.value.get(edge.source) ?? [],
    targetFields: tableFieldCache.value.get(edge.target) ?? [],
    editingEdgeId: edge.id,
    definition: edge.data?.definition as DynamicRelationDefinition | undefined
  };
  configModalOpen.value = true;
}

async function editRelationByRow(row: {
  id: string;
  source: string;
  target: string;
  definition?: DynamicRelationDefinition;
}) {
  try {
    await ensureTableFields([row.source, row.target]);
  } catch (error) {
    message.error((error as Error).message || t("relationDesigner.loadFieldsFailed"));
    return;
  }
  pendingEdge.value = {
    id: `${row.source}-${row.target}-${Date.now()}`,
    sourceTableKey: row.source,
    targetTableKey: row.target,
    sourceFields: tableFieldCache.value.get(row.source) ?? [],
    targetFields: tableFieldCache.value.get(row.target) ?? [],
    editingEdgeId: row.id,
    definition: row.definition
  };
  configModalOpen.value = true;
}

function removeRelationById(edgeId: string) {
  const edgeList = edges.value as unknown as Edge[];
  const index = edgeList.findIndex((edge) => edge.id === edgeId);
  if (index >= 0) {
    edgeList.splice(index, 1);
    const relationEdgeStore = allRelationEdges.value as unknown as Edge[];
    const relationIndex = relationEdgeStore.findIndex((edge) => edge.id === edgeId);
    if (relationIndex >= 0) {
      relationEdgeStore.splice(relationIndex, 1);
    }
    message.success(t("relationDesigner.deleteRelationSuccess"));
  }
}

function removeNode(nodeId: string) {
  const filteredNodes = (nodes.value as unknown as Node[]).filter((n) => n.id !== nodeId);
  const filteredEdges = (edges.value as unknown as Edge[]).filter(
    (e) => e.source !== nodeId && e.target !== nodeId
  );
  nodes.value = filteredNodes as typeof nodes.value;
  edges.value = filteredEdges as typeof edges.value;
  relationsCache.value.delete(nodeId);
}

function clearCanvas() {
  nodes.value = [];
  edges.value = [];
  pendingEdge.value = null;
  configModalOpen.value = false;
}

// ---- 保存 ----
async function handleSave() {
  if (pendingEdge.value) {
    message.warning(t("relationDesigner.completeConfigFirst"));
    return;
  }

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
    allRelationEdges.value = [...(edges.value as unknown as Edge[])];

    // 逐表顺序保存，保证失败时可定位到具体表
    const canvasTableKeys: string[] = [];
    for (const node of nodes.value as unknown as Node[]) {
      canvasTableKeys.push(String(node.id));
    }
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

function buildRelationIdentity(sourceTableKey: string, targetTableKey: string, def: DynamicRelationDefinition): string {
  const relationType = def.relationType?.trim() || "MasterDetail";
  const sourceField = def.sourceField?.trim() || "";
  const targetField = def.targetField?.trim() || "";
  return `${sourceTableKey}->${targetTableKey}:${sourceField}:${targetField}:${relationType}`;
}

function emptyRelationDefinition(relatedTableKey: string): DynamicRelationDefinition {
  return {
    relatedTableKey,
    sourceField: "",
    targetField: "",
    relationType: "MasterDetail",
    multiplicity: "OneToMany"
  };
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
