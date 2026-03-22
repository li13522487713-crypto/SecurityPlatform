<template>
  <div ref="bridgeContainer" class="property-editor-bridge">
    <slot />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from "vue";

interface Props {
  value?: unknown;
  onChange?: (value: unknown) => void;
}

const props = defineProps<Props>();
const bridgeContainer = ref<HTMLElement | null>(null);

const emit = defineEmits<{
  (e: "update:value", value: unknown): void;
}>();

function handleChange(newValue: unknown) {
  emit("update:value", newValue);
  props.onChange?.(newValue);
}

onMounted(() => {
  if (bridgeContainer.value) {
    bridgeContainer.value.setAttribute("data-bridge", "vue3");
  }
});

onBeforeUnmount(() => {
  /* cleanup if needed */
});

defineExpose({ handleChange });
</script>

<style scoped>
.property-editor-bridge {
  width: 100%;
}
</style>
