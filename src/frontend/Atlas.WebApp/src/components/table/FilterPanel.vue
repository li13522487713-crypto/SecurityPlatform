<script setup lang="ts">
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import type { TableViewQueryGroup } from "@/types/api";

interface FilterField {
  key: string;
  label: string;
  type: 'string' | 'number' | 'date' | 'select' | 'boolean';
  options?: Array<{ label: string; value: string | number }>;
}

interface Props {
  fields: FilterField[];
  queryModel?: TableViewQueryGroup;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "update:queryModel", model: TableViewQueryGroup): void;
  (e: "search", model: TableViewQueryGroup): void;
  (e: "reset"): void;
}>();

const { t } = useI18n();

const visible = ref(false);

const handleSearch = () => {
  if (props.queryModel) {
    emit("search", props.queryModel);
  }
  visible.value = false;
};

const handleReset = () => {
  emit("reset");
};

</script>

<template>
  <div class="filter-panel-wrapper">
    <a-button @click="visible = !visible">高级筛选</a-button>
    <a-drawer
      v-model:open="visible"
      title="高级筛选构建器"
      placement="right"
      width="480"
    >
      <div class="filter-builder-content">
        <a-empty description="高级筛选规则构建器 (迭代中)" />
        <!-- P0 阶段，预留占位 UI 进行后续丰富的逻辑与多级嵌套 -->
      </div>
      <template #footer>
        <a-space>
          <a-button @click="handleReset">重置</a-button>
          <a-button type="primary" @click="handleSearch">搜索</a-button>
        </a-space>
      </template>
    </a-drawer>
  </div>
</template>

<style scoped>
.filter-panel-wrapper {
  display: inline-block;
}
</style>
