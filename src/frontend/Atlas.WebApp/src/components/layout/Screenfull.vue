<template>
  <div class="screenfull-container" @click="click">
    <a-tooltip placement="bottom">
      <template #title>
        <span>{{ isFullscreen ? t("layoutChrome.exitFullscreen") : t("layoutChrome.fullscreen") }}</span>
      </template>
      <fullscreen-outlined v-if="!isFullscreen" class="screenfull-icon" />
      <fullscreen-exit-outlined v-else class="screenfull-icon" />
    </a-tooltip>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import screenfull from "screenfull";
import { FullscreenOutlined, FullscreenExitOutlined } from "@ant-design/icons-vue";

defineOptions({
  name: "ScreenfullToggle"
});

const { t } = useI18n();
const isFullscreen = ref(false);

const click = () => {
  if (!screenfull.isEnabled) {
    message.warning(t("layoutChrome.fullscreenUnsupported"));
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
    screenfull.on("change", change);
  }
});

onUnmounted(() => {
  if (screenfull.isEnabled) {
    screenfull.off("change", change);
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
