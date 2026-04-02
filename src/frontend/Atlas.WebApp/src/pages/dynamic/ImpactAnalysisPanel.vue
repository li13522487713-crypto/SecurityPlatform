<template>
  <div class="impact-analysis-panel">
    <a-space class="summary" wrap>
      <span>{{ t("dynamic.impactAnalysisPanel.summaryPrefix") }}</span>
      <a-tag :color="riskColor(riskLevel)">{{ riskLabel(riskLevel) }}</a-tag>
    </a-space>
    <a-tree
      :tree-data="treeData"
      block-node
      default-expand-all
    >
      <template #title="{ title, risk }">
        <span>{{ title }}</span>
        <a-tag v-if="risk" class="risk-tag" :color="riskColor(risk)">{{ riskLabel(risk) }}</a-tag>
      </template>
    </a-tree>
  </div>
</template>

<script setup lang="ts">
import type { TreeProps } from "ant-design-vue";
import { computed } from "vue";
import { useI18n } from "vue-i18n";

export type RiskLevel = "low" | "medium" | "high";

export interface ImpactTreeNode {
  title: string;
  key: string;
  risk?: RiskLevel;
  children?: ImpactTreeNode[];
}

const props = withDefaults(
  defineProps<{
    riskLevel?: RiskLevel;
    nodes?: ImpactTreeNode[];
  }>(),
  {
    riskLevel: "medium",
    nodes: () => []
  }
);

const { t } = useI18n();

const treeData = computed<TreeProps["treeData"]>(() => {
  if (props.nodes.length > 0) {
    return props.nodes as TreeProps["treeData"];
  }
  return [
    {
      title: t("dynamic.impactAnalysisPanel.rootTable"),
      key: "table",
      risk: props.riskLevel,
      children: [
        {
          title: t("dynamic.impactAnalysisPanel.itemView"),
          key: "v1",
          risk: "low"
        },
        {
          title: t("dynamic.impactAnalysisPanel.itemFunction"),
          key: "f1",
          risk: "medium"
        },
        {
          title: t("dynamic.impactAnalysisPanel.itemFlow"),
          key: "fl1",
          risk: "high"
        }
      ]
    }
  ] as TreeProps["treeData"];
});

function riskColor(r: RiskLevel): string {
  if (r === "high") {
    return "red";
  }
  if (r === "medium") {
    return "orange";
  }
  return "green";
}

function riskLabel(r: RiskLevel): string {
  const map: Record<RiskLevel, string> = {
    low: t("dynamic.impactAnalysisPanel.riskLow"),
    medium: t("dynamic.impactAnalysisPanel.riskMedium"),
    high: t("dynamic.impactAnalysisPanel.riskHigh")
  };
  return map[r];
}
</script>

<style scoped>
.impact-analysis-panel {
  padding: 8px 0;
}

.summary {
  margin-bottom: 12px;
}

.risk-tag {
  margin-left: 8px;
  font-size: 11px;
  line-height: 18px;
}
</style>
