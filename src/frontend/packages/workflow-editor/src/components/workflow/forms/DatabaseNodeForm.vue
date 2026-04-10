<template>
  <a-form layout="vertical" size="small">
    <a-form-item :label="t('wfUi.forms.database.databaseId')">
      <a-input v-model:value="databaseId" placeholder="db_xxx" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.database.tableName')">
      <a-input v-model:value="tableName" placeholder="table_name" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.database.mode')">
      <a-tag color="blue">{{ mode }}</a-tag>
    </a-form-item>
    <a-form-item v-if="mode === 'query'" :label="t('wfUi.forms.database.whereJson')">
      <a-textarea v-model:value="whereJson" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'insert'" :label="t('wfUi.forms.database.payloadJson')">
      <a-textarea v-model:value="payloadJson" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'update'" :label="t('wfUi.forms.database.updateWhereJson')">
      <a-textarea v-model:value="whereJson" :rows="3" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'update'" :label="t('wfUi.forms.database.updatePayloadJson')">
      <a-textarea v-model:value="payloadJson" :rows="3" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'delete'" :label="t('wfUi.forms.database.deleteWhereJson')">
      <a-textarea v-model:value="whereJson" :rows="4" @change="emitChange" />
    </a-form-item>
    <a-form-item v-if="mode === 'customSql'" label="SQL">
      <a-textarea v-model:value="sql" :rows="5" @change="emitChange" />
    </a-form-item>
    <a-form-item :label="t('wfUi.forms.database.outputKey')">
      <a-input v-model:value="outputKey" placeholder="db_output" @change="emitChange" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{
  configs: Record<string, unknown>;
  mode: "query" | "insert" | "update" | "delete" | "customSql";
}>();
const emit = defineEmits<{ (e: "change"): void }>();
const { t } = useI18n();

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
