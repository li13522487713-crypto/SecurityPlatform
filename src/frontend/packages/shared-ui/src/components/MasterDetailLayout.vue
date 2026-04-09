<template>
  <div class="master-detail-container" :class="{ 'has-detail': detailVisible }">
    <div class="master-list" :style="masterStyle">
      <slot name="master"></slot>
    </div>

    <div v-if="detailVisible" class="detail-panel">
      <slot name="detail"></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = withDefaults(
  defineProps<{
    detailVisible?: boolean;
    masterWidth?: number;
  }>(),
  {
    detailVisible: false,
    masterWidth: 380
  }
);

const masterStyle = computed(() => {
  if (props.detailVisible) {
    return {
      width: `${props.masterWidth}px`,
      minWidth: `${props.masterWidth}px`
    };
  }
  return {};
});
</script>

<style scoped>
.master-detail-container {
  flex: 1;
  display: flex;
  overflow: hidden;
  background: var(--color-bg-base);
}

.master-list {
  width: 100%;
  max-width: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--color-bg-base);
  transition: all 0.3s;
}

.has-detail .master-list {
  border-right: 1px solid var(--color-border);
  background: var(--color-bg-container);
}

.detail-panel {
  flex: 1;
  min-width: 0;
  background: var(--color-bg-container);
  box-shadow: -2px 0 8px rgba(0, 0, 0, 0.02);
  z-index: 2;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

@media screen and (max-width: 768px) {
  .master-detail-container {
    position: relative;
  }

  .has-detail .master-list {
    display: none;
  }
}
</style>
