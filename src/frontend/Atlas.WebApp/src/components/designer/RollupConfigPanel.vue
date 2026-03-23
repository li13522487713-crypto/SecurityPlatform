<template>
  <div class="rollup-config-panel">
    <div
      v-for="(def, idx) in defs"
      :key="idx"
      class="rollup-row"
    >
      <a-row :gutter="8" align="middle">
        <!-- 聚合函数 -->
        <a-col :span="5">
          <a-select
            v-model:value="def.aggregateFunction"
            size="small"
            style="width: 100%"
          >
            <a-select-option value="SUM">SUM</a-select-option>
            <a-select-option value="COUNT">COUNT</a-select-option>
            <a-select-option value="MIN">MIN</a-select-option>
            <a-select-option value="MAX">MAX</a-select-option>
            <a-select-option value="AVG">AVG</a-select-option>
          </a-select>
        </a-col>

        <!-- 子表字段 -->
        <a-col :span="6">
          <a-input
            v-model:value="def.childField"
            size="small"
            :placeholder="t('rollupConfig.childField')"
          />
        </a-col>

        <!-- → 目标字段 -->
        <a-col :span="1" style="text-align: center; font-size: 11px; color: #888">→</a-col>

        <!-- 目标字段 -->
        <a-col :span="6">
          <a-input
            v-model:value="def.targetField"
            size="small"
            :placeholder="t('rollupConfig.targetField')"
          />
        </a-col>

        <!-- 过滤表达式 -->
        <a-col :span="5">
          <a-input
            v-model:value="def.filterExpression"
            size="small"
            :placeholder="t('rollupConfig.filterExpression')"
          />
        </a-col>

        <!-- 删除按钮 -->
        <a-col :span="1">
          <a-button
            type="text"
            danger
            size="small"
            @click="removeDef(idx)"
          >
            <template #icon><MinusCircleOutlined /></template>
          </a-button>
        </a-col>
      </a-row>
    </div>

    <a-button
      type="dashed"
      block
      size="small"
      style="margin-top: 8px"
      @click="addDef"
    >
      <template #icon><PlusOutlined /></template>
      {{ t("rollupConfig.addRule") }}
    </a-button>

    <div v-if="defs.length > 0" style="margin-top: 8px; font-size: 11px; color: #aaa">
      {{ t("rollupConfig.hint", { child: targetTableKey, master: sourceTableKey }) }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { PlusOutlined, MinusCircleOutlined } from "@ant-design/icons-vue";

const props = defineProps<{
  value: string;
  sourceTableKey: string;
  targetTableKey: string;
}>();

const emit = defineEmits<{
  (e: "update:value", v: string): void;
}>();

interface RollupDef {
  targetField: string;
  childTableKey: string;
  childField: string;
  aggregateFunction: string;
  filterExpression: string;
}

const { t } = useI18n();

const defs = ref<RollupDef[]>([]);

function parseValue(json: string) {
  try {
    const parsed = JSON.parse(json);
    if (Array.isArray(parsed)) {
      return parsed as RollupDef[];
    }
  } catch {
    // ignore
  }
  return [];
}

watch(
  () => props.value,
  (v) => {
    defs.value = parseValue(v);
  },
  { immediate: true }
);

watch(
  defs,
  (newDefs) => {
    emit("update:value", JSON.stringify(newDefs));
  },
  { deep: true }
);

function addDef() {
  defs.value = [
    ...defs.value,
    {
      targetField: "",
      childTableKey: props.targetTableKey,
      childField: "",
      aggregateFunction: "SUM",
      filterExpression: ""
    }
  ];
}

function removeDef(idx: number) {
  defs.value = defs.value.filter((_, i) => i !== idx);
}
</script>

<style scoped>
.rollup-config-panel {
  padding: 4px 0;
}

.rollup-row {
  margin-bottom: 8px;
  padding: 8px;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 4px;
}
</style>
