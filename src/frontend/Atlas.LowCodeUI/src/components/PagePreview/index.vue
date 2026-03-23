<template>
  <Teleport to="body">
    <Transition name="atlas-preview-fade">
      <div v-if="visible" class="atlas-page-preview-overlay" @keydown.esc="close">
        <div class="atlas-page-preview-toolbar">
          <span class="atlas-page-preview-title">页面预览</span>
          <button class="atlas-page-preview-close" @click="close" title="关闭 (ESC)">✕</button>
        </div>
        <div class="atlas-page-preview-body">
          <AmisRenderer :schema="schema" :data="data" />
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount } from "vue";
import type { AmisSchema } from "@/types/amis";
import AmisRenderer from "@/components/AmisRenderer/index.vue";

interface Props {
  schema: AmisSchema;
  data?: Record<string, unknown>;
  visible?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  data: () => ({}),
  visible: false,
});

const emit = defineEmits<{
  (e: "update:visible", value: boolean): void;
  (e: "close"): void;
}>();

const visible = ref(props.visible);

watch(() => props.visible, (val) => {
  visible.value = val;
});

function close(): void {
  visible.value = false;
  emit("update:visible", false);
  emit("close");
}

function open(): void {
  visible.value = true;
  emit("update:visible", true);
}

function handleKeydown(e: KeyboardEvent): void {
  if (e.key === "Escape" && visible.value) {
    close();
  }
}

onMounted(() => {
  document.addEventListener("keydown", handleKeydown);
});

onBeforeUnmount(() => {
  document.removeEventListener("keydown", handleKeydown);
});

defineExpose({ open, close });
</script>

<style>
.atlas-page-preview-overlay {
  position: fixed;
  inset: 0;
  z-index: 10000;
  background: #fff;
  display: flex;
  flex-direction: column;
}

.atlas-page-preview-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 48px;
  padding: 0 16px;
  border-bottom: 1px solid #e8e8e8;
  background: #fafafa;
  flex-shrink: 0;
}

.atlas-page-preview-title {
  font-size: 14px;
  font-weight: 500;
  color: #1f2329;
}

.atlas-page-preview-close {
  width: 32px;
  height: 32px;
  border: none;
  border-radius: 4px;
  background: transparent;
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #666;
  transition: all 0.2s;
}

.atlas-page-preview-close:hover {
  background: #f0f0f0;
  color: #333;
}

.atlas-page-preview-body {
  flex: 1;
  overflow: auto;
  padding: 16px;
}

/* Transition */
.atlas-preview-fade-enter-active,
.atlas-preview-fade-leave-active {
  transition: opacity 0.2s ease;
}
.atlas-preview-fade-enter-from,
.atlas-preview-fade-leave-to {
  opacity: 0;
}
</style>
