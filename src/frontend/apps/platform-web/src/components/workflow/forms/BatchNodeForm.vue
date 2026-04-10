<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="批处理输入路径">
      <a-input v-model:value="collectionPath" placeholder="例如: input.records" @change="emitChange" />
    </a-form-item>

    <a-form-item label="并发度">
      <a-input-number v-model:value="parallelism" :min="1" :max="64" style="width: 100%" @change="emitChange" />
    </a-form-item>

    <a-form-item label="单项超时（毫秒）">
      <a-input-number v-model:value="itemTimeoutMs" :min="0" :step="100" style="width: 100%" @change="emitChange" />
    </a-form-item>

    <a-form-item label="失败策略">
      <a-select v-model:value="onError" @change="emitChange">
        <a-select-option value="continue">继续处理</a-select-option>
        <a-select-option value="fail-fast">立即失败</a-select-option>
      </a-select>
    </a-form-item>

    <a-form-item label="结果变量名">
      <a-input v-model:value="outputKey" placeholder="batch_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  configs: Record<string, unknown>;
}>();

const emit = defineEmits<{
  (e: "change"): void;
}>();

const collectionPath = computed<string>({
  get() {
    if (typeof props.configs.collectionPath !== "string") {
      props.configs.collectionPath = "";
    }
    return props.configs.collectionPath as string;
  },
  set(value) {
    props.configs.collectionPath = value;
  }
});

const parallelism = computed<number>({
  get() {
    if (typeof props.configs.parallelism !== "number") {
      props.configs.parallelism = 4;
    }
    return props.configs.parallelism as number;
  },
  set(value) {
    props.configs.parallelism = value;
  }
});

const itemTimeoutMs = computed<number>({
  get() {
    if (typeof props.configs.itemTimeoutMs !== "number") {
      props.configs.itemTimeoutMs = 0;
    }
    return props.configs.itemTimeoutMs as number;
  },
  set(value) {
    props.configs.itemTimeoutMs = value;
  }
});

const onError = computed<string>({
  get() {
    if (typeof props.configs.onError !== "string") {
      props.configs.onError = "continue";
    }
    return props.configs.onError as string;
  },
  set(value) {
    props.configs.onError = value;
  }
});

const outputKey = computed<string>({
  get() {
    if (typeof props.configs.outputKey !== "string") {
      props.configs.outputKey = "batch_output";
    }
    return props.configs.outputKey as string;
  },
  set(value) {
    props.configs.outputKey = value;
  }
});

function emitChange() {
  emit("change");
}
</script>
