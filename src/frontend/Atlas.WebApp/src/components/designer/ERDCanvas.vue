<template>
  <div class="erd-layout">
    <div class="erd-sidebar">
      <div class="sidebar-header">
        数据实体
      </div>
      <a-spin :spinning="loading">
        <div class="entity-list">
          <div
            v-for="table in tables"
            :key="table.tableKey"
            class="entity-item"
            @mousedown="(e) => startDrag(table, e)"
          >
            <TableOutlined class="item-icon" />
            <div class="item-name">{{ table.displayName || table.tableKey }}</div>
            <a-button
              type="text"
              size="small"
              :title="t('erd.viewReferences')"
              @click.stop="openReferences(table.tableKey)"
            >
              <template #icon><LinkOutlined /></template>
            </a-button>
          </div>
          <a-empty v-if="tables.length === 0 && !loading" description="无可用实体" />
        </div>
      </a-spin>
    </div>
    <div class="erd-main">
      <div class="toolbar">
        <a-space>
          <a-button type="primary" :loading="saving" @click="saveConnections">保存关联</a-button>
          <a-button title="放大" @click="zoomIn"><ZoomInOutlined /></a-button>
          <a-button title="缩小" @click="zoomOut"><ZoomOutOutlined /></a-button>
          <a-button title="居中" @click="centerContent"><FullscreenExitOutlined /></a-button>
        </a-space>
      </div>
      <div ref="containerRef" class="erd-canvas-container"></div>
    </div>
  </div>

  <EntityReferencesDrawer v-model:open="referencesDrawerOpen" :table-key="selectedTableKey" />
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { TableOutlined, ZoomInOutlined, ZoomOutOutlined, FullscreenExitOutlined, LinkOutlined } from '@ant-design/icons-vue';
import { Graph } from '@antv/x6';
import { Dnd } from '@antv/x6-plugin-dnd';
import { register } from '@antv/x6-vue-shape';
import { message } from 'ant-design-vue';
import type { DynamicTableListItem } from '@/types/dynamic-tables';
import { getAllDynamicTables, getDynamicTableRelations, setDynamicTableRelations } from '@/services/dynamic-tables';
import ERDEntityNode from './ERDEntityNode.vue';
import EntityReferencesDrawer from './EntityReferencesDrawer.vue';

const { t } = useI18n();

const props = defineProps<{
  appId: string;
}>();

const containerRef = ref<HTMLElement | null>(null);
const tables = ref<DynamicTableListItem[]>([]);
const loading = ref(false);
const saving = ref(false);

const referencesDrawerOpen = ref(false);
const selectedTableKey = ref('');

const openReferences = (tableKey: string) => {
  selectedTableKey.value = tableKey;
  referencesDrawerOpen.value = true;
};

let graph: Graph;
let dnd: Dnd;

// Register Vue Component
register({
  shape: 'erd-entity',
  width: 220,
  height: 200, // Dynamic height could be handled differently
  component: ERDEntityNode,
  ports: {
    groups: {
      in: {
        position: 'left',
        attrs: { circle: { r: 4, magnet: true, stroke: '#5F95FF', fill: '#fff' } },
      },
      out: {
        position: 'right',
        attrs: { circle: { r: 4, magnet: true, stroke: '#5F95FF', fill: '#fff' } },
      },
    },
    items: [
      { id: 'port-in', group: 'in' },
      { id: 'port-out', group: 'out' },
    ],
  },
});

onMounted(async () => {
  await loadTables();
  initGraph();
});

onUnmounted(() => {
  if (graph) {
    graph.dispose();
  }
});

const loadTables = async () => {
  loading.value = true;
  try {
    tables.value = await getAllDynamicTables();
  } catch (err) {
    message.error("加载实体列表失败");
  } finally {
    loading.value = false;
  }
};

const initGraph = () => {
  if (!containerRef.value) return;

  graph = new Graph({
    container: containerRef.value,
    grid: true,
    panning: true,
    mousewheel: true,
    connecting: {
      snap: true,
      allowBlank: false,
      allowLoop: false,
      allowNode: false,
      router: 'manhattan',
      connector: {
        name: 'rounded',
        args: { radius: 8 },
      },
      createEdge() {
        return graph.createEdge({
          shape: 'edge',
          attrs: {
            line: {
              stroke: '#8f8f8f',
              strokeWidth: 2,
              targetMarker: {
                name: 'block',
                width: 12,
                height: 8,
              },
            },
          },
          labels: [
            {
              attrs: { label: { text: '1:N' } },
            },
          ],
        });
      },
    },
  });

  dnd = new Dnd({
    target: graph,
    scaled: false,
    validateNode: () => true,
  });
};

const startDrag = (table: DynamicTableListItem, e: MouseEvent) => {
  // Check if node already exists
  if (graph.getNodes().some(n => n.getData()?.table?.tableKey === table.tableKey)) {
    message.warning("实体已在画布中");
    return;
  }

  const node = graph.createNode({
    shape: 'erd-entity',
    data: { table },
  });
  dnd.start(node, e);
};

// Toolbar Actions
const zoomIn = () => graph?.zoom(0.1);
const zoomOut = () => graph?.zoom(-0.1);
const centerContent = () => graph?.centerContent();

const saveConnections = async () => {
  saving.value = true;
  try {
    // Collect all edges and map them to DynamicRelationDefinition
    const edges = graph.getEdges();
    
    // Group relationships by source table
    const relationsByTable = new Map<string, any[]>();
    
    tables.value.forEach(t => relationsByTable.set(t.tableKey, []));

    for (const edge of edges) {
      const sourceNode = edge.getSourceNode();
      const targetNode = edge.getTargetNode();
      
      if (!sourceNode || !targetNode) continue;

      const sourceTable = sourceNode.getData()?.table?.tableKey;
      const targetTable = targetNode.getData()?.table?.tableKey;

      if (sourceTable && targetTable) {
        // Here we build the payload
        // Note: For MVP we assume sourceField is Id and targetField is ${tableKey}Id
        if (!relationsByTable.has(sourceTable)) {
          relationsByTable.set(sourceTable, []);
        }
        
        relationsByTable.get(sourceTable)!.push({
          relatedTableKey: targetTable,
          sourceField: "id", // default assumption for now
          targetField: `${sourceTable}Id`, // default target foreign key
          relationType: "1:N",
          cascadeRule: null
        });
      }
    }

    // Save for all nodes in the canvas (or all loaded tables)
    // To be precise, we need to save the relations array per source table.
    const promises = [];
    for (const [tableKey, relations] of relationsByTable.entries()) {
      // Only update tables that are in canvas to avoid wiping others out unless intended
      if (graph.getNodes().some(n => n.getData()?.table?.tableKey === tableKey)) {
        promises.push(setDynamicTableRelations(tableKey, { relations }));
      }
    }
    
    await Promise.all(promises);
    message.success("实体关系保存成功");
  } catch (err) {
    message.error((err as Error).message || "保存实体关系失败");
  } finally {
    saving.value = false;
  }
};
</script>

<style scoped>
.erd-layout {
  display: flex;
  height: 100%;
  width: 100%;
  border-top: 1px solid #f0f0f0;
}

.erd-sidebar {
  width: 240px;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  background-color: #fafafa;
}

.sidebar-header {
  padding: 12px 16px;
  font-weight: 600;
  border-bottom: 1px solid #f0f0f0;
  background-color: #fff;
}

.entity-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;
}

.entity-item {
  padding: 8px 16px;
  display: flex;
  align-items: center;
  cursor: grab;
  color: #333;
  transition: background-color 0.2s;
}

.entity-item:hover {
  background-color: #e6f7ff;
}

.entity-item:active {
  cursor: grabbing;
}

.item-icon {
  margin-right: 8px;
  color: #1890ff;
}

.item-name {
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.erd-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  position: relative;
  overflow: hidden;
}

.toolbar {
  position: absolute;
  top: 16px;
  right: 16px;
  z-index: 10;
  background-color: white;
  padding: 8px;
  border-radius: 4px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.15);
}

.erd-canvas-container {
  flex: 1;
  width: 100%;
  height: 100%;
  outline: none;
}
</style>
