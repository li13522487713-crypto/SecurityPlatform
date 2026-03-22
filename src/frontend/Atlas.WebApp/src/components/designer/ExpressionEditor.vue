<template>
  <div class="expression-editor">
    <a-textarea
      ref="editorRef"
      v-model:value="expression"
      :rows="3"
      placeholder="[Status = 'Active' and Amount > 100]"
      class="expression-textarea"
      @input="handleInput"
    />
    <div class="expression-toolbar">
      <a-tag
        v-for="field in fields"
        :key="field"
        class="field-tag"
        @click="insertField(field)"
      >
        {{ field }}
      </a-tag>
    </div>
    <div class="operator-bar">
      <a-button
        v-for="op in operators"
        :key="op"
        size="small"
        @click="insertOperator(op)"
      >
        {{ op }}
      </a-button>
    </div>
    <div v-if="validationError" class="validation-error">
      {{ validationError }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";

interface Props {
  modelValue?: string;
  fields?: string[];
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: "",
  fields: () => [],
});

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const expression = ref(props.modelValue);
const validationError = ref("");
const editorRef = ref<{ resizableTextArea?: { textArea?: HTMLTextAreaElement } } | null>(null);

const operators = ["=", "!=", ">", "<", ">=", "<=", "contains", "and", "or"];

function insertField(field: string) {
  expression.value += ` ${field} `;
  emitChange();
}

function insertOperator(op: string) {
  expression.value += ` ${op} `;
  emitChange();
}

function handleInput() {
  validate();
  emitChange();
}

function validate() {
  validationError.value = "";
  const val = expression.value.trim();
  if (!val) return;

  const bracketStart = val.startsWith("[");
  const bracketEnd = val.endsWith("]");
  if (bracketStart !== bracketEnd) {
    validationError.value = "Mismatched brackets";
  }
}

function emitChange() {
  emit("update:modelValue", expression.value);
}

watch(
  () => props.modelValue,
  (val) => {
    expression.value = val;
  },
);
</script>

<style scoped>
.expression-editor {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.expression-textarea {
  font-family: monospace;
  font-size: 13px;
}

.expression-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.field-tag {
  cursor: pointer;
}

.field-tag:hover {
  background: #e6f7ff;
  border-color: #1890ff;
}

.operator-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.validation-error {
  color: #ff4d4f;
  font-size: 12px;
}
</style>
