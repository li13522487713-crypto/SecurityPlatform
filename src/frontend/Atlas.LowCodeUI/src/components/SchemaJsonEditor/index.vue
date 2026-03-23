<template>
  <div class="atlas-schema-json-editor" :style="{ height: height }">
    <div class="atlas-json-editor-toolbar">
      <span class="atlas-json-editor-title">Schema JSON</span>
      <div class="atlas-json-editor-actions">
        <span v-if="hasError" class="atlas-json-editor-error">JSON 语法错误</span>
        <span v-else class="atlas-json-editor-valid">✓ 有效</span>
        <button class="atlas-json-editor-btn" @click="formatJson" title="格式化">格式化</button>
        <button class="atlas-json-editor-btn" @click="compactJson" title="压缩">压缩</button>
        <button class="atlas-json-editor-btn atlas-json-editor-btn--primary" @click="applyChanges" :disabled="hasError" title="应用">应用</button>
      </div>
    </div>
    <textarea
      ref="textareaRef"
      class="atlas-json-editor-textarea"
      :value="jsonText"
      :readonly="readonly"
      @input="onInput"
      spellcheck="false"
    ></textarea>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from "vue";
import type { AmisSchema } from "@/types/amis";

interface Props {
  modelValue: AmisSchema;
  height?: string;
  readonly?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  height: "400px",
  readonly: false,
});

const emit = defineEmits<{
  (e: "update:modelValue", schema: AmisSchema): void;
}>();

const textareaRef = ref<HTMLTextAreaElement | null>(null);
const jsonText = ref("");
const hasError = ref(false);

function schemaToText(schema: AmisSchema): string {
  return JSON.stringify(schema, null, 2);
}

onMounted(() => {
  jsonText.value = schemaToText(props.modelValue);
});

watch(() => props.modelValue, (newSchema) => {
  const newText = schemaToText(newSchema);
  // 仅在外部变化时更新（避免编辑中被覆盖）
  if (newText !== jsonText.value) {
    try {
      const currentParsed = JSON.parse(jsonText.value);
      if (JSON.stringify(currentParsed) !== JSON.stringify(newSchema)) {
        jsonText.value = newText;
        hasError.value = false;
      }
    } catch {
      jsonText.value = newText;
      hasError.value = false;
    }
  }
}, { deep: true });

function onInput(e: Event): void {
  const target = e.target as HTMLTextAreaElement;
  jsonText.value = target.value;

  try {
    JSON.parse(target.value);
    hasError.value = false;
  } catch {
    hasError.value = true;
  }
}

function formatJson(): void {
  try {
    const parsed = JSON.parse(jsonText.value);
    jsonText.value = JSON.stringify(parsed, null, 2);
    hasError.value = false;
  } catch {
    // 保持原样
  }
}

function compactJson(): void {
  try {
    const parsed = JSON.parse(jsonText.value);
    jsonText.value = JSON.stringify(parsed);
    hasError.value = false;
  } catch {
    // 保持原样
  }
}

function applyChanges(): void {
  if (hasError.value) return;
  try {
    const parsed = JSON.parse(jsonText.value) as AmisSchema;
    emit("update:modelValue", parsed);
  } catch {
    // ignore
  }
}

defineExpose({
  formatJson,
  compactJson,
  applyChanges,
  getSchema: (): AmisSchema | null => {
    try {
      return JSON.parse(jsonText.value) as AmisSchema;
    } catch {
      return null;
    }
  },
});
</script>

<style>
.atlas-schema-json-editor {
  display: flex;
  flex-direction: column;
  border: 1px solid #e8e8e8;
  border-radius: 6px;
  overflow: hidden;
  background: #fff;
}

.atlas-json-editor-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 40px;
  padding: 0 12px;
  border-bottom: 1px solid #e8e8e8;
  background: #fafafa;
  flex-shrink: 0;
}

.atlas-json-editor-title {
  font-size: 13px;
  font-weight: 500;
  color: #1f2329;
}

.atlas-json-editor-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.atlas-json-editor-error {
  font-size: 12px;
  color: #f53f3f;
}

.atlas-json-editor-valid {
  font-size: 12px;
  color: #00b42a;
}

.atlas-json-editor-btn {
  padding: 3px 10px;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  background: #fff;
  cursor: pointer;
  font-size: 12px;
  color: #434c5a;
  transition: all 0.2s;
}

.atlas-json-editor-btn:hover {
  border-color: #0052d9;
  color: #0052d9;
}

.atlas-json-editor-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.atlas-json-editor-btn--primary {
  background: #0052d9;
  border-color: #0052d9;
  color: #fff;
}

.atlas-json-editor-btn--primary:hover {
  background: #3b82f6;
  border-color: #3b82f6;
}

.atlas-json-editor-textarea {
  flex: 1;
  font-family: "JetBrains Mono", "Fira Code", "SF Mono", Consolas, monospace;
  font-size: 13px;
  line-height: 1.6;
  padding: 12px;
  border: none;
  resize: none;
  outline: none;
  color: #1f2329;
  background: #fff;
  tab-size: 2;
}

.atlas-json-editor-textarea:focus {
  background: #fafbfc;
}
</style>
