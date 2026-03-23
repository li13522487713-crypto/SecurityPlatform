<template>
  <div class="node-list">
    <div class="header">
      <span>{{ t("designerUi.nodeStructure") }}</span>
      <a-button type="link" size="small" @click="onAddStart">{{ t("designerUi.addStartEnd") }}</a-button>
    </div>
    <a-tree
      block-node
      :tree-data="treeData"
      :selected-keys="selectedKeys"
      @select="onSelect"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { FlowNode } from "@/types/workflow";

const { t } = useI18n();

interface Props {
  nodes: FlowNode[];
  selectedId?: string;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "select", id: string | undefined): void;
  (e: "add-default"): void;
}>();

const treeData = computed(() => nodesToTree(props.nodes));
const selectedKeys = computed(() => (props.selectedId ? [props.selectedId] : []));

const onSelect = (keys: (string | number)[]) => {
  emit("select", (keys[0] as string) || undefined);
};

const onAddStart = () => {
  if (props.nodes.length === 0) {
    emit("add-default");
  }
};

type TreeItem = {
  key: string;
  title: string;
  children?: TreeItem[];
};

function nodesToTree(nodes: FlowNode[]): TreeItem[] {
  return nodes.map((n) => ({
    key: n.id,
    title: `${n.name} (${n.type})`,
    children: n.children ? nodesToTree(n.children) : []
  }));
}
</script>

<style scoped>
.node-list {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  padding: 12px;
  background: #fff;
}
.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
</style>
