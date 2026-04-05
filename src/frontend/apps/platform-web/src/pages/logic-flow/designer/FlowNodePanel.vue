<template>
  <div class="flow-node-panel">
    <a-input
      v-model:value="search"
      allow-clear
      class="node-search"
      :placeholder="t('logicFlow.designerUi.nodePanel.searchPlaceholder')"
    >
      <template #prefix><SearchOutlined style="color: rgba(0, 0, 0, 0.25)" /></template>
    </a-input>
    <a-collapse v-model:active-key="openKeys" :bordered="false" class="node-collapse">
      <a-collapse-panel v-for="cat in filteredCategories" :key="cat.key" :header="cat.title">
        <div class="node-cards">
          <a-card
            v-for="node in cat.nodes"
            :key="node.id"
            size="small"
            hoverable
            class="node-card"
            draggable="true"
            @dragstart="onDragStart(node, $event)"
          >
            <div class="node-card-head">
              <component :is="iconFor(cat.key)" class="node-icon" />
              <span class="node-name">{{ node.name }}</span>
            </div>
            <div class="node-desc">{{ node.description }}</div>
          </a-card>
        </div>
      </a-collapse-panel>
    </a-collapse>
  </div>
</template>

<script setup lang="ts">
import {
  ApiOutlined,
  BranchesOutlined,
  CloudSyncOutlined,
  DatabaseOutlined,
  DeploymentUnitOutlined,
  SearchOutlined,
  ThunderboltOutlined
} from "@ant-design/icons-vue";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";

export interface FlowPaletteNode {
  id: string;
  name: string;
  description: string;
  typeKey: string;
}

export interface FlowPaletteCategory {
  key: string;
  title: string;
  nodes: FlowPaletteNode[];
}

const props = withDefaults(
  defineProps<{
    categories?: FlowPaletteCategory[];
  }>(),
  {
    categories: () => []
  }
);

const emit = defineEmits<{
  (e: "node-drag-start", payload: { node: FlowPaletteNode; dataTransfer: DataTransfer | null }): void;
}>();

const { t } = useI18n();
const search = ref("");
const openKeys = ref<string[]>(["trigger", "dataRead", "transform", "control", "transaction", "integration"]);

const defaultCategories = computed<FlowPaletteCategory[]>(() => [
  {
    key: "trigger",
    title: t("logicFlow.designerUi.nodePanel.catTrigger"),
    nodes: [
      { id: "t1", name: "Manual", description: t("logicFlow.designerUi.nodePanel.sample.manual"), typeKey: "trigger.manual" },
      { id: "t2", name: "Schedule", description: t("logicFlow.designerUi.nodePanel.sample.schedule"), typeKey: "trigger.schedule" }
    ]
  },
  {
    key: "dataRead",
    title: t("logicFlow.designerUi.nodePanel.catDataRead"),
    nodes: [
      { id: "d1", name: "Query Table", description: t("logicFlow.designerUi.nodePanel.sample.query"), typeKey: "data.query" }
    ]
  },
  {
    key: "transform",
    title: t("logicFlow.designerUi.nodePanel.catTransform"),
    nodes: [
      { id: "x1", name: "Map", description: t("logicFlow.designerUi.nodePanel.sample.map"), typeKey: "transform.map" }
    ]
  },
  {
    key: "control",
    title: t("logicFlow.designerUi.nodePanel.catControl"),
    nodes: [
      { id: "c1", name: "Branch", description: t("logicFlow.designerUi.nodePanel.sample.branch"), typeKey: "control.branch" }
    ]
  },
  {
    key: "transaction",
    title: t("logicFlow.designerUi.nodePanel.catTransaction"),
    nodes: [
      { id: "b1", name: "Unit of Work", description: t("logicFlow.designerUi.nodePanel.sample.uow"), typeKey: "tx.uow" }
    ]
  },
  {
    key: "integration",
    title: t("logicFlow.designerUi.nodePanel.catIntegration"),
    nodes: [
      { id: "i1", name: "HTTP Call", description: t("logicFlow.designerUi.nodePanel.sample.http"), typeKey: "integration.http" }
    ]
  }
]);

const sourceCategories = computed(() => (props.categories.length > 0 ? props.categories : defaultCategories.value));

const filteredCategories = computed(() => {
  const q = search.value.trim().toLowerCase();
  if (!q) {
    return sourceCategories.value;
  }
  return sourceCategories.value
    .map((c) => ({
      ...c,
      nodes: c.nodes.filter(
        (n) => n.name.toLowerCase().includes(q) || n.description.toLowerCase().includes(q) || n.typeKey.toLowerCase().includes(q)
      )
    }))
    .filter((c) => c.nodes.length > 0);
});

function iconFor(key: string) {
  const map: Record<string, typeof ThunderboltOutlined> = {
    trigger: ThunderboltOutlined,
    dataRead: DatabaseOutlined,
    transform: DeploymentUnitOutlined,
    control: BranchesOutlined,
    transaction: CloudSyncOutlined,
    integration: ApiOutlined
  };
  return map[key] ?? ApiOutlined;
}

function onDragStart(node: FlowPaletteNode, ev: DragEvent): void {
  emit("node-drag-start", { node, dataTransfer: ev.dataTransfer });
  if (ev.dataTransfer) {
    ev.dataTransfer.setData("application/json", JSON.stringify(node));
    ev.dataTransfer.effectAllowed = "copy";
  }
}
</script>

<style scoped>
.flow-node-panel {
  display: flex;
  flex-direction: column;
  gap: 8px;
  height: 100%;
  min-height: 200px;
  padding: 8px;
  background: #fafafa;
}

.node-search {
  width: 100%;
}

.node-collapse {
  flex: 1;
  overflow: auto;
  background: transparent;
}

.node-cards {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.node-card-head {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
}

.node-icon {
  font-size: 16px;
  color: #1677ff;
}

.node-desc {
  margin-top: 4px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
}
</style>
