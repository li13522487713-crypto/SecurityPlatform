<template>
  <a-form layout="vertical" size="small">
    <a-form-item label="数据库连接 ID">
      <a-input v-model:value="databaseId" placeholder="db_xxx" @change="emitChange" />
    </a-form-item>
    <a-form-item label="表名">
      <a-input v-model:value="tableName" placeholder="table_name" @change="emitChange" />
    </a-form-item>
    <a-form-item label="操作类型">
      <a-tag color="blue">{{ mode }}</a-tag>
    </a-form-item>
    <a-form-item v-if="mode === 'query'" label="查询条件 JSON">
      <a-textarea v-model:value="whereJson" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'insert'" label="插入数据 JSON">
      <a-textarea v-model:value="payloadJson" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'update'" label="更新条件 JSON">
      <a-textarea v-model:value="whereJson" :rows="3" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'update'" label="更新数据 JSON">
      <a-textarea v-model:value="payloadJson" :rows="3" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'delete'" label="删除条件 JSON">
      <a-textarea v-model:value="whereJson" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'customSql'" label="SQL">
      <a-textarea v-model:value="sql" :rows="5" @change="emitChange" />
    </a-form-item>
    <a-form-item label="输出变量名">
      <a-input v-model:value="outputKey" placeholder="db_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  configs: Record<string, unknown>;
  mode: "query" | "insert" | "update" | "delete" | "customSql";
}>();
const emit = defineEmits<{ (e: "change"): void }>();

const mode = computed(() => props.mode);
const databaseId = computed<string>({
  get: () => (typeof props.configs.databaseId === "string" ? (props.configs.databaseId as string) : ((props.configs.databaseId = ""), "")),
  set: (value) => (props.configs.databaseId = value)
});
const tableName = computed<string>({
  get: () => (typeof props.configs.tableName === "string" ? (props.configs.tableName as string) : ((props.configs.tableName = ""), "")),
  set: (value) => (props.configs.tableName = value)
});
const whereJson = computed<string>({
  get: () => (typeof props.configs.whereJson === "string" ? (props.configs.whereJson as string) : ((props.configs.whereJson = "{}"), "{}")),
  set: (value) => (props.configs.whereJson = value)
});
const payloadJson = computed<string>({
  get: () => (typeof props.configs.payloadJson === "string" ? (props.configs.payloadJson as string) : ((props.configs.payloadJson = "{}"), "{}")),
  set: (value) => (props.configs.payloadJson = value)
});
const sql = computed<string>({
  get: () => (typeof props.configs.sql === "string" ? (props.configs.sql as string) : ((props.configs.sql = ""), "")),
  set: (value) => (props.configs.sql = value)
});
const outputKey = computed<string>({
  get: () => (typeof props.configs.outputKey === "string" ? (props.configs.outputKey as string) : ((props.configs.outputKey = "db_output"), "db_output")),
  set: (value) => (props.configs.outputKey = value)
});

function emitChange() {
  emit("change");
}
</script>
