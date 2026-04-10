<template>
  <BaseEdge :id="id" :path="edgePath" :style="edgeStyle" />
  <BaseEdge v-if="isRunningEdge" :id="`${id}-animated`" :path="edgePath" class="animated-overlay" />
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
import { BaseEdge, EdgeLabelRenderer, getBezierPath, type EdgeProps } from "@vue-flow/core";

const props = defineProps<EdgeProps>();

const edgeLabel = computed(() => {
  return ((props.data as { condition?: string } | undefined)?.condition ?? "").trim();
});

const pathData = computed(() =>
  getBezierPath({
    sourceX: props.sourceX,
    sourceY: props.sourceY,
    sourcePosition: props.sourcePosition,
    targetX: props.targetX,
    targetY: props.targetY,
    targetPosition: props.targetPosition
  })
);

const edgePath = computed(() => pathData.value[0]);
const labelX = computed(() => pathData.value[1]);
const labelY = computed(() => pathData.value[2]);

const edgeStyle = computed(() => {
  const isConditional = edgeLabel.value.length > 0;
  const running = Boolean((props.data as { running?: boolean } | undefined)?.running);
  return {
    stroke: isConditional ? "#4e40e5" : "#94a3b8",
    strokeWidth: running ? 3 : (isConditional ? 2.5 : 2),
    strokeDasharray: running ? "9 6" : (isConditional ? "6 4" : "0"),
    animation: running ? "edge-flow 0.8s linear infinite" : "none"
  };
});

const isRunningEdge = computed(() => Boolean((props.data as { running?: boolean } | undefined)?.running));
</script>

<style scoped>
.workflow-edge-label {
  position: absolute;
  pointer-events: all;
  padding: 2px 8px;
  border-radius: 999px;
  border: 1px solid #dbeafe;
  background: #eef2ff;
  color: #4338ca;
  font-size: 11px;
  line-height: 16px;
  white-space: nowrap;
  box-shadow: 0 2px 8px rgba(79, 70, 229, 0.16);
}

:deep(.animated-overlay) {
  stroke: #4e40e5;
  stroke-width: 3;
  stroke-dasharray: 8 7;
  animation: edge-flow 0.7s linear infinite;
}

@keyframes edge-flow {
  to {
    stroke-dashoffset: -15;
  }
}
</style>
