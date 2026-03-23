<template>
  <div class="atlas-amis-designer" :style="{ height: height }">
    <!-- 顶部工具栏 -->
    <div class="atlas-designer-toolbar">
      <div class="atlas-designer-toolbar-left">
        <span class="atlas-designer-title">AMIS 可视化设计器</span>
      </div>
      <div class="atlas-designer-toolbar-center">
        <button class="atlas-designer-btn" :disabled="!canUndo" @click="handleUndo" title="撤销 (Ctrl+Z)">
          ↩ 撤销
        </button>
        <button class="atlas-designer-btn" :disabled="!canRedo" @click="handleRedo" title="重做 (Ctrl+Y)">
          ↪ 重做
        </button>
        <button class="atlas-designer-btn" :class="{ active: showPreview }" @click="togglePreview">
          {{ showPreview ? '编辑' : '预览' }}
        </button>
      </div>
      <div class="atlas-designer-toolbar-right">
        <button class="atlas-designer-btn" @click="importJson" title="导入 JSON">导入</button>
        <button class="atlas-designer-btn" @click="exportJson" title="导出 JSON">导出</button>
        <button class="atlas-designer-btn atlas-designer-btn--primary" @click="handleSave" title="保存 (Ctrl+S)">
          保存
        </button>
      </div>
    </div>

    <!-- 主体区域 -->
    <div class="atlas-designer-body">
      <template v-if="showPreview">
        <div class="atlas-designer-preview">
          <AmisRenderer :schema="currentSchema" />
        </div>
      </template>
      <template v-else>
        <div class="atlas-designer-canvas">
          <template v-if="!editorActivated">
            <div class="atlas-designer-idle">
              <p class="atlas-designer-idle-hint">
                可视化编辑器未自动加载。点击下方按钮加载 amis-editor（体积较大）。
              </p>
              <button type="button" class="atlas-designer-btn atlas-designer-btn--primary" @click="activateVisualEditor">
                启动可视化编辑器
              </button>
            </div>
          </template>
          <template v-else>
            <!-- 错误层与挂载点同层，保证 ref 在失败时仍存在以便重试 -->
            <div class="atlas-designer-editor-wrap">
              <div v-if="editorLoadError" class="atlas-designer-error-overlay" role="alert">
                <div class="atlas-designer-error">
                  <p class="atlas-designer-error-title">无法加载可视化设计器</p>
                  <p class="atlas-designer-error-msg">{{ editorLoadError }}</p>
                  <p class="atlas-designer-error-hint">
                    请确认已安装 peer 依赖：amis-editor、amis-ui、amis-formula、amis-theme-editor-helper、i18n-runtime（版本需与 amis 6.x 一致）。
                  </p>
                  <button type="button" class="atlas-designer-btn atlas-designer-btn--primary" @click="retryMountEditor">
                    重试
                  </button>
                </div>
              </div>
              <div ref="editorContainerRef" class="atlas-designer-editor-mount"></div>
            </div>
          </template>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount, toRaw, nextTick } from "vue";
import type { ComponentType } from "react";
import * as React from "react";
import type { AmisSchema, AmisDesignerProps } from "@/types/amis";
import { useSchemaHistory } from "@/composables/useSchemaHistory";
import { registerDesignerCustomPluginsOnce } from "@/plugins/register-designer-custom-plugins";
import AmisRenderer from "@/components/AmisRenderer/index.vue";

const props = withDefaults(defineProps<AmisDesignerProps>(), {
  preview: false,
  isMobile: false,
  theme: "cxd",
  height: "100%",
  autoLoadEditor: true,
  prefetchEditor: false,
  sdkComponents: () => [],
  reactComponents: () => [],
  editorPlugins: () => [],
});

const emit = defineEmits<{
  (e: "update:modelValue", schema: AmisSchema): void;
  (e: "save", schema: AmisSchema): void;
}>();

const editorContainerRef = ref<HTMLElement | null>(null);
const showPreview = ref(props.preview);
const isMounted = ref(false);
const editorActivated = ref(props.autoLoadEditor);
const editorLoadError = ref<string | null>(null);

// Schema 历史管理
const { current: currentSchema, canUndo, canRedo, push, undo, redo } = useSchemaHistory(props.modelValue);

// React root for amis-editor
let reactRoot: { render: (element: unknown) => void; unmount: () => void } | null = null;

// 同步外部 modelValue 变化
watch(() => props.modelValue, (newSchema) => {
  const rawNew = JSON.stringify(toRaw(newSchema));
  const rawCurrent = JSON.stringify(toRaw(currentSchema.value));
  if (rawNew !== rawCurrent) {
    push(newSchema);
  }
}, { deep: true });

// Schema 变化时通知父组件
watch(currentSchema, (schema) => {
  emit("update:modelValue", schema);
}, { deep: true });

function handleUndo(): void {
  undo();
  void nextTick(() => {
    void syncEditorAfterHistoryChange();
  });
}

function handleRedo(): void {
  redo();
  void nextTick(() => {
    void syncEditorAfterHistoryChange();
  });
}

async function syncEditorAfterHistoryChange(): Promise<void> {
  if (!editorActivated.value || showPreview.value || !isMounted.value) {
    return;
  }
  await mountAmisEditor();
}

function handleSave(): void {
  emit("save", currentSchema.value);
}

function togglePreview(): void {
  showPreview.value = !showPreview.value;
}

function importJson(): void {
  const input = document.createElement("input");
  input.type = "file";
  input.accept = ".json";
  input.onchange = (e) => {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const schema = JSON.parse(reader.result as string) as AmisSchema;
        push(schema);
        void nextTick(() => {
          void syncEditorAfterHistoryChange();
        });
      } catch {
        console.error("[Atlas Designer] Invalid JSON file");
      }
    };
    reader.readAsText(file);
  };
  input.click();
}

function exportJson(): void {
  const json = JSON.stringify(currentSchema.value, null, 2);
  const blob = new Blob([json], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `amis-schema-${Date.now()}.json`;
  a.click();
  URL.revokeObjectURL(url);
}

function schedulePrefetchEditor(): void {
  const run = (): void => {
    void import("amis-editor").catch(() => {
      /* 预取失败仅忽略 */
    });
  };
  if (typeof requestIdleCallback !== "undefined") {
    requestIdleCallback(run, { timeout: 3000 });
  } else {
    setTimeout(run, 1);
  }
}

function activateVisualEditor(): void {
  editorLoadError.value = null;
  editorActivated.value = true;
  void nextTick(() => {
    void mountAmisEditor();
  });
}

function retryMountEditor(): void {
  editorLoadError.value = null;
  void nextTick(() => {
    void mountAmisEditor();
  });
}

// 挂载 amis-editor（纯拖拽，无 JSON 回退）
async function mountAmisEditor(): Promise<void> {
  const container = editorContainerRef.value;
  if (!container) return;

  editorLoadError.value = null;

  try {
    reactRoot?.unmount();
    reactRoot = null;

    await registerDesignerCustomPluginsOnce({
      sdkComponents: props.sdkComponents,
      reactComponents: props.reactComponents,
    });

    if (!isMounted.value) return;
    const { createRoot } = await import("react-dom/client");
    if (!isMounted.value) return;

    const amisEditorModule = await import("amis-editor");
    if (!isMounted.value) return;

    const Editor = (amisEditorModule as { Editor?: unknown; default?: unknown }).Editor
      ?? (amisEditorModule as { default?: unknown }).default;
    if (!Editor || typeof Editor !== "function") {
      throw new Error("amis-editor 未导出 Editor 组件");
    }

    reactRoot = createRoot(container) as unknown as { render: (element: unknown) => void; unmount: () => void };

    const plugins = props.editorPlugins?.length ? props.editorPlugins : undefined;

    const editorProps: Record<string, unknown> = {
      value: JSON.parse(JSON.stringify(toRaw(currentSchema.value))),
      onChange: (value: Record<string, unknown>) => {
        push(value);
      },
      preview: showPreview.value,
      isMobile: props.isMobile,
      theme: props.theme,
    };
    if (plugins) {
      editorProps.plugins = plugins;
    }

    const element = React.createElement(Editor as ComponentType<Record<string, unknown>>, editorProps);
    reactRoot.render(element);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    editorLoadError.value = message;
    console.error("[Atlas Designer] amis-editor mount failed:", error);
  }
}

watch(showPreview, (isPreview) => {
  if (isPreview) {
    reactRoot?.unmount();
    reactRoot = null;
  } else if (editorActivated.value) {
    void nextTick(() => {
      void mountAmisEditor();
    });
  }
});

// 键盘快捷键
function handleKeydown(e: KeyboardEvent): void {
  if ((e.ctrlKey || e.metaKey) && e.key === "z" && !e.shiftKey) {
    e.preventDefault();
    handleUndo();
  }
  if ((e.ctrlKey || e.metaKey) && (e.key === "y" || (e.key === "z" && e.shiftKey))) {
    e.preventDefault();
    handleRedo();
  }
  if ((e.ctrlKey || e.metaKey) && e.key === "s") {
    e.preventDefault();
    handleSave();
  }
}

onMounted(() => {
  isMounted.value = true;
  document.addEventListener("keydown", handleKeydown);
  if (props.autoLoadEditor) {
    editorActivated.value = true;
    void nextTick(() => {
      void mountAmisEditor();
    });
  }
  if (props.prefetchEditor && !props.autoLoadEditor) {
    schedulePrefetchEditor();
  }
});

onBeforeUnmount(() => {
  isMounted.value = false;
  document.removeEventListener("keydown", handleKeydown);
  reactRoot?.unmount();
  reactRoot = null;
});
</script>
