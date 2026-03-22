<template>
  <div class="component-tree-panel">
    <div class="tree-header">
      <h4>{{ t("designer.componentTree.title") }}</h4>
    </div>
    <a-tree
      v-if="treeData.length > 0"
      :tree-data="treeData"
      :selected-keys="selectedKeys"
      default-expand-all
      show-icon
      draggable
      @select="handleSelect"
      @drop="handleDrop"
    >
      <template #title="{ title, key: nodeKey }">
        <span :class="{ 'tree-node-selected': selectedKeys.includes(nodeKey) }">
          {{ title }}
        </span>
      </template>
    </a-tree>
    <a-empty v-else :description="t('designer.componentTree.emptyHint')" />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import type { TreeProps } from "ant-design-vue";

const { t } = useI18n();

interface Props {
  schema: Record<string, unknown> | null;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  (e: "select", nodeId: string): void;
  (e: "reorder", fromKey: string, toKey: string, position: string): void;
}>();

const selectedKeys = ref<string[]>([]);

interface TreeNode {
  key: string;
  title: string;
  children: TreeNode[];
}

function extractTree(node: unknown, parentPath = "root"): TreeNode[] {
  if (!node || typeof node !== "object") return [];
  const obj = node as Record<string, unknown>;
  const nodeType = (obj.type as string) ?? "unknown";
  const nodeName = (obj.name as string) ?? (obj.label as string) ?? "";
  const nodeTitle = `${nodeType}${nodeName ? ` (${nodeName})` : ""}`;
  const key = `${parentPath}/${nodeType}-${nodeName || Math.random().toString(36).slice(2, 8)}`;

  const children: TreeNode[] = [];

  const childArrayKeys = ["body", "columns", "items", "controls", "tabs", "steps"];
  for (const ck of childArrayKeys) {
    const childVal = obj[ck];
    if (Array.isArray(childVal)) {
      childVal.forEach((child, idx) => {
        children.push(...extractTree(child, `${key}[${idx}]`));
      });
    } else if (childVal && typeof childVal === "object") {
      children.push(...extractTree(childVal, `${key}/${ck}`));
    }
  }

  return [{ key, title: nodeTitle, children }];
}

const treeData = computed(() => {
  if (!props.schema) return [];
  return extractTree(props.schema);
});

function handleSelect(keys: string[]) {
  selectedKeys.value = keys;
  if (keys.length > 0) {
    emit("select", keys[0]);
  }
}

function handleDrop(info: TreeProps["onDrop"] extends ((...args: infer P) => void) | undefined ? P[0] : never) {
  const dropInfo = info as { dragNode?: { key?: string }; node?: { key?: string }; dropPosition?: number };
  const fromKey = String(dropInfo.dragNode?.key ?? "");
  const toKey = String(dropInfo.node?.key ?? "");
  const position = String(dropInfo.dropPosition ?? "0");
  emit("reorder", fromKey, toKey, position);
}

watch(() => props.schema, () => {
  selectedKeys.value = [];
}, { deep: true });
</script>

<style scoped>
.component-tree-panel {
  padding: 8px;
  height: 100%;
  overflow-y: auto;
}

.tree-header h4 {
  margin: 0 0 8px;
  font-size: 13px;
  font-weight: 600;
}

.tree-node-selected {
  color: #1890ff;
  font-weight: 500;
}
</style>
