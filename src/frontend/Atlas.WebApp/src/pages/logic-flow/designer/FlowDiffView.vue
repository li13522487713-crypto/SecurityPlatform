<template>
  <div class="flow-diff-view">
    <a-card size="small" class="summary-card" :title="t('logicFlow.designerUi.diff.summaryTitle')">
      <a-space wrap>
        <a-tag color="green">+ {{ summary.added }} {{ t("logicFlow.designerUi.diff.added") }}</a-tag>
        <a-tag color="red">− {{ summary.removed }} {{ t("logicFlow.designerUi.diff.removed") }}</a-tag>
        <a-tag color="blue">~ {{ summary.modified }} {{ t("logicFlow.designerUi.diff.modified") }}</a-tag>
      </a-space>
    </a-card>
    <a-row :gutter="16" class="diff-row">
      <a-col :xs="24" :lg="12">
        <a-card size="small" :title="t('logicFlow.designerUi.diff.leftVersion')">
          <a-select v-model:value="leftVersion" style="width: 100%; margin-bottom: 12px" :options="versionOptions" />
          <div class="diff-canvas diff-canvas-left">
            <div v-for="n in leftNodes" :key="n.id" class="diff-node" :class="n.change">
              {{ n.label }}
            </div>
          </div>
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="12">
        <a-card size="small" :title="t('logicFlow.designerUi.diff.rightVersion')">
          <a-select v-model:value="rightVersion" style="width: 100%; margin-bottom: 12px" :options="versionOptions" />
          <div class="diff-canvas diff-canvas-right">
            <div v-for="n in rightNodes" :key="n.id" class="diff-node" :class="n.change">
              {{ n.label }}
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";

export interface DiffNodeItem {
  id: string;
  label: string;
  change: "added" | "removed" | "modified" | "unchanged";
}

const props = withDefaults(
  defineProps<{
    versionOptions?: { label: string; value: string }[];
    leftNodes?: DiffNodeItem[];
    rightNodes?: DiffNodeItem[];
  }>(),
  {
    versionOptions: () => [
      { label: "v1.0.0", value: "v1.0.0" },
      { label: "v1.1.0", value: "v1.1.0" }
    ],
    leftNodes: () => [
      { id: "a", label: "Start", change: "unchanged" },
      { id: "b", label: "Query", change: "modified" }
    ],
    rightNodes: () => [
      { id: "a", label: "Start", change: "unchanged" },
      { id: "b2", label: "Query v2", change: "added" },
      { id: "c", label: "Legacy", change: "removed" }
    ]
  }
);

const { t } = useI18n();

const leftVersion = ref(props.versionOptions[0]?.value ?? "v1.0.0");
const rightVersion = ref(props.versionOptions[1]?.value ?? "v1.1.0");

const versionOptions = computed(() => props.versionOptions);

const summary = computed(() => {
  const count = (items: DiffNodeItem[], k: DiffNodeItem["change"]) => items.filter((x) => x.change === k).length;
  return {
    added: count(props.rightNodes, "added"),
    removed: count(props.rightNodes, "removed"),
    modified: count(props.leftNodes, "modified") + count(props.rightNodes, "modified")
  };
});

const leftNodes = computed(() => props.leftNodes);
const rightNodes = computed(() => props.rightNodes);
</script>

<style scoped>
.flow-diff-view {
  padding: 8px;
}

.summary-card {
  margin-bottom: 16px;
}

.diff-row {
  align-items: stretch;
}

.diff-canvas {
  min-height: 220px;
  padding: 12px;
  border: 1px dashed #d9d9d9;
  border-radius: 4px;
  background: #fafafa;
}

.diff-node {
  margin-bottom: 8px;
  padding: 8px 12px;
  border-radius: 4px;
  border: 1px solid #f0f0f0;
  background: #fff;
}

.diff-node.added {
  border-color: #b7eb8f;
  background: #f6ffed;
}

.diff-node.removed {
  border-color: #ffa39e;
  background: #fff2f0;
}

.diff-node.modified {
  border-color: #91d5ff;
  background: #e6f7ff;
}
</style>
