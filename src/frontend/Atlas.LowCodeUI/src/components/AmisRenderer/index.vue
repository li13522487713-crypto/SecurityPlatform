<template>
  <div ref="containerRef" class="atlas-amis-renderer" :class="{ 'is-debug': debug }"></div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch, toRaw } from "vue";
import type { AmisSchema, AmisEnv, AmisRendererProps, AmisActionContext } from "@/types/amis";
import { useAmisEnv } from "@/composables/useAmisEnv";

const props = withDefaults(defineProps<AmisRendererProps>(), {
  data: () => ({}),
  theme: "cxd",
  locale: "zh-CN",
  lazyLoad: false,
  debug: false,
});

const emit = defineEmits<{
  (e: "action", context: AmisActionContext): void;
}>();

const containerRef = ref<HTMLElement | null>(null);
const isMounted = ref(false);

// React root 实例（动态导入 react-dom/client）
let reactRoot: { render: (element: unknown) => void; unmount: () => void } | null = null;

// 动态加载 amis.render 的缓存 Promise
let amisRenderPromise: Promise<(schema: Record<string, unknown>, props?: Record<string, unknown>, env?: Record<string, unknown>) => unknown> | null = null;

function loadAmisRender() {
  if (!amisRenderPromise) {
    amisRenderPromise = import("amis").then((m) => m.render);
  }
  return amisRenderPromise;
}

/** 深拷贝去除 Vue 代理 */
function normalizeValue<T>(value: T): T {
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
}

/** 合并用户传入的 env 覆盖与默认 env */
function buildEnv(): AmisEnv {
  const defaultEnv = useAmisEnv({
    locale: props.locale,
    theme: props.theme,
  });

  if (!props.env) return defaultEnv;

  return {
    ...defaultEnv,
    ...props.env,
    // 确保 fetcher 总是存在
    fetcher: props.env.fetcher ?? defaultEnv.fetcher,
    notify: props.env.notify ?? defaultEnv.notify,
    alert: props.env.alert ?? defaultEnv.alert,
    confirm: props.env.confirm ?? defaultEnv.confirm,
  };
}

async function renderSchema(): Promise<void> {
  const container = containerRef.value;
  if (!container) return;

  if (!reactRoot) {
    const { createRoot } = await import("react-dom/client");
    if (!isMounted.value) return;
    reactRoot = createRoot(container);
  }

  const renderAmis = await loadAmisRender();
  if (!isMounted.value) return;

  const rawSchema = normalizeValue(props.schema);
  const data = normalizeValue(props.data ?? {});
  const env = buildEnv();

  const element = renderAmis(
    rawSchema as Record<string, unknown>,
    { data },
    env as unknown as Record<string, unknown>,
  );

  reactRoot.render(element);
}

onMounted(() => {
  isMounted.value = true;
  void renderSchema();
});

watch(
  [() => props.schema, () => props.data, () => props.locale, () => props.theme],
  () => {
    if (isMounted.value) {
      void renderSchema();
    }
  },
  { deep: true },
);

onBeforeUnmount(() => {
  isMounted.value = false;
  reactRoot?.unmount();
  reactRoot = null;
  if (containerRef.value) {
    containerRef.value.replaceChildren();
  }
});
</script>

<style>
.atlas-amis-renderer {
  min-height: 100px;
  position: relative;
}

.atlas-amis-renderer.is-debug::after {
  content: "DEBUG";
  position: absolute;
  top: 4px;
  right: 4px;
  background: rgba(255, 165, 0, 0.8);
  color: #fff;
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 3px;
  pointer-events: none;
  z-index: 9999;
}
</style>
