<template>
  <div class="var-picker">
    <a-textarea
      :value="modelValue"
      :rows="rows"
      :placeholder="placeholder"
      @change="onTextChange"
      @keydown="onKeyDown"
    />
    <a-dropdown :open="open" :trigger="['click']" placement="bottomLeft">
      <a-button size="small" class="insert-btn" @click="toggleOpen">{{ open ? "关闭变量" : "插入变量" }}</a-button>
      <template #overlay>
        <a-card size="small" class="var-card">
          <a-input v-model:value="keyword" size="small" placeholder="搜索变量..." />
          <div class="var-list">
            <a-button
              v-for="item in filteredVariables"
              :key="item.value"
              type="text"
              size="small"
              class="var-item"
              @click="insertVar(item.value)"
            >
              {{ item.label }} <span class="var-token">{{ item.value }}</span>
            </a-button>
          </div>
        </a-card>
      </template>
    </a-dropdown>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";

interface VariableOption {
  label: string;
  value: string;
}

const props = withDefaults(defineProps<{
  modelValue: string;
  rows?: number;
  placeholder?: string;
  variables?: VariableOption[];
}>(), {
  rows: 4,
  placeholder: "",
  variables: () => [
    { label: "工作流输入.query", value: "{{input.query}}" },
    { label: "工作流输入.message", value: "{{input.message}}" },
    { label: "上游输出.result", value: "{{upstream.result}}" },
    { label: "系统时间", value: "{{sys.datetime}}" }
  ]
});

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
  (e: "change"): void;
}>();

const open = ref(false);
const keyword = ref("");

const filteredVariables = computed(() =>
  props.variables.filter((item) => item.label.includes(keyword.value) || item.value.includes(keyword.value))
);

function onTextChange(event: Event) {
  const value = (event.target as HTMLTextAreaElement).value;
  emit("update:modelValue", value);
  emit("change");
}

function onKeyDown(event: KeyboardEvent) {
  const target = event.target as HTMLTextAreaElement | null;
  if (!target) {
    return;
  }
  if (event.key === "{" && target.value.endsWith("{")) {
    open.value = true;
  }
  if (event.key === "/") {
    open.value = true;
  }
}

function insertVar(token: string) {
  emit("update:modelValue", `${props.modelValue}${token}`);
  emit("change");
  open.value = false;
}

function toggleOpen() {
  open.value = !open.value;
}
</script>

<style scoped>
.var-picker {
  display: grid;
  gap: 8px;
}

.insert-btn {
  justify-self: flex-start;
}

.var-card {
  width: 340px;
}

.var-list {
  max-height: 220px;
  overflow: auto;
  margin-top: 8px;
  display: grid;
}

.var-item {
  justify-content: space-between;
  text-align: left;
}

.var-token {
  color: #4f46e5;
  margin-left: 8px;
}
</style>
