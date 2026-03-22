<template>
  <div class="device-frame-container">
    <div 
      class="device-frame-wrapper" 
      :class="[settings.deviceType, settings.orientation]"
      :style="wrapperStyle"
    >
      <div class="device-screen">
        <slot></slot>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { DeviceSettings } from './DeviceToolbar.vue';

const props = defineProps<{
  settings: DeviceSettings;
}>();

const wrapperStyle = computed(() => {
  const { deviceType, orientation, scale } = props.settings;
  
  if (deviceType === 'desktop') {
    return {
      width: '100%',
      height: '100%',
      transform: `scale(${scale})`,
      transformOrigin: 'top center'
    };
  }

  let width = 375;
  let height = 667;

  if (deviceType === 'tablet') {
    width = 768;
    height = 1024;
  }

  if (orientation === 'landscape') {
    const temp = width;
    width = height;
    height = temp;
  }

  return {
    width: `${width}px`,
    height: `${height}px`,
    transform: `scale(${scale})`,
    transformOrigin: 'top center',
    margin: '0 auto',
    border: '16px solid #333',
    borderRadius: '24px',
    boxShadow: '0 8px 24px rgba(0,0,0,0.15)',
    overflow: 'hidden',
    backgroundColor: '#fff',
    transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)'
  };
});
</script>

<style scoped>
.device-frame-container {
  width: 100%;
  height: 100%;
  background-color: #f0f2f5;
  padding: 24px;
  overflow: auto;
  display: flex;
  justify-content: center;
  align-items: flex-start;
}
.device-frame-wrapper {
  position: relative;
  box-sizing: content-box;
}
.device-screen {
  width: 100%;
  height: 100%;
  overflow: auto;
  position: relative;
  background: white;
}

.device-frame-wrapper.desktop {
  border: none;
  border-radius: 0;
  box-shadow: none;
  background-color: transparent;
}
</style>
