<template>
  <a-space>
    <a-input v-model:value="localName" placeholder="工作台名称" style="width: 220px" />
    <a-select v-model:value="localTheme" style="width: 140px" :options="themeOptions" />
    <a-button type="primary" size="small" :loading="saving" @click="submit">保存</a-button>
  </a-space>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";

const props = defineProps<{
  name: string;
  theme: string;
}>();

const emit = defineEmits<{
  (event: "save", payload: { name: string; theme: string }): void;
}>();

const localName = ref(props.name);
const localTheme = ref(props.theme);
const saving = ref(false);

const themeOptions = [
  { label: "浅色", value: "light" },
  { label: "深色", value: "dark" }
];

watch(
  () => props.name,
  (value) => {
    localName.value = value;
  }
);

watch(
  () => props.theme,
  (value) => {
    localTheme.value = value;
  }
);

function submit() {
  saving.value = true;
  emit("save", {
    name: localName.value.trim() || "我的 AI 工作台",
    theme: localTheme.value || "light"
  });
  window.setTimeout(() => {
    saving.value = false;
  }, 200);
}
</script>
