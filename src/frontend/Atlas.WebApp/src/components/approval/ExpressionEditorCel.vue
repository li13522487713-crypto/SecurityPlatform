<template>
  <div class="expr-editor">
    <a-textarea
      :value="modelValue"
      :rows="rows"
      :placeholder="placeholder"
      @update:value="handleInput"
    />
    <div class="expr-toolbar">
      <a-space size="small">
        <a-tag color="blue" v-for="v in variables" :key="v" @click="insertVariable(v)">{{ v }}</a-tag>
      </a-space>
      <span class="expr-status" :class="{ invalid: !isValid }">{{ statusText }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(defineProps<{
  modelValue: string;
  placeholder?: string;
  rows?: number;
  variables?: string[];
}>(), {
  placeholder: '请输入 CEL 表达式，例如：form.amount > 1000 && user.roles.exists(r, r == "FinanceManager")',
  rows: 4,
  variables: () => ['tenant', 'user', 'app', 'project', 'form', 'record', 'workflow', 'task', 'now']
});

const emit = defineEmits<{
  'update:modelValue': [value: string];
  validate: [valid: boolean, message: string];
}>();

const forbiddenPatterns = [/javascript:/i, /eval\(/i, /function\(/i, /<script/i];

const validateExpression = (value: string) => {
  const expr = value.trim();
  if (!expr) {
    return { valid: true, message: '空表达式（视为不启用）' };
  }
  if (expr.length > 4096) {
    return { valid: false, message: '表达式长度超过 4096 字符限制' };
  }
  if (forbiddenPatterns.some((pattern) => pattern.test(expr))) {
    return { valid: false, message: '表达式包含不安全内容' };
  }

  let balance = 0;
  for (const ch of expr) {
    if (ch === '(') balance += 1;
    if (ch === ')') balance -= 1;
    if (balance < 0) return { valid: false, message: '括号不匹配' };
  }
  if (balance !== 0) {
    return { valid: false, message: '括号不匹配' };
  }

  return { valid: true, message: '语法检查通过（基础校验）' };
};

const validation = computed(() => validateExpression(props.modelValue));
const isValid = computed(() => validation.value.valid);
const statusText = computed(() => validation.value.message);

const handleInput = (value: string) => {
  emit('update:modelValue', value);
  const result = validateExpression(value);
  emit('validate', result.valid, result.message);
};

const insertVariable = (name: string) => {
  const nextValue = `${props.modelValue}${props.modelValue ? ' ' : ''}${name}`;
  handleInput(nextValue);
};
</script>

<style scoped>
.expr-editor {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.expr-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.expr-status {
  font-size: 12px;
  color: #52c41a;
}

.expr-status.invalid {
  color: #ff4d4f;
}
</style>
