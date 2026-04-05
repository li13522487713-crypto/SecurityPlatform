<template>
  <div class="dd-toolbar">
    <a-button type="text" size="small" class="dd-toolbar__back" @click="emit('back')">
      <LeftOutlined /> {{ t('approvalDesigner.toolbarBack') }}
    </a-button>
    <a-divider type="vertical" />
    <a-input
      :value="flowName"
      :placeholder="t('approvalDesigner.toolbarFlowNamePh')"
      :bordered="false"
      class="dd-toolbar__name"
      :maxlength="100"
      @update:value="emit('update:flowName', $event)"
    />
    <a-tag v-if="flowVersion" color="blue" class="dd-toolbar__version">v{{ flowVersion }}</a-tag>

    <div style="flex: 1"></div>

    <div class="dd-toolbar__actions">
      <template v-if="activeMenu === 'process'">
        <a-divider type="vertical" />
        <a-button
          size="small"
          :type="paletteVisible ? 'primary' : 'default'"
          :title="t('approvalDesigner.nodePaletteTitle')"
          @click="emit('update:paletteVisible', !paletteVisible)"
        >
          <AppstoreOutlined />
        </a-button>
        <a-divider type="vertical" />
        <a-button size="small" :title="t('approvalDesigner.zoomOutTitle')" @click="emit('zoom-out')"><MinusOutlined /></a-button>
        <a-button size="small" :title="t('approvalDesigner.zoomFitTitle')" @click="emit('zoom-fit')"><CompressOutlined /></a-button>
        <a-button size="small" :title="t('approvalDesigner.zoomInTitle')" @click="emit('zoom-in')"><PlusOutlined /></a-button>
        <a-divider type="vertical" />
        <a-button size="small" :disabled="!canUndo" @click="emit('undo')"><UndoOutlined /></a-button>
        <a-button size="small" :disabled="!canRedo" @click="emit('redo')"><RedoOutlined /></a-button>
        <a-divider type="vertical" />
        <a-button size="small" :loading="validating" @click="emit('validate')"><CheckCircleOutlined /> {{ t('approvalDesigner.toolbarValidate') }}</a-button>
        <a-button size="small" @click="emit('preview')"><EyeOutlined /> {{ t('approvalDesigner.toolbarPreview') }}</a-button>
      </template>
      <a-divider type="vertical" />
      <a-button size="small" @click="emit('history')"><HistoryOutlined /> {{ t('approvalDesigner.toolbarVersionHistory') }}</a-button>
      <a-button size="small" @click="emit('save')">{{ t('approvalDesigner.toolbarSave') }}</a-button>
      <a-button type="primary" size="small" @click="emit('publish')">{{ t('approvalDesigner.toolbarPublish') }}</a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import {
  LeftOutlined,
  MinusOutlined,
  PlusOutlined,
  CompressOutlined,
  UndoOutlined,
  RedoOutlined,
  CheckCircleOutlined,
  EyeOutlined,
  AppstoreOutlined,
  HistoryOutlined,
} from '@ant-design/icons-vue';

const { t } = useI18n();

defineProps<{
  flowName: string;
  flowVersion: number;
  activeMenu: string;
  paletteVisible: boolean;
  canUndo: boolean;
  canRedo: boolean;
  validating: boolean;
}>();

const emit = defineEmits<{
  back: [];
  'update:flowName': [value: string];
  'update:activeMenu': [value: string];
  'update:paletteVisible': [value: boolean];
  'zoom-out': [];
  'zoom-fit': [];
  'zoom-in': [];
  undo: [];
  redo: [];
  validate: [];
  preview: [];
  save: [];
  publish: [];
  history: [];
}>();
</script>

<style scoped>
.dd-toolbar {
  display: flex;
  align-items: center;
  height: 44px;
  flex-shrink: 0;
  padding: 0 10px;
  background: #fff;
  border-bottom: 1px solid #e8e8e8;
  gap: 4px;
}
.dd-toolbar__back {
  font-size: 13px;
  color: #595959;
  padding: 0 6px;
}
.dd-toolbar__name {
  width: 180px;
  font-weight: 600;
  font-size: 14px;
}
.dd-toolbar__version {
  margin-right: 4px;
}
.dd-toolbar__version {
  margin-right: 4px;
}
.dd-toolbar__actions {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
}
</style>
