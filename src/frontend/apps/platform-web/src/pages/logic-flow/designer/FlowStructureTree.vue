<template>
  <div class="flow-structure-tree">
    <a-collapse v-model:active-key="openKeys" :bordered="false">
      <a-collapse-panel v-for="root in displayRoots" :key="root.key" :header="root.title">
        <a-tree
          :tree-data="root.children"
          block-node
          :selected-keys="selectedKeysInner"
          @select="onSelect"
        />
      </a-collapse-panel>
    </a-collapse>
  </div>
</template>

<script setup lang="ts">
import type { TreeProps } from "ant-design-vue";
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";

export interface StructureSubFlow {
  key: string;
  title: string;
  children: TreeProps["treeData"];
}

const props = withDefaults(
  defineProps<{
    roots?: StructureSubFlow[];
    selectedKeys?: string[];
  }>(),
  {
    roots: () => [],
    selectedKeys: () => []
  }
);

const emit = defineEmits<{
  (e: "update:selectedKeys", keys: string[]): void;
  (e: "select-node", key: string): void;
}>();

const { t } = useI18n();

const openKeys = ref<string[]>(["main"]);
const selectedKeysInner = ref<string[]>([...props.selectedKeys]);

const displayRoots = computed<StructureSubFlow[]>(() => {
  if (props.roots.length > 0) {
    return props.roots;
  }
  return [
    {
      key: "main",
      title: t("logicFlow.designerUi.structure.mainFlow"),
      children: [
        {
          title: t("logicFlow.designerUi.structure.placeholderNode"),
          key: "n1",
          children: [{ title: t("logicFlow.designerUi.structure.placeholderChild"), key: "n1-1" }]
        }
      ] as TreeProps["treeData"]
    }
  ];
});

watch(
  displayRoots,
  (r) => {
    openKeys.value = r.map((x) => x.key);
  },
  { immediate: true }
);

watch(
  () => props.selectedKeys,
  (v) => {
    selectedKeysInner.value = [...v];
  }
);

function onSelect(keys: (string | number)[]): void {
  const str = keys.map(String);
  selectedKeysInner.value = str;
  emit("update:selectedKeys", str);
  const last = str[str.length - 1];
  if (last) {
    emit("select-node", last);
  }
}
</script>

<style scoped>
.flow-structure-tree {
  padding: 8px;
  min-height: 200px;
  background: #fff;
}
</style>
