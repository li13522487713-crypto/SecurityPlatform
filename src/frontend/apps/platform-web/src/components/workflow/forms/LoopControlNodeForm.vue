<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="控制信号">
      <a-tag :color="signal === 'loop_break' ? 'red' : 'gold'">
        {{ signal === "loop_break" ? "BREAK" : "CONTINUE" }}
      </a-tag>
    </a-form-item>
    <a-form-item label="说明">
      <a-textarea v-model:value="reason" :rows="3" placeholder="可选，记录中断/继续原因" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="loop_control_signal" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  configs: Record<string, unknown>;
  mode: "break" | "continue";
}>();

const emit = defineEmits<{
  (e: "change"): void;
}>();

const signal = computed<string>({
  get() {
    const defaultSignal = props.mode === "break" ? "loop_break" : "loop_continue";
    if (typeof props.configs.signal !== "string") {
      props.configs.signal = defaultSignal;
    }
    return props.configs.signal as string;
  },
  set(value) {
    props.configs.signal = value;
  }
});

const reason = computed<string>({
  get() {
    if (typeof props.configs.reason !== "string") {
      props.configs.reason = "";
    }
    return props.configs.reason as string;
  },
  set(value) {
    props.configs.reason = value;
  }
});

const outputKey = computed<string>({
  get() {
    if (typeof props.configs.outputKey !== "string") {
      props.configs.outputKey = "loop_control_signal";
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
