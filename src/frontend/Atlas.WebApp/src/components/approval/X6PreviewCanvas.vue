<template>
  <div ref="containerRef" class="x6-container"></div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, onBeforeUnmount } from 'vue';
import { Graph } from '@antv/x6';
import type { ApprovalFlowTree } from '@/types/approval-tree';
import { ApprovalTreeConverter } from '@/utils/approval-tree-converter';

const props = defineProps<{
  flowTree: ApprovalFlowTree;
}>();

const containerRef = ref<HTMLElement>();
const graphRef = ref<Graph>();

const initGraph = () => {
  if (!containerRef.value) return;

  const graph = new Graph({
    container: containerRef.value,
    grid: true,
    panning: true,
    mousewheel: true,
    interacting: false, // Read-only
    connecting: {
      router: 'manhattan',
      connector: {
        name: 'rounded',
        args: { radius: 8 },
      },
    },
  });

  graphRef.value = graph;
  renderGraph();
};

const renderGraph = () => {
  if (!graphRef.value || !props.flowTree) return;
  
  const { nodes, edges } = ApprovalTreeConverter.treeToGraph(props.flowTree);
  
  graphRef.value.fromJSON({ nodes, edges });
  graphRef.value.centerContent();
};

watch(() => props.flowTree, () => {
  renderGraph();
}, { deep: true });

onMounted(() => {
  initGraph();
});

onBeforeUnmount(() => {
  graphRef.value?.dispose();
});
</script>

<style scoped>
.x6-container {
  width: 100%;
  height: 100%;
}
</style>
