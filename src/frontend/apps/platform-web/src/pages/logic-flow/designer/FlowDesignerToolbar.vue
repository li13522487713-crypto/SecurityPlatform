<template>
  <div class="flow-designer-toolbar">
    <a-space wrap :size="[8, 8]">
      <a-tooltip :title="t('logicFlow.designerUi.toolbar.new')">
        <a-button size="small" @click="emit('new')">
          <template #icon><FileAddOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-tooltip :title="t('common.save')">
        <a-button size="small" @click="emit('save')">
          <template #icon><SaveOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-tooltip :title="t('logicFlow.publish')">
        <a-button size="small" type="primary" @click="emit('publish')">
          <template #icon><CloudUploadOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-tooltip :title="t('logicFlow.validate')">
        <a-button size="small" @click="emit('validate')">
          <template #icon><CheckCircleOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-divider type="vertical" />
      <a-tooltip :title="t('logicFlow.designerUi.toolbar.undo')">
        <a-button size="small" :disabled="!canUndo" @click="emit('undo')">
          <template #icon><UndoOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-tooltip :title="t('logicFlow.designerUi.toolbar.redo')">
        <a-button size="small" :disabled="!canRedo" @click="emit('redo')">
          <template #icon><RedoOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-divider type="vertical" />
      <a-tooltip :title="t('logicFlow.designerUi.toolbar.zoomIn')">
        <a-button size="small" @click="emit('zoomIn')">
          <template #icon><ZoomInOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-tooltip :title="t('logicFlow.designerUi.toolbar.zoomOut')">
        <a-button size="small" @click="emit('zoomOut')">
          <template #icon><ZoomOutOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-tooltip :title="t('logicFlow.designerUi.toolbar.fit')">
        <a-button size="small" @click="emit('fit')">
          <template #icon><ExpandOutlined /></template>
        </a-button>
      </a-tooltip>
      <a-tooltip :title="t('logicFlow.designerUi.toolbar.toggleMinimap')">
        <a-button size="small" :type="minimapVisible ? 'primary' : 'default'" @click="emit('toggleMinimap')">
          <template #icon><BlockOutlined /></template>
        </a-button>
      </a-tooltip>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import {
  BlockOutlined,
  CheckCircleOutlined,
  CloudUploadOutlined,
  ExpandOutlined,
  FileAddOutlined,
  RedoOutlined,
  SaveOutlined,
  UndoOutlined,
  ZoomInOutlined,
  ZoomOutOutlined
} from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";

defineProps<{
  canUndo?: boolean;
  canRedo?: boolean;
  minimapVisible?: boolean;
}>();

const emit = defineEmits<{
  (e: "new"): void;
  (e: "save"): void;
  (e: "publish"): void;
  (e: "validate"): void;
  (e: "undo"): void;
  (e: "redo"): void;
  (e: "zoomIn"): void;
  (e: "zoomOut"): void;
  (e: "fit"): void;
  (e: "toggleMinimap"): void;
}>();

const { t } = useI18n();
</script>

<style scoped>
.flow-designer-toolbar {
  padding: 8px 12px;
  border-bottom: 1px solid #f0f0f0;
  background: #fff;
}
</style>
