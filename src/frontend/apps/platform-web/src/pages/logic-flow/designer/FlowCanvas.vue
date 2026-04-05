<template>
  <div class="flow-canvas-wrap">
    <div
      ref="canvasRoot"
      class="flow-canvas"
      role="application"
      tabindex="0"
      @click="onCanvasClick"
    >
      <div v-if="!flowLoaded" class="canvas-placeholder">
        <a-empty :description="t('logicFlow.designerUi.canvas.placeholder')" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";

const props = withDefaults(
  defineProps<{
    flowLoaded?: boolean;
  }>(),
  {
    flowLoaded: false
  }
);

const emit = defineEmits<{
  (e: "node-click", nodeId: string): void;
  (e: "edge-click", edgeId: string): void;
  (e: "canvas-click"): void;
}>();

const { t } = useI18n();
const canvasRoot = ref<HTMLElement | null>(null);

function onCanvasClick(): void {
  emit("canvas-click");
}

/** 供父组件触发，演示事件桥接 */
function notifyNodeClick(nodeId: string): void {
  emit("node-click", nodeId);
}

function notifyEdgeClick(edgeId: string): void {
  emit("edge-click", edgeId);
}

defineExpose({
  canvasRoot,
  notifyNodeClick,
  notifyEdgeClick,
  getMountEl: (): HTMLElement | null => canvasRoot.value
});

onMounted(() => {
  // 占位：后续在此初始化 X6
});

watch(
  () => props.flowLoaded,
  () => {
    // 占位：加载/卸载图实例
  }
);
</script>

<style scoped>
.flow-canvas-wrap {
  position: relative;
  width: 100%;
  min-height: 320px;
  flex: 1;
}

.flow-canvas {
  min-height: 320px;
  height: 100%;
  background: #fafafa;
  border: 1px dashed #d9d9d9;
  border-radius: 4px;
  outline: none;
}

.canvas-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 300px;
}
</style>
