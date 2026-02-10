<template>
  <div ref="containerRef" class="amis-container"></div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch, toRaw } from "vue";
import { render as renderAmis } from "amis";
import { createRoot, type Root } from "react-dom/client";
import type { Schema } from "amis-core";
import type { AmisSchema } from "@/types/amis";
import type { JsonValue } from "@/types/api";
import { createAmisEnv } from "@/amis/amis-env";

interface Props {
  schema: AmisSchema;
  data?: Record<string, JsonValue>;
}

const props = defineProps<Props>();
const containerRef = ref<HTMLElement | null>(null);
const rootRef = ref<Root | null>(null);
const amisEnv = createAmisEnv();
const emptyData: Record<string, JsonValue> = {};

const normalizeValue = <T>(value: T): T => {
  const raw = toRaw(value) as T;
  if (typeof structuredClone === "function") {
    try {
      return structuredClone(raw);
    } catch {
      return raw;
    }
  }
  try {
    return JSON.parse(JSON.stringify(raw)) as T;
  } catch {
    return raw;
  }
};

const renderSchema = () => {
  const container = containerRef.value;
  if (!container) return;
  if (!rootRef.value) {
    rootRef.value = createRoot(container);
  }
  const schema = normalizeValue(props.schema);
  const data = normalizeValue(props.data ?? emptyData);
  const element = renderAmis(schema as unknown as Schema, { data }, amisEnv as unknown as Record<string, unknown>);
  rootRef.value.render(element);
};

onMounted(() => {
  renderSchema();
});

watch(
  () => props.schema,
  () => {
    renderSchema();
  },
  { deep: true }
);

onBeforeUnmount(() => {
  rootRef.value?.unmount();
  rootRef.value = null;
  if (containerRef.value) {
    containerRef.value.innerHTML = "";
  }
});
</script>

<style scoped>
.amis-container {
  min-height: 200px;
}
</style>
