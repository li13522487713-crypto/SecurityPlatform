<template>
  <div ref="containerRef" class="amis-container"></div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch, toRaw } from "vue";
import "amis/lib/themes/default.css";
import "amis/lib/helper.css";
import "amis/sdk/iconfont.css";
import { createRoot, type Root } from "react-dom/client";
import type { Schema } from "amis-core";
import type { AmisSchema } from "@/types/amis";
import type { JsonValue } from "@/types/api";
import { createAmisEnv } from "@/amis/amis-env";
import { AmisSchemaPreprocessor } from "@/utils/AmisSchemaPreprocessor";

interface Props {
  schema: AmisSchema;
  data?: Record<string, JsonValue>;
}

const props = defineProps<Props>();
const containerRef = ref<HTMLElement | null>(null);
const rootRef = ref<Root | null>(null);
const amisEnv = createAmisEnv();
const emptyData: Record<string, JsonValue> = {};
const isMounted = ref(false);

let amisRenderPromise: Promise<typeof import("amis")["render"]> | null = null;

function loadAmisRender() {
  if (!amisRenderPromise) {
    amisRenderPromise = import("amis").then((module) => module.render);
  }
  return amisRenderPromise;
}

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

const renderSchema = async () => {
  const container = containerRef.value;
  if (!container) return;
  if (!rootRef.value) {
    rootRef.value = createRoot(container);
  }
  const renderAmis = await loadAmisRender();
  if (!isMounted.value) return;
  const rawSchema = normalizeValue(props.schema);
  const data = normalizeValue(props.data ?? emptyData);
  
  // 注入高级变量沙盒预处理器
  const processedSchema = AmisSchemaPreprocessor.process(rawSchema, data as Record<string, any>);
  
  const element = renderAmis(processedSchema as unknown as Schema, { data }, amisEnv as unknown as Record<string, unknown>);
  rootRef.value.render(element);
};

onMounted(() => {
  isMounted.value = true;
  void renderSchema();
});

watch(
  () => props.schema,
  () => {
    void renderSchema();
  },
  { deep: true }
);

onBeforeUnmount(() => {
  isMounted.value = false;
  rootRef.value?.unmount();
  rootRef.value = null;
  if (containerRef.value) {
    containerRef.value.replaceChildren();
  }
});
</script>

<style scoped>
.amis-container {
  min-height: 200px;
}
</style>
