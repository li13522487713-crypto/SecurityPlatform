<template>
  <div class="sql-editor" :class="{ focused: isFocused }">
    <div class="sql-editor-lines" aria-hidden="true">
      <div v-for="n in lineCount" :key="n" class="line-number">{{ n }}</div>
    </div>
    <textarea
      ref="textareaRef"
      v-model="modelValue"
      class="sql-textarea"
      :placeholder="t('datasource.sqlPlaceholder')"
      spellcheck="false"
      wrap="off"
      @keydown="handleKeydown"
      @focus="isFocused = true"
      @blur="isFocused = false"
      @scroll="syncScroll"
    ></textarea>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();
const modelValue = defineModel<string>({ required: true });
const emit = defineEmits<{
  execute: [];
}>();

const textareaRef = ref<HTMLTextAreaElement>();
const isFocused = ref(false);

const lineCount = computed(() => {
  const lines = (modelValue.value || "").split("\n").length;
  return Math.max(lines, 5);
});

function syncScroll() {
  const lineEl = textareaRef.value?.parentElement?.querySelector(".sql-editor-lines") as HTMLElement | null;
  if (lineEl && textareaRef.value) {
    lineEl.scrollTop = textareaRef.value.scrollTop;
  }
}

function handleKeydown(event: KeyboardEvent) {
  if (event.ctrlKey && event.key === "Enter") {
    event.preventDefault();
    emit("execute");
    return;
  }

  if (event.key === "Tab") {
    event.preventDefault();
    const el = textareaRef.value;
    if (!el) return;
    const start = el.selectionStart;
    const end = el.selectionEnd;
    modelValue.value =
      modelValue.value.substring(0, start) + "  " + modelValue.value.substring(end);
    requestAnimationFrame(() => {
      el.selectionStart = el.selectionEnd = start + 2;
    });
  }
}
</script>

<style scoped>
.sql-editor {
  display: flex;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  background: #fafafa;
  transition: border-color 0.2s;
  overflow: hidden;
}

.sql-editor.focused {
  border-color: #1677ff;
  box-shadow: 0 0 0 2px rgba(22, 119, 255, 0.1);
}

.sql-editor-lines {
  flex-shrink: 0;
  width: 40px;
  padding: 8px 4px 8px 0;
  text-align: right;
  background: #f0f0f0;
  border-right: 1px solid #e0e0e0;
  overflow: hidden;
  user-select: none;
}

.line-number {
  font-family: "Fira Code", Consolas, Monaco, monospace;
  font-size: 13px;
  line-height: 20px;
  color: #999;
  height: 20px;
}

.sql-textarea {
  flex: 1;
  min-height: 120px;
  padding: 8px;
  border: none;
  background: transparent;
  outline: none;
  font-family: "Fira Code", Consolas, Monaco, "Andale Mono", "Ubuntu Mono", monospace;
  font-size: 13px;
  line-height: 20px;
  color: #333;
  resize: vertical;
  white-space: pre;
  overflow-x: auto;
}
</style>
