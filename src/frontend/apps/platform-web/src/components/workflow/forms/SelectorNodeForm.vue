<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="分支命中策略">
      <a-radio-group v-model:value="matchMode" @change="emitChange">
        <a-radio value="all">全部满足 (AND)</a-radio>
        <a-radio value="any">任一满足 (OR)</a-radio>
      </a-radio-group>
    </a-form-item>

    <a-form-item label="条件构建器">
      <div v-for="(item, index) in conditions" :key="index" class="condition-row">
        <a-input v-model:value="item.field" placeholder="变量路径，例如 input.score" @change="emitChange" />
        <a-select v-model:value="item.operator" style="width: 120px" @change="emitChange">
          <a-select-option value="eq">=</a-select-option>
          <a-select-option value="ne">!=</a-select-option>
          <a-select-option value="gt">></a-select-option>
          <a-select-option value="gte">>=</a-select-option>
          <a-select-option value="lt"><</a-select-option>
          <a-select-option value="lte"><=</a-select-option>
          <a-select-option value="contains">contains</a-select-option>
        </a-select>
        <a-input v-model:value="item.value" placeholder="比较值" @change="emitChange" />
        <a-button size="small" @click="removeCondition(index)">-</a-button>
      </div>
      <a-button size="small" type="dashed" @click="addCondition">+ 添加条件</a-button>
    </a-form-item>

    <a-form-item label="兜底分支表达式（可选）">
      <a-textarea v-model:value="fallbackExpression" :rows="3" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

interface SelectorCondition {
  field: string;
  operator: string;
  value: string;
}

const props = defineProps<{
  configs: Record<string, unknown>;
}>();

const emit = defineEmits<{
  (e: "change"): void;
}>();

const matchMode = computed<string>({
  get() {
    if (typeof props.configs.matchMode !== "string") {
      props.configs.matchMode = "all";
    }
    return props.configs.matchMode as string;
  },
  set(value) {
    props.configs.matchMode = value;
  }
});

const conditions = computed<SelectorCondition[]>({
  get() {
    if (!Array.isArray(props.configs.conditions)) {
      props.configs.conditions = [];
    }
    return props.configs.conditions as SelectorCondition[];
  },
  set(value) {
    props.configs.conditions = value;
  }
});

const fallbackExpression = computed<string>({
  get() {
    if (typeof props.configs.fallbackExpression !== "string") {
      props.configs.fallbackExpression = "";
    }
    return props.configs.fallbackExpression as string;
  },
  set(value) {
    props.configs.fallbackExpression = value;
  }
});

function addCondition() {
  conditions.value.push({ field: "", operator: "eq", value: "" });
  emit("change");
}

function removeCondition(index: number) {
  conditions.value.splice(index, 1);
  emit("change");
}

function emitChange() {
  emit("change");
}
</script>

<style scoped>
.condition-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 120px minmax(0, 1fr) 32px;
  gap: 8px;
  margin-bottom: 8px;
}
</style>
