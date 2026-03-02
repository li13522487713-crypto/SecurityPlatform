<template>
  <div class="screenfull-container" @click="click">
    <a-tooltip placement="bottom">
      <template #title>
        <span>{{ isFullscreen ? '退出全屏' : '全屏' }}</span>
      </template>
      <fullscreen-outlined v-if="!isFullscreen" class="screenfull-icon" />
      <fullscreen-exit-outlined v-else class="screenfull-icon" />
    </a-tooltip>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { message } from 'ant-design-vue';
import screenfull from 'screenfull';
import { FullscreenOutlined, FullscreenExitOutlined } from '@ant-design/icons-vue';

const isFullscreen = ref(false);

const click = () => {
  if (!screenfull.isEnabled) {
    message.warning('您的浏览器不支持全屏功能');
    return;
  }
  screenfull.toggle();
};

const change = () => {
  if (screenfull.isEnabled) {
    isFullscreen.value = screenfull.isFullscreen;
  }
};

onMounted(() => {
  if (screenfull.isEnabled) {
    screenfull.on('change', change);
  }
});

onUnmounted(() => {
  if (screenfull.isEnabled) {
    screenfull.off('change', change);
  }
});
</script>

<style scoped>
.screenfull-container {
  display: inline-block;
  cursor: pointer;
  padding: 0 12px;
  height: 100%;
  transition: background 0.3s;
  color: rgba(255, 255, 255, 0.85); /* 适配渐变 header */
}

.screenfull-container:hover {
  background: rgba(0, 0, 0, 0.025);
  color: #fff;
}

.screenfull-icon {
  font-size: 18px;
  vertical-align: middle;
}
</style>
