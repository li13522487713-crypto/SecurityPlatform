<template>
  <div class="run-panel">
    <a-form layout="inline">
      <a-form-item label="Input(JSON)">
        <a-input v-model:value="inputJson" style="width: 260px" />
      </a-form-item>
      <a-form-item>
        <a-button type="primary" :loading="running" @click="run">执行</a-button>
      </a-form-item>
      <a-form-item v-if="executionId">
        <a-button danger @click="$emit('cancel', executionId)">取消</a-button>
      </a-form-item>
    </a-form>
    <div class="run-meta" v-if="executionId">
      <span>ExecutionId: {{ executionId }}</span>
      <span>Status: {{ status || "-" }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { message } from "ant-design-vue";

const props = defineProps<{
  running: boolean;
  executionId?: string;
  status?: string;
}>();

const emit = defineEmits<{
  (e: "run", inputs: Record<string, unknown>): void;
  (e: "cancel", executionId: string): void;
}>();

const inputJson = ref("{}");

function run() {
  try {
    const parsed = JSON.parse(inputJson.value || "{}") as Record<string, unknown>;
    emit("run", parsed);
  } catch {
    message.error("Input 必须是合法 JSON");
  }
}
</script>

<style scoped>
.run-panel {
  border-top: 1px solid #f0f0f0;
  padding: 12px;
}

.run-meta {
  margin-top: 8px;
  display: flex;
  gap: 16px;
  color: rgba(0, 0, 0, 0.65);
}
</style>
