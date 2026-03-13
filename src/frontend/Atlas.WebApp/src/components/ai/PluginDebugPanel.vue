<template>
  <a-form layout="vertical">
    <a-form-item label="目标接口">
      <a-select
        :value="apiId"
        allow-clear
        placeholder="不选择则按插件级调试"
        :options="apiOptions"
        @update:value="onApiIdChange"
      />
    </a-form-item>
    <a-form-item label="输入 JSON">
      <a-textarea
        :value="inputJson"
        :rows="5"
        @update:value="onInputJsonChange"
      />
    </a-form-item>
    <a-form-item v-if="outputJson">
      <a-alert :message="resultTitle" type="info" />
      <pre class="json-block">{{ outputJson }}</pre>
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
const props = defineProps<{
  apiOptions: Array<{ label: string; value: number }>;
  apiId?: number;
  inputJson: string;
  outputJson: string;
  resultTitle: string;
}>();

const emit = defineEmits<{
  (event: "update:apiId", value: number | undefined): void;
  (event: "update:inputJson", value: string): void;
}>();

function onApiIdChange(value: number | undefined) {
  emit("update:apiId", value);
}

function onInputJsonChange(value: string) {
  emit("update:inputJson", value);
}
</script>

<style scoped>
.json-block {
  margin-top: 8px;
  margin-bottom: 0;
  padding: 12px;
  border-radius: 8px;
  background: #fafafa;
  max-height: 260px;
  overflow: auto;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
