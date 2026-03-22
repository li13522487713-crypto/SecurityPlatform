<template>
  <div class="device-toolbar">
    <a-space size="middle">
      <a-radio-group v-model:value="settings.deviceType" option-type="button" button-style="solid" @change="handleChange">
        <a-radio-button value="desktop">
          <laptop-outlined /> 桌面 (100%)
        </a-radio-button>
        <a-radio-button value="tablet">
          <tablet-outlined /> 平板 (768px)
        </a-radio-button>
        <a-radio-button value="mobile">
          <mobile-outlined /> 手机 (375px)
        </a-radio-button>
      </a-radio-group>

      <a-tooltip title="切换屏幕方向 (仅平板/手机模式有效)">
        <a-button 
          :disabled="settings.deviceType === 'desktop'" 
          @click="toggleOrientation"
        >
          <template #icon><sync-outlined :rotate="settings.orientation === 'landscape' ? 90 : 0" /></template>
        </a-button>
      </a-tooltip>

      <a-divider type="vertical" />
      
      <span class="scale-label">缩放:</span>
      <a-slider 
        v-model:value="settings.scale" 
        :min="0.5" 
        :max="2" 
        :step="0.1" 
        style="width: 120px; display: inline-block; vertical-align: middle;" 
        :tip-formatter="(val: number) => `${Math.round(val * 100)}%`"
        @change="handleChange"
      />
      <span class="scale-value">{{ Math.round(settings.scale * 100) }}%</span>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue';
import { LaptopOutlined, TabletOutlined, MobileOutlined, SyncOutlined } from '@ant-design/icons-vue';

export interface DeviceSettings {
  deviceType: 'desktop' | 'tablet' | 'mobile';
  orientation: 'portrait' | 'landscape';
  scale: number;
}

const props = defineProps<{
  modelValue: DeviceSettings;
}>();

const emit = defineEmits(['update:modelValue', 'change']);

const settings = reactive<DeviceSettings>({ ...props.modelValue });

watch(() => props.modelValue, (newVal: DeviceSettings) => {
  settings.deviceType = newVal.deviceType;
  settings.orientation = newVal.orientation;
  settings.scale = newVal.scale;
}, { deep: true });

const handleChange = () => {
  emit('update:modelValue', { ...settings });
  emit('change', { ...settings });
};

const toggleOrientation = () => {
  settings.orientation = settings.orientation === 'portrait' ? 'landscape' : 'portrait';
  handleChange();
};
</script>

<style scoped>
.device-toolbar {
  padding: 8px 16px;
  background-color: #fafafa;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  align-items: center;
}
.scale-label {
  font-size: 14px;
  color: #666;
  margin-right: 8px;
}
.scale-value {
  margin-left: 8px;
  font-size: 14px;
  color: #666;
  min-width: 40px;
  display: inline-block;
}
</style>
