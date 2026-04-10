<template>
  <BaseEdge :id="id" :path="edgePath" :style="edgeStyle" />
  <EdgeLabelRenderer v-if="edgeLabel">
    <div
      class="workflow-edge-label"
      :style="{
        transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
      }"
    >
      {{ edgeLabel }}
    </div>
  </EdgeLabelRenderer>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { BaseEdge, EdgeLabelRenderer, getSmoothStepPath, type EdgeProps } from "@vue-flow/core";

const props = defineProps<EdgeProps>();

const edgeLabel = computed(() => {
  return ((props.data as { condition?: string } | undefined)?.condition ?? "").trim();
});

const pathData = computed(() =>
  getSmoothStepPath({
    sourceX: props.sourceX,
    sourceY: props.sourceY,
    sourcePosition: props.sourcePosition,
    targetX: props.targetX,
    targetY: props.targetY,
    targetPosition: props.targetPosition,
    borderRadius: 10
  })
);

const edgePath = computed(() => pathData.value[0]);
const labelX = computed(() => pathData.value[1]);
const labelY = computed(() => pathData.value[2]);

const edgeStyle = computed(() => {
  const isConditional = edgeLabel.value.length > 0;
  return {
    stroke: isConditional ? "#1677ff" : "#4b5563",
    strokeWidth: isConditional ? 2.5 : 2,
    strokeDasharray: isConditional ? "6 4" : "0"
  };
});
</script>

<style scoped>
.workflow-edge-label {
  position: absolute;
  pointer-events: all;
  padding: 2px 8px;
  border-radius: 999px;
  border: 1px solid #2f3b4a;
  background: #0f1722;
  color: #cbd5e1;
  font-size: 11px;
  line-height: 16px;
  white-space: nowrap;
}
</style>
