<template>
  <div class="flow-debug-view">
    <div class="top-pane">
      <FlowCanvas :flow-loaded="flowLoaded" @node-click="onNodeClick" @edge-click="onEdgeClick" @canvas-click="onCanvasClick" />
    </div>
    <div class="debug-toolbar">
      <a-space wrap>
        <a-button size="small" @click="emit('step-in')">{{ t("logicFlow.designerUi.debugView.stepIn") }}</a-button>
        <a-button size="small" @click="emit('step-over')">{{ t("logicFlow.designerUi.debugView.stepOver") }}</a-button>
        <a-button size="small" type="primary" @click="emit('continue')">{{ t("logicFlow.designerUi.debugView.continue") }}</a-button>
        <a-button size="small" danger @click="emit('stop')">{{ t("logicFlow.designerUi.debugView.stop") }}</a-button>
      </a-space>
      <div class="breakpoints-inline">
        <span class="lbl">{{ t("logicFlow.designerUi.debugView.breakpointMarkers") }}</span>
        <a-tag v-for="m in breakpointMarkers" :key="m" color="red">{{ m }}</a-tag>
      </div>
    </div>
    <a-row :gutter="0" class="bottom-pane">
      <a-col :xs="24" :md="16">
        <FlowDebugPanel />
      </a-col>
      <a-col :xs="24" :md="8" class="inspector-col">
        <div class="inspector-title">{{ t("logicFlow.designerUi.debugView.variableInspector") }}</div>
        <a-descriptions size="small" bordered :column="1">
          <a-descriptions-item v-for="row in inspectorRows" :key="row.name" :label="row.name">
            {{ row.value }}
          </a-descriptions-item>
        </a-descriptions>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import FlowCanvas from "./FlowCanvas.vue";
import FlowDebugPanel from "./FlowDebugPanel.vue";

withDefaults(
  defineProps<{
    flowLoaded?: boolean;
    breakpointMarkers?: string[];
    inspectorRows?: { name: string; value: string }[];
  }>(),
  {
    flowLoaded: true,
    breakpointMarkers: () => ["node-start"],
    inspectorRows: () => [
      { name: "input", value: "{}" },
      { name: "locals", value: "{}" }
    ]
  }
);

const emit = defineEmits<{
  (e: "step-in"): void;
  (e: "step-over"): void;
  (e: "continue"): void;
  (e: "stop"): void;
  (e: "select-node", id: string): void;
}>();

const { t } = useI18n();

function onNodeClick(id: string): void {
  emit("select-node", id);
}

function onEdgeClick(_id: string): void {
  // 占位
}

function onCanvasClick(): void {
  // 占位
}
</script>

<style scoped>
.flow-debug-view {
  display: flex;
  flex-direction: column;
  min-height: 480px;
  background: #fff;
}

.top-pane {
  flex: 1;
  min-height: 200px;
  padding: 8px;
}

.debug-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 12px;
  border-top: 1px solid #f0f0f0;
  border-bottom: 1px solid #f0f0f0;
}

.breakpoints-inline {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.lbl {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
}

.bottom-pane {
  border-top: 1px solid #f0f0f0;
}

.inspector-col {
  border-left: 1px solid #f0f0f0;
  padding: 12px;
  background: #fafafa;
}

.inspector-title {
  font-weight: 600;
  margin-bottom: 8px;
}
</style>
