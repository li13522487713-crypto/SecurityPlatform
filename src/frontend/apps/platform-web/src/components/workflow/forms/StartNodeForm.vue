<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="默认变量">
      <div v-for="(item, index) in variables" :key="index" class="variable-row">
        <a-input v-model:value="item.name" placeholder="变量名" @change="emitChange" />
        <a-select v-model:value="item.type" style="width: 110px" @change="emitChange">
          <a-select-option value="string">string</a-select-option>
          <a-select-option value="number">number</a-select-option>
          <a-select-option value="boolean">boolean</a-select-option>
          <a-select-option value="json">json</a-select-option>
          <a-select-option value="array">array</a-select-option>
          <a-select-option value="object">object</a-select-option>
        </a-select>
        <a-input v-model:value="item.defaultValue" placeholder="默认值" @change="emitChange" />
        <a-button size="small" @click="removeVariable(index)">-</a-button>
      </div>
      <a-button size="small" type="dashed" @click="addVariable">+ 添加变量</a-button>
    </a-form-item>

    <a-form-item label="自动保存历史">
      <a-switch v-model:checked="autoSaveHistory" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

interface StartVariableItem {
  name: string;
  type: string;
  defaultValue: string;
}

const props = defineProps<{
  configs: Record<string, unknown>;
}>();

const emit = defineEmits<{
  (e: "change"): void;
}>();

const variables = computed<StartVariableItem[]>({
  get() {
    if (!Array.isArray(props.configs.variables)) {
      props.configs.variables = [];
    }
    return props.configs.variables as StartVariableItem[];
  },
  set(value) {
    props.configs.variables = value;
  }
});

const autoSaveHistory = computed<boolean>({
  get() {
    if (typeof props.configs.autoSaveHistory !== "boolean") {
      props.configs.autoSaveHistory = true;
    }
    return props.configs.autoSaveHistory as boolean;
  },
  set(value) {
    props.configs.autoSaveHistory = value;
  }
});

function addVariable() {
  variables.value.push({ name: "", type: "string", defaultValue: "" });
  emit("change");
}

function removeVariable(index: number) {
  variables.value.splice(index, 1);
  emit("change");
}

function emitChange() {
  emit("change");
}
</script>

<style scoped>
.variable-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 110px minmax(0, 1fr) 32px;
  gap: 8px;
  margin-bottom: 8px;
}
</style>
