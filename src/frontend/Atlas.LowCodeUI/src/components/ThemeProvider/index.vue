<template>
  <div class="atlas-theme-provider" :class="themeClass" :style="cssVarsStyle">
    <slot />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

export type ThemeMode = "light" | "dark" | "auto";

interface Props {
  /** 主题模式 */
  mode?: ThemeMode;
  /** 自定义 CSS 变量覆盖 */
  cssVars?: Record<string, string>;
  /** 辅助 CSS class */
  className?: string;
}

const props = withDefaults(defineProps<Props>(), {
  mode: "light",
  cssVars: () => ({}),
  className: "",
});

const emit = defineEmits<{
  (e: "themeChange", mode: ThemeMode): void;
}>();

const currentMode = ref<ThemeMode>(props.mode);

watch(() => props.mode, (newMode) => {
  currentMode.value = newMode;
});

const resolvedMode = computed(() => {
  if (currentMode.value !== "auto") return currentMode.value;
  if (typeof window !== "undefined" && window.matchMedia) {
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  }
  return "light";
});

const themeClass = computed(() => {
  const classes = [`atlas-theme-${resolvedMode.value}`];
  if (props.className) classes.push(props.className);
  return classes.join(" ");
});

/** 三层主题变量：内置默认 → props.cssVars 覆盖 */
const cssVarsStyle = computed(() => {
  const baseVars: Record<string, string> = resolvedMode.value === "dark"
    ? {
        "--atlas-bg-primary": "#1a1a2e",
        "--atlas-bg-secondary": "#16213e",
        "--atlas-bg-surface": "#0f3460",
        "--atlas-text-primary": "#e8e8e8",
        "--atlas-text-secondary": "#a0a0a0",
        "--atlas-border-color": "#2a2a4a",
        "--atlas-color-primary": "#4da8ff",
        "--atlas-color-primary-hover": "#66b8ff",
        "--atlas-color-error": "#ff6b6b",
        "--atlas-color-success": "#51cf66",
        "--atlas-color-warning": "#fcc419",
        "--atlas-shadow": "0 2px 8px rgba(0,0,0,0.3)",
      }
    : {
        "--atlas-bg-primary": "#ffffff",
        "--atlas-bg-secondary": "#fafafa",
        "--atlas-bg-surface": "#f5f5f5",
        "--atlas-text-primary": "#1f2329",
        "--atlas-text-secondary": "#86909c",
        "--atlas-border-color": "#e8e8e8",
        "--atlas-color-primary": "#0052d9",
        "--atlas-color-primary-hover": "#3b82f6",
        "--atlas-color-error": "#f53f3f",
        "--atlas-color-success": "#00b42a",
        "--atlas-color-warning": "#ff7d00",
        "--atlas-shadow": "0 2px 8px rgba(0,0,0,0.06)",
      };

  return { ...baseVars, ...props.cssVars };
});

/** 切换主题 */
function toggleTheme(): void {
  const newMode: ThemeMode = resolvedMode.value === "dark" ? "light" : "dark";
  currentMode.value = newMode;
  emit("themeChange", newMode);
}

/** 设置主题 */
function setTheme(mode: ThemeMode): void {
  currentMode.value = mode;
  emit("themeChange", mode);
}

defineExpose({ toggleTheme, setTheme, currentMode: resolvedMode });
</script>

<style>
.atlas-theme-provider {
  color: var(--atlas-text-primary);
  background: var(--atlas-bg-primary);
  transition: background 0.3s ease, color 0.3s ease;
}

/* AMIS 组件主题变量覆盖 */
.atlas-theme-dark .amis-container {
  --body-bg: var(--atlas-bg-primary);
  --panel-bg: var(--atlas-bg-secondary);
  --panel-border-color: var(--atlas-border-color);
}

.atlas-theme-dark .amis-container .cxd-Button--primary {
  background-color: var(--atlas-color-primary);
  border-color: var(--atlas-color-primary);
}

.atlas-theme-dark .amis-container .cxd-Table-table > thead > tr > th {
  background-color: var(--atlas-bg-secondary);
  color: var(--atlas-text-secondary);
}

.atlas-theme-dark .amis-container .cxd-TextControl-input,
.atlas-theme-dark .amis-container .cxd-Select {
  background-color: var(--atlas-bg-surface);
  border-color: var(--atlas-border-color);
  color: var(--atlas-text-primary);
}
</style>
