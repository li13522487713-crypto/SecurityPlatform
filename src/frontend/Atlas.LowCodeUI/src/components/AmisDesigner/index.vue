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
        <!-- 编辑器画布（amis-editor 或 fallback JSON 编辑器） -->
        <div class="atlas-designer-canvas">
          <div ref="editorContainerRef" class="atlas-designer-editor-mount"></div>
        </div>
        <!-- 右侧 JSON 面板 -->
        <div class="atlas-designer-json-panel" v-if="showJsonPanel">
          <SchemaJsonEditor v-model="currentSchema" height="100%" @update:model-value="onJsonChange" />
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount, toRaw } from "vue";
import type { AmisSchema, AmisDesignerProps } from "@/types/amis";
import { useSchemaHistory } from "@/composables/useSchemaHistory";
import AmisRenderer from "@/components/AmisRenderer/index.vue";
import SchemaJsonEditor from "@/components/SchemaJsonEditor/index.vue";

const props = withDefaults(defineProps<AmisDesignerProps>(), {
  preview: false,
  isMobile: false,
  theme: "cxd",
  height: "100%",
});

const emit = defineEmits<{
  (e: "update:modelValue", schema: AmisSchema): void;
  (e: "save", schema: AmisSchema): void;
}>();

const editorContainerRef = ref<HTMLElement | null>(null);
const showPreview = ref(props.preview);
const showJsonPanel = ref(true);
const isMounted = ref(false);

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
}

function handleRedo(): void {
  redo();
}

function handleSave(): void {
  emit("save", currentSchema.value);
}

function togglePreview(): void {
  showPreview.value = !showPreview.value;
}

function onJsonChange(schema: AmisSchema): void {
  push(schema);
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

// 尝试挂载 amis-editor
async function mountAmisEditor(): Promise<void> {
  const container = editorContainerRef.value;
  if (!container) return;

  try {
    const React = await import("react");
    if (!isMounted.value) return;
    const { createRoot } = await import("react-dom/client");
    if (!isMounted.value) return;

    const amisEditorModule = "amis-editor";
    const { Editor } = await import(/* @vite-ignore */ amisEditorModule);
    if (!isMounted.value) return;

    if (!reactRoot) {
      reactRoot = createRoot(container) as unknown as { render: (element: unknown) => void; unmount: () => void };
    }

    // 隐藏 JSON 面板，因为 amis-editor 自己有属性面板
    showJsonPanel.value = false;

    const editorProps = {
      value: JSON.parse(JSON.stringify(toRaw(currentSchema.value))),
      onChange: (value: Record<string, unknown>) => {
        push(value);
      },
      preview: showPreview.value,
      isMobile: props.isMobile,
      theme: props.theme,
    };

    const element = React.createElement(Editor, editorProps);
    reactRoot!.render(element);
  } catch {
    // amis-editor 不可用，使用 JSON 编辑器作为 fallback
    showJsonPanel.value = true;
    console.info("[Atlas Designer] amis-editor not available, using JSON editor fallback");
  }
}

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
  void mountAmisEditor();
});

onBeforeUnmount(() => {
  isMounted.value = false;
  document.removeEventListener("keydown", handleKeydown);
  reactRoot?.unmount();
  reactRoot = null;
});
</script>
