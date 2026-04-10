<template>
  <div class="monaco-editor-wrapper">
    <div v-if="ready" ref="containerRef" class="editor-container"></div>
    <a-textarea
      v-else
      v-model:value="fallbackValue"
      :rows="8"
      style="font-family: Consolas, 'Courier New', monospace"
      @change="emitFallback"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from "vue";

const props = defineProps<{
  value: string;
  language?: string;
}>();

const emit = defineEmits<{
  (e: "update:value", value: string): void;
}>();

const containerRef = ref<HTMLElement | null>(null);
const ready = ref(false);
const fallbackValue = ref(props.value);

let monacoInstance: unknown;
let editorInstance: { dispose: () => void; getValue: () => string; onDidChangeModelContent: (cb: () => void) => { dispose: () => void }; setValue: (v: string) => void } | null = null;
let changeDisposable: { dispose: () => void } | null = null;

watch(
  () => props.value,
  (value) => {
    fallbackValue.value = value;
    if (editorInstance && editorInstance.getValue() !== value) {
      editorInstance.setValue(value);
    }
  }
);

async function initMonaco() {
  if (!containerRef.value) return;
  try {
    monacoInstance = await import("monaco-editor");
    const monaco = monacoInstance as {
      editor: {
        create: (
          element: HTMLElement,
          options: Record<string, unknown>
        ) => {
          dispose: () => void;
          getValue: () => string;
          setValue: (value: string) => void;
          onDidChangeModelContent: (cb: () => void) => { dispose: () => void };
        };
      };
    };

    editorInstance = monaco.editor.create(containerRef.value, {
      value: props.value,
      language: props.language ?? "javascript",
      theme: "vs-dark",
      automaticLayout: true,
      minimap: { enabled: false },
      fontSize: 13,
      lineNumbers: "on",
      scrollBeyondLastLine: false
    });

    changeDisposable = editorInstance.onDidChangeModelContent(() => {
      emit("update:value", editorInstance?.getValue() ?? "");
    });
    ready.value = true;
  } catch {
    ready.value = false;
  }
}

function emitFallback() {
  emit("update:value", fallbackValue.value);
}

onMounted(() => {
  void initMonaco();
});

onUnmounted(() => {
  changeDisposable?.dispose();
  editorInstance?.dispose();
  changeDisposable = null;
  editorInstance = null;
  monacoInstance = null;
});
</script>

<style scoped>
.monaco-editor-wrapper {
  width: 100%;
}

.editor-container {
  width: 100%;
  height: 280px;
  border: 1px solid #30363d;
  border-radius: 6px;
  overflow: hidden;
}
</style>
