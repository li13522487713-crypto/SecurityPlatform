<template>
  <div class="relation-designer">
    <div class="header">
      <a-space>
        <a-select
          v-model:value="selectedTableKey"
          style="width: 280px"
          :options="tableOptions"
          :placeholder="t('relationDesigner.selectTable')"
          show-search
          :filter-option="false"
          @search="onSearch"
        />
        <a-button @click="refreshTables">{{ t("common.refresh") }}</a-button>
      </a-space>
      <a-button type="primary" :loading="saving" :disabled="!selectedTableKey" @click="saveRelations">
        {{ t("relationDesigner.save") }}
      </a-button>
    </div>

    <a-row :gutter="16">
      <a-col :span="16">
        <a-card :title="t('relationDesigner.relationList')" size="small">
          <a-table :data-source="relations" :columns="columns" row-key="id" size="small" :pagination="false" :loading="loadingRelations">
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'actions'">
                <a-button type="link" danger size="small" @click="removeRelation(record.id)">{{ t("common.delete") }}</a-button>
              </template>
            </template>
          </a-table>
          <a-empty v-if="relations.length === 0 && !loadingRelations" :description="t('common.noData')" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card :title="t('relationDesigner.createRelation')" size="small">
          <a-form layout="vertical">
            <a-form-item :label="t('relationDesigner.targetTable')">
              <a-select
                v-model:value="form.relatedTableKey"
                :options="tableOptions"
                :placeholder="t('relationDesigner.targetTable')"
                show-search
                :filter-option="false"
                @search="onSearch"
              />
            </a-form-item>
            <a-form-item :label="t('relationDesigner.sourceField')">
              <a-input v-model:value="form.sourceField" />
            </a-form-item>
            <a-form-item :label="t('relationDesigner.targetField')">
              <a-input v-model:value="form.targetField" />
            </a-form-item>
            <a-form-item :label="t('relationDesigner.relationType')">
              <a-select v-model:value="form.relationType" :options="relationTypeOptions" />
            </a-form-item>
            <a-button type="primary" block @click="addRelation">
              {{ t("common.create") }}
            </a-button>
          </a-form>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import {
  getAppScopedDynamicTables,
  getDynamicTableRelations,
  setDynamicTableRelations,
  type AppScopedDynamicTableListItem,
} from "@/services/api-dynamic-tables";
import type { DynamicRelationDefinition } from "@/types/dynamic-tables";
import { getCurrentAppIdFromStorage } from "@/utils/app-context";

const props = defineProps<{
  appId?: string;
  initialViewId?: string;
}>();

const { t } = useI18n();
const loadingTables = ref(false);
const loadingRelations = ref(false);
const saving = ref(false);
const keyword = ref("");
const tables = ref<AppScopedDynamicTableListItem[]>([]);
const relations = ref<Array<DynamicRelationDefinition & { id: string }>>([]);
const selectedTableKey = ref("");
const rowIdSeed = ref(1);

const form = reactive({
  relatedTableKey: "",
  sourceField: "",
  targetField: "",
  relationType: "OneToMany",
});

const tableOptions = computed(() =>
  tables.value.map((table) => ({
    label: `${table.displayName} (${table.tableKey})`,
    value: table.tableKey,
  }))
);

const relationTypeOptions = computed(() => [
  { label: "OneToOne", value: "OneToOne" },
  { label: "OneToMany", value: "OneToMany" },
  { label: "ManyToMany", value: "ManyToMany" },
]);

const columns = computed(() => [
  { title: t("relationDesigner.sourceField"), dataIndex: "sourceField", key: "sourceField" },
  { title: t("relationDesigner.targetTable"), dataIndex: "relatedTableKey", key: "relatedTableKey" },
  { title: t("relationDesigner.targetField"), dataIndex: "targetField", key: "targetField" },
  { title: t("relationDesigner.relationType"), dataIndex: "relationType", key: "relationType" },
  { title: t("common.actions"), key: "actions", width: 90 },
]);

const resolveAppId = () => props.appId?.trim() || getCurrentAppIdFromStorage() || "";

const refreshTables = async () => {
  const appId = resolveAppId();
  if (!appId) {
    return;
  }
  loadingTables.value = true;
  try {
    tables.value = await getAppScopedDynamicTables(appId, keyword.value.trim());
    if (!selectedTableKey.value && tables.value.length > 0) {
      selectedTableKey.value = tables.value[0].tableKey;
    }
  } catch (error) {
    message.error((error as Error).message || t("dynamic.loadTablesFailed"));
  } finally {
    loadingTables.value = false;
  }
};

const loadRelations = async () => {
  if (!selectedTableKey.value) {
    relations.value = [];
    return;
  }
  loadingRelations.value = true;
  try {
    const result = await getDynamicTableRelations(selectedTableKey.value);
    relations.value = result.map((item) => ({
      ...item,
      id: item.id || `tmp-${rowIdSeed.value++}`,
    }));
  } catch (error) {
    message.error((error as Error).message || t("relationDesigner.loadFailed"));
  } finally {
    loadingRelations.value = false;
  }
};

const onSearch = (value: string) => {
  keyword.value = value;
};

const addRelation = () => {
  if (!form.relatedTableKey || !form.sourceField.trim() || !form.targetField.trim()) {
    message.warning(t("validation.required"));
    return;
  }
  relations.value = [
    ...relations.value,
    {
      id: `tmp-${rowIdSeed.value++}`,
      relatedTableKey: form.relatedTableKey,
      sourceField: form.sourceField.trim(),
      targetField: form.targetField.trim(),
      relationType: form.relationType,
    },
  ];
  form.relatedTableKey = "";
  form.sourceField = "";
  form.targetField = "";
  form.relationType = "OneToMany";
};

const removeRelation = (id: string) => {
  relations.value = relations.value.filter((item) => item.id !== id);
};

const saveRelations = async () => {
  if (!selectedTableKey.value) {
    return;
  }
  saving.value = true;
  try {
    await setDynamicTableRelations(selectedTableKey.value, {
      relations: relations.value.map(({ id, ...rest }) => ({
        ...rest,
        id: id.startsWith("tmp-") ? null : id,
      })),
    });
    message.success(t("common.save"));
    await loadRelations();
  } catch (error) {
    message.error((error as Error).message || t("relationDesigner.saveFailed"));
  } finally {
    saving.value = false;
  }
};

watch(selectedTableKey, () => {
  void loadRelations();
});

watch(keyword, () => {
  void refreshTables();
});

onMounted(async () => {
  await refreshTables();
  await loadRelations();
});
</script>

<style scoped>
.relation-designer {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
</style>
