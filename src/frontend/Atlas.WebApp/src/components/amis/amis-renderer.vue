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
  schemaRevision?: number;
  data?: Record<string, JsonValue>;
  dataRevision?: number;
}

const props = defineProps<Props>();
const containerRef = ref<HTMLElement | null>(null);
const rootRef = ref<Root | null>(null);
const amisEnv = createAmisEnv();
const emptyData: Record<string, JsonValue> = {};
const isMounted = ref(false);
let renderQueued = false;
let renderRevision = 0;

let amisRenderPromise: Promise<typeof import("amis")["render"]> | null = null;

function loadAmisRender() {
  if (!amisRenderPromise) {
    amisRenderPromise = import("amis").then((module) => module.render);
  }
  return amisRenderPromise;
}

const normalizeValue = <T>(value: T): T => toRaw(value) as T;

const renderSchema = async () => {
  const container = containerRef.value;
  if (!container) return;
  const currentRevision = ++renderRevision;
  if (!rootRef.value) {
    rootRef.value = createRoot(container);
  }
  const renderAmis = await loadAmisRender();
  if (!isMounted.value || currentRevision !== renderRevision) return;
  const rawSchema = normalizeValue(props.schema);
  const data = normalizeValue(props.data ?? emptyData);
  
  // 注入高级变量沙盒预处理器
  const processedSchema = AmisSchemaPreprocessor.process(rawSchema, data as Record<string, any>);
  
  // amis 内部部分 renderer 仍依赖 findDOMNode（第三方技术债）。
  // findDOMNode 告警已在 amis-env.ts 模块初始化阶段统一屏蔽，本层无需额外处理。
  const element = renderAmis(processedSchema as unknown as Schema, { data }, amisEnv as unknown as Record<string, unknown>);
  rootRef.value.render(element);
};

const requestRenderSchema = () => {
  if (renderQueued) {
    return;
  }
  renderQueued = true;
  queueMicrotask(() => {
    renderQueued = false;
    if (!isMounted.value) {
      return;
    }
    void renderSchema();
  });
};

onMounted(() => {
  isMounted.value = true;
  requestRenderSchema();
});

watch(
  () => [props.schemaRevision, props.dataRevision],
  () => {
    requestRenderSchema();
  }
);

watch(
  () => props.schema,
  () => {
    requestRenderSchema();
  },
  { deep: false }
);

watch(
  () => props.data,
  () => {
    requestRenderSchema();
  },
  { deep: false }
);

onBeforeUnmount(() => {
  isMounted.value = false;
  renderRevision += 1;
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
