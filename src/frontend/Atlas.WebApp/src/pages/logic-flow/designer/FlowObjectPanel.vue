<template>
  <div class="flow-object-panel">
    <div class="panel-title">{{ t("logicFlow.designerUi.objectPanel.title") }}</div>
    <a-tree
      v-model:selected-keys="innerSelected"
      v-model:expanded-keys="expandedKeys"
      :tree-data="treeData"
      show-line
      block-node
      @select="onSelect"
    >
      <template #title="{ title, status }">
        <span class="tree-title">{{ title }}</span>
        <CheckCircleOutlined v-if="status === 'ok'" class="status-ico ok" />
        <LoadingOutlined v-else-if="status === 'running'" class="status-ico run" />
        <CloseCircleOutlined v-else-if="status === 'error'" class="status-ico err" />
      </template>
    </a-tree>
  </div>
</template>

<script setup lang="ts">
import { CheckCircleOutlined, CloseCircleOutlined, LoadingOutlined } from "@ant-design/icons-vue";
import type { TreeProps } from "ant-design-vue";
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";

export type ObjectNodeStatus = "ok" | "running" | "error" | "idle";

export interface FlowObjectTreeNode {
  key: string;
  title: string;
  status?: ObjectNodeStatus;
  children?: FlowObjectTreeNode[];
}

const props = withDefaults(
  defineProps<{
    nodes?: FlowObjectTreeNode[];
    selectedKeys?: string[];
  }>(),
  {
    nodes: () => [],
    selectedKeys: () => []
  }
);

const emit = defineEmits<{
  (e: "update:selectedKeys", keys: string[]): void;
  (e: "select-object", key: string): void;
}>();

const { t } = useI18n();

const innerSelected = ref<string[]>([...props.selectedKeys]);
const expandedKeys = ref<string[]>([]);

watch(
  () => props.selectedKeys,
  (v) => {
    innerSelected.value = [...v];
  }
);

const treeData = computed<TreeProps["treeData"]>(() => {
  if (props.nodes.length > 0) {
    return props.nodes as TreeProps["treeData"];
  }
  return [
    {
      key: "flow-root",
      title: t("logicFlow.designerUi.objectPanel.rootFlow"),
      status: "idle",
      children: [
        { key: "node-start", title: t("logicFlow.designerUi.objectPanel.placeholderNode"), status: "ok" }
      ]
    }
  ] as TreeProps["treeData"];
});

watch(
  treeData,
  (td) => {
    const keys: string[] = [];
    const walk = (list: FlowObjectTreeNode[]) => {
      for (const n of list) {
        keys.push(n.key);
        if (n.children?.length) {
          walk(n.children);
        }
      }
    };
    if (td && Array.isArray(td)) {
      walk(td as FlowObjectTreeNode[]);
    }
    expandedKeys.value = keys.slice(0, 8);
  },
  { immediate: true }
);

function onSelect(keys: (string | number)[]): void {
  const strKeys = keys.map(String);
  innerSelected.value = strKeys;
  emit("update:selectedKeys", strKeys);
  const last = strKeys[strKeys.length - 1];
  if (last) {
    emit("select-object", last);
  }
}
</script>

<style scoped>
.flow-object-panel {
  padding: 8px;
  min-height: 160px;
  background: #fff;
  border-right: 1px solid #f0f0f0;
}

.panel-title {
  font-weight: 600;
  margin-bottom: 8px;
}

.tree-title {
  margin-right: 6px;
}

.status-ico {
  font-size: 12px;
}

.status-ico.ok {
  color: #52c41a;
}

.status-ico.run {
  color: #1677ff;
}

.status-ico.err {
  color: #ff4d4f;
}
</style>
