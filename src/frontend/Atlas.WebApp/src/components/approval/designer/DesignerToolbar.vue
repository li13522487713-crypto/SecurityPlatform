<template>
  <div class="dd-toolbar">
    <a-button type="text" size="small" class="dd-toolbar__back" @click="emit('back')">
      <LeftOutlined /> 返回
    </a-button>
    <a-divider type="vertical" />
    <a-input
      :value="flowName"
      placeholder="流程名称"
      :bordered="false"
      class="dd-toolbar__name"
      :maxlength="100"
      @update:value="emit('update:flowName', $event)"
    />
    <a-tag v-if="flowVersion" color="blue" class="dd-toolbar__version">v{{ flowVersion }}</a-tag>

    <div class="dd-toolbar__steps">
      <span
        v-for="(s, i) in ['基础设置', '表单设计', '流程设计']"
        :key="i"
        class="dd-step-dot"
        :class="{ 'dd-step-dot--active': activeStep === i, 'dd-step-dot--done': activeStep > i }"
        @click="emit('update:activeStep', i)"
      >{{ s }}</span>
    </div>

    <div class="dd-toolbar__actions">
      <a-button v-if="activeStep > 0" size="small" @click="emit('prev-step')">上一步</a-button>
      <a-button v-if="activeStep < 2" size="small" @click="emit('next-step')">下一步</a-button>
      <template v-if="activeStep === 2">
        <a-divider type="vertical" />
        <a-button
          size="small"
          :type="paletteVisible ? 'primary' : 'default'"
          title="节点面板"
          @click="emit('update:paletteVisible', !paletteVisible)"
        >
          <AppstoreOutlined />
        </a-button>
        <a-divider type="vertical" />
        <a-button size="small" title="缩小（Ctrl + -）" @click="emit('zoom-out')"><MinusOutlined /></a-button>
        <a-button size="small" title="适应画布（Ctrl + 0）" @click="emit('zoom-fit')"><CompressOutlined /></a-button>
        <a-button size="small" title="放大（Ctrl + +）" @click="emit('zoom-in')"><PlusOutlined /></a-button>
        <a-divider type="vertical" />
        <a-button size="small" :disabled="!canUndo" @click="emit('undo')"><UndoOutlined /></a-button>
        <a-button size="small" :disabled="!canRedo" @click="emit('redo')"><RedoOutlined /></a-button>
        <a-divider type="vertical" />
        <a-button size="small" :loading="validating" @click="emit('validate')"><CheckCircleOutlined /> 校验</a-button>
        <a-button size="small" @click="emit('preview')"><EyeOutlined /> 预览</a-button>
      </template>
      <a-divider type="vertical" />
      <a-button size="small" @click="emit('save')">保存</a-button>
      <a-button type="primary" size="small" @click="emit('publish')">发布</a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
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
} from '@ant-design/icons-vue';

defineProps<{
  flowName: string;
  flowVersion: number;
  activeStep: number;
  paletteVisible: boolean;
  canUndo: boolean;
  canRedo: boolean;
  validating: boolean;
}>();

const emit = defineEmits<{
  back: [];
  'update:flowName': [value: string];
  'update:activeStep': [value: number];
  'update:paletteVisible': [value: boolean];
  'prev-step': [];
  'next-step': [];
  'zoom-out': [];
  'zoom-fit': [];
  'zoom-in': [];
  undo: [];
  redo: [];
  validate: [];
  preview: [];
  save: [];
  publish: [];
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
.dd-toolbar__steps {
  display: flex;
  align-items: center;
  gap: 2px;
  margin: 0 auto;
}
.dd-step-dot {
  padding: 2px 10px;
  font-size: 12px;
  color: #8c8c8c;
  cursor: pointer;
  border-radius: 10px;
  transition: all 0.2s;
  white-space: nowrap;
}
.dd-step-dot--active {
  background: #1677ff;
  color: #fff;
  font-weight: 500;
}
.dd-step-dot--done {
  color: #1677ff;
}
.dd-step-dot:hover:not(.dd-step-dot--active) {
  background: #f0f0f0;
}
.dd-toolbar__actions {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
}
</style>
