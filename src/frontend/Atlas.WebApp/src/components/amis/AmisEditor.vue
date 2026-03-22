<template>
  <div ref="containerRef" class="amis-editor-container" :style="containerStyle"></div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch, toRaw, computed, onUnmounted } from "vue";
import "amis/lib/themes/default.css";
import "amis/lib/helper.css";
import "amis/sdk/iconfont.css";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { JsonValue } from "@/types/api";
import { translate } from "@/i18n";
import type { AmisPluginConfig } from "./amis-plugins";
import { getRegisteredPlugins } from "./amis-plugins";
import { registerBusinessPlugins } from "./amis-plugins/business-plugins";

registerBusinessPlugins();

interface Props {
  schema: Record<string, unknown>;
  height?: string;
  preview?: boolean;
  isMobile?: boolean;
  theme?: string;
  plugins?: AmisPluginConfig[];
}

const props = withDefaults(defineProps<Props>(), {
  height: "100%",
  preview: false,
  isMobile: false,
  theme: "cxd",
  plugins: () => [],
});

const emit = defineEmits<{
  (e: "change", schema: Record<string, unknown>): void;
  (e: "save", schema: Record<string, unknown>): void;
}>();

const containerRef = ref<HTMLElement | null>(null);
const rootRef = ref<any>(null);
const editorRef = ref<any>(null);

const containerStyle = computed(() => ({
  height: props.height,
  width: "100%",
  overflow: "hidden"
}));

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

/**
 * Dynamically import amis-editor (React-based) and render into the container.
 * amis-editor is a React component, so we use react-dom/client createRoot
 * to mount it inside a Vue-managed DOM element.
 */
const renderEditor = async () => {
  const container = containerRef.value;
  if (!container) return;

  try {
    const React  = await import("react");

    if (!isMounted.value) return;
    const { createRoot }  = await import("react-dom/client");

    if (!isMounted.value) return;
    // Keep amis-editor optional: avoid Vite pre-bundling scan on a static literal.
    const amisEditorModuleName = "amis-editor";
    const { Editor }  = await import(/* @vite-ignore */ amisEditorModuleName);

    if (!isMounted.value) return;

    if (!rootRef.value) {
      rootRef.value = createRoot(container);
    }

    const schema = normalizeValue(props.schema);

    const allPlugins = [...getRegisteredPlugins(), ...props.plugins];
    const pluginInstances = allPlugins
      .filter((p) => p.editorConfig?.scaffold)
      .map((p) => {
        try {
          const { registerEditorPlugin, BasePlugin } = await import(/* @vite-ignore */ amisEditorModuleName) as {
            registerEditorPlugin: (cls: unknown) => void;
            BasePlugin: new () => Record<string, unknown>;
          };

          class DynamicPlugin extends BasePlugin {
            rendererName = p.name;
            name = p.displayName;
            description = p.description ?? "";
            tags = (p.editorConfig?.tags as string[]) ?? [p.group];
            icon = p.icon ?? "fa fa-puzzle-piece";
            scaffold = p.editorConfig?.scaffold ?? p.schema;
            previewSchema = p.editorConfig?.previewSchema ?? p.schema;
          }

          registerEditorPlugin(DynamicPlugin);
          return DynamicPlugin;
        } catch {
          return null;
        }
      });

    const editorProps = {
      value: schema,
      onChange: (value: Record<string, unknown>) => {
        emit("change", value);
      },
      preview: props.preview,
      isMobile: props.isMobile,
      theme: props.theme,
      className: "amis-editor-instance"
    };

    const element = React.createElement(Editor, editorProps);
    rootRef.value.render(element);
  } catch (error) {
    // amis-editor might not be installed; fall back to JSON editor
    console.warn("amis-editor not available, falling back to JSON editor:", error);
    renderFallbackEditor(container);
  }
};

/**
 * Fallback: render a JSON text editor when amis-editor is not available.
 */
const renderFallbackEditor = (container: HTMLElement) => {
  container.replaceChildren();
  const wrapper = document.createElement("div");
  wrapper.style.cssText = "display:flex;flex-direction:column;height:100%;padding:16px;gap:12px;";

  const header = document.createElement("div");
  header.style.cssText = "display:flex;justify-content:space-between;align-items:center;";

  const titleSpan = document.createElement("span");
  titleSpan.style.cssText = "font-size:14px;font-weight:500;";
  titleSpan.textContent = translate("amisEditor.title");

  const btnGroup = document.createElement("div");
  btnGroup.style.cssText = "display:flex;gap:8px;";

  const formatBtn = document.createElement("button");
  formatBtn.id = "amis-format-btn";
  formatBtn.style.cssText = "padding:4px 12px;border:1px solid #d9d9d9;border-radius:4px;background:#fff;cursor:pointer;font-size:12px;";
  formatBtn.textContent = translate("amisEditor.format");

  const saveBtn = document.createElement("button");
  saveBtn.id = "amis-save-btn";
  saveBtn.style.cssText = "padding:4px 12px;border:1px solid #1890ff;border-radius:4px;background:#1890ff;color:#fff;cursor:pointer;font-size:12px;";
  saveBtn.textContent = translate("amisEditor.save");

  btnGroup.appendChild(formatBtn);
  btnGroup.appendChild(saveBtn);
  header.appendChild(titleSpan);
  header.appendChild(btnGroup);

  const textarea = document.createElement("textarea");
  textarea.id = "amis-schema-textarea";
  textarea.style.cssText = "flex:1;font-family:monospace;font-size:13px;line-height:1.5;padding:12px;border:1px solid #d9d9d9;border-radius:4px;resize:none;outline:none;";
  textarea.value = JSON.stringify(normalizeValue(props.schema), null, 2);

  textarea.addEventListener("input", () => {
    try {
      const parsed = JSON.parse(textarea.value) as Record<string, unknown>;
      textarea.style.borderColor = "#d9d9d9";
      emit("change", parsed);
    } catch {
      textarea.style.borderColor = "#ff4d4f";
    }
  });

  wrapper.appendChild(header);
  wrapper.appendChild(textarea);
  container.appendChild(wrapper);

  formatBtn.addEventListener("click", () => {
    try {
      const parsed = JSON.parse(textarea.value);
      textarea.value = JSON.stringify(parsed, null, 2);
    } catch {
      // ignore
    }
  });

  saveBtn.addEventListener("click", () => {
    try {
      const parsed = JSON.parse(textarea.value) as Record<string, unknown>;
      emit("save", parsed);
    } catch {
      // ignore
    }
  });
};

onMounted(() => {
  renderEditor();
});

watch(
  () => props.preview,
  () => {
    renderEditor();
  }
);

onBeforeUnmount(() => {
  rootRef.value?.unmount();
  rootRef.value = null;
  if (containerRef.value) {
    containerRef.value.replaceChildren();
  }
});

/**
 * Expose the current schema for parent components to read.
 */
defineExpose({
  getSchema: (): Record<string, unknown> => {
    const textarea = containerRef.value?.querySelector("#amis-schema-textarea") as HTMLTextAreaElement | null;
    if (textarea) {
      try {
        return JSON.parse(textarea.value) as Record<string, unknown>;
      } catch {
        return normalizeValue(props.schema);
      }
    }
    return normalizeValue(props.schema);
  }
});
</script>

<style>
.amis-editor-container {
  position: relative;
  background: #f5f5f5;
}

/* Override amis-editor default styles for better integration */
.amis-editor-instance {
  height: 100% !important;
}
</style>
