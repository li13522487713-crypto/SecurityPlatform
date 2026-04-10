<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="终止计划">
      <a-radio-group v-model:value="terminationMode" @change="emitChange">
        <a-radio value="returnVariables">返回变量</a-radio>
        <a-radio value="answerText">回答内容</a-radio>
      </a-radio-group>
    </a-form-item>

    <a-form-item label="输出变量映射">
      <div v-for="(item, index) in mappings" :key="index" class="mapping-row">
        <a-input v-model:value="item.from" placeholder="源变量" @change="emitChange" />
        <span class="arrow">→</span>
        <a-input v-model:value="item.to" placeholder="输出字段" @change="emitChange" />
        <a-button size="small" @click="removeMapping(index)">-</a-button>
      </div>
      <a-button size="small" type="dashed" @click="addMapping">+ 添加映射</a-button>
    </a-form-item>

    <a-form-item label="模板文本">
      <a-textarea v-model:value="templateText" :rows="4" @change="emitChange" />
    </a-form-item>

    <a-form-item label="流式输出">
      <a-switch v-model:checked="streamOutput" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

interface MappingItem {
  from: string;
  to: string;
}

const props = defineProps<{
  configs: Record<string, unknown>;
}>();

const emit = defineEmits<{
  (e: "change"): void;
}>();

const terminationMode = computed<string>({
  get() {
    if (typeof props.configs.terminationMode !== "string") {
      props.configs.terminationMode = "returnVariables";
    }
    return props.configs.terminationMode as string;
  },
  set(value) {
    props.configs.terminationMode = value;
  }
});

const mappings = computed<MappingItem[]>({
  get() {
    if (!Array.isArray(props.configs.outputMappings)) {
      props.configs.outputMappings = [];
    }
    return props.configs.outputMappings as MappingItem[];
  },
  set(value) {
    props.configs.outputMappings = value;
  }
});

const templateText = computed<string>({
  get() {
    if (typeof props.configs.templateText !== "string") {
      props.configs.templateText = "";
    }
    return props.configs.templateText as string;
  },
  set(value) {
    props.configs.templateText = value;
  }
});

const streamOutput = computed<boolean>({
  get() {
    if (typeof props.configs.streamOutput !== "boolean") {
      props.configs.streamOutput = false;
    }
    return props.configs.streamOutput as boolean;
  },
  set(value) {
    props.configs.streamOutput = value;
  }
});

function addMapping() {
  mappings.value.push({ from: "", to: "" });
  emit("change");
}

function removeMapping(index: number) {
  mappings.value.splice(index, 1);
  emit("change");
}

function emitChange() {
  emit("change");
}
</script>

<style scoped>
.mapping-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 16px minmax(0, 1fr) 32px;
  gap: 8px;
  margin-bottom: 8px;
  align-items: center;
}

.arrow {
  color: #8f99a6;
  text-align: center;
}
</style>
