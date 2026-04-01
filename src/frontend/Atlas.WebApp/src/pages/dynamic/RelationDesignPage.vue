<template>
  <div class="relation-design-page">
    <!-- 顶部页头 -->
    <div class="page-header">
      <div class="header-left">
        <a-button type="text" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
        </a-button>
        <span class="page-title">{{ pageTitle }}</span>
        <a-tag color="blue">{{ tableKey }}</a-tag>
      </div>
      <div class="header-actions">
        <a-button @click="openErd">
          <template #icon><PartitionOutlined /></template>
          {{ t("erd.modeErd") }}
        </a-button>
        <a-button type="primary" @click="openCreateDrawer">
          <template #icon><PlusOutlined /></template>
          {{ t("relation.addRelation") }}
        </a-button>
      </div>
    </div>

    <!-- 关系列表 -->
    <div class="page-body">
      <a-spin :spinning="loading">
        <a-empty v-if="!loading && relations.length === 0" :description="t('relation.noRelations')" style="margin-top: 80px">
          <a-button type="primary" @click="openCreateDrawer">{{ t("relation.addRelation") }}</a-button>
        </a-empty>
        <a-table
          v-else
          :dataSource="relations"
          :columns="columns"
          row-key="_localId"
          :pagination="false"
          size="middle"
          class="relation-table"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'relationType'">
              <a-tag :color="relationTypeColor(record.relationType)">{{ record.relationType }}</a-tag>
            </template>
            <template v-else-if="column.key === 'sourceField'">
              <code>{{ record.sourceField }}</code>
            </template>
            <template v-else-if="column.key === 'targetTable'">
              <a-space>
                <code>{{ record.targetTableKey }}</code>
                <span v-if="record.displayName" style="color: #8c8c8c; font-size: 12px">{{ record.displayName }}</span>
              </a-space>
            </template>
            <template v-else-if="column.key === 'cascadeRule'">
              <a-tag>{{ record.cascadeRule || 'None' }}</a-tag>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-space>
                <a-button type="link" size="small" @click="openEditDrawer(record)">{{ t("common.edit") }}</a-button>
                <a-popconfirm :title="t('relation.confirmDelete')" @confirm="handleDelete(record._localId)">
                  <a-button type="link" danger size="small">{{ t("common.delete") }}</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-spin>
    </div>

    <!-- 新建/编辑关系抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      :title="editingRelation ? t('relation.editRelation') : t('relation.addRelation')"
      placement="right"
      width="500"
      :confirm-loading="saving"
      @close="resetDrawer"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="formRules">
        <a-form-item :label="t('relation.relationType')" name="relationType">
          <a-radio-group v-model:value="form.relationType" button-style="solid">
            <a-radio-button value="OneToOne">1:1</a-radio-button>
            <a-radio-button value="OneToMany">1:N</a-radio-button>
          </a-radio-group>
        </a-form-item>
        <a-form-item :label="t('relation.sourceField')" name="sourceField">
          <a-select
            v-model:value="form.sourceField"
            :options="sourceFieldOptions"
            :placeholder="t('relation.selectField')"
          />
        </a-form-item>
        <a-form-item :label="t('relation.targetTable')" name="targetTableKey">
          <a-select
            v-model:value="form.targetTableKey"
            :options="tableOptions"
            :placeholder="t('relation.selectTargetTable')"
            show-search
            :filter-option="filterTableOption"
            @change="onTargetTableChange"
          />
        </a-form-item>
        <a-form-item :label="t('relation.targetField')" name="targetField">
          <a-select
            v-model:value="form.targetField"
            :options="targetFieldOptions"
            :placeholder="t('relation.selectField')"
            :disabled="!form.targetTableKey"
          />
        </a-form-item>
        <a-form-item :label="t('relation.cascadeRule')" name="cascadeRule">
          <a-select v-model:value="form.cascadeRule" :options="cascadeOptions" allow-clear />
        </a-form-item>
        <a-form-item :label="t('relation.displayName')" name="displayName">
          <a-input v-model:value="form.displayName" :placeholder="t('relation.displayNamePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('relation.description')" name="description">
          <a-textarea v-model:value="form.description" :rows="2" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="resetDrawer">{{ t("common.cancel") }}</a-button>
          <a-button type="primary" :loading="saving" @click="handleSave">{{ t("common.save") }}</a-button>
        </a-space>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  PlusOutlined,
  PartitionOutlined
} from "@ant-design/icons-vue";
import type { Rule } from "ant-design-vue/es/form";
import type { DynamicRelationType } from "@/types/dynamic-tables";
import {
  getDynamicTableFields,
  getDynamicTableRelations,
  setDynamicTableRelations,
  getAllDynamicTables
} from "@/services/dynamic-tables";

interface RelationRow {
  _localId: string;
  relationType: DynamicRelationType;
  sourceField: string;
  targetTableKey: string;
  targetField: string;
  cascadeRule?: string | null;
  displayName?: string | null;
  description?: string | null;
}

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.params.tableKey === "string" ? decodeURIComponent(route.params.tableKey) : ""));

const loading = ref(false);
const saving = ref(false);
const drawerOpen = ref(false);
const formRef = ref();
const relations = ref<RelationRow[]>([]);
const sourceFieldOptions = ref<{ label: string; value: string }[]>([]);
const tableOptions = ref<{ label: string; value: string }[]>([]);
const targetFieldOptions = ref<{ label: string; value: string }[]>([]);
const editingRelation = ref<RelationRow | null>(null);

const pageTitle = computed(() =>
  tableKey.value ? `${tableKey.value} - ${t("relation.pageTitle")}` : t("relation.pageTitle")
);

const form = reactive({
  relationType: "OneToMany" as DynamicRelationType,
  sourceField: "",
  targetTableKey: "",
  targetField: "",
  cascadeRule: "",
  displayName: "",
  description: ""
});

const cascadeOptions = [
  { label: "None", value: "None" },
  { label: "SetNull", value: "SetNull" },
  { label: "Cascade", value: "Cascade" },
  { label: "Restrict", value: "Restrict" }
];

const formRules: Record<string, Rule[]> = {
  relationType: [{ required: true }],
  sourceField: [{ required: true, message: t("validation.required") }],
  targetTableKey: [{ required: true, message: t("validation.required") }],
  targetField: [{ required: true, message: t("validation.required") }]
};

const columns = computed(() => [
  { title: t("relation.relationType"), key: "relationType", width: 100 },
  { title: t("relation.sourceField"), key: "sourceField", dataIndex: "sourceField" },
  { title: t("relation.targetTable"), key: "targetTable" },
  { title: t("relation.targetField"), dataIndex: "targetField", key: "targetField" },
  { title: t("relation.cascadeRule"), key: "cascadeRule", width: 120 },
  { title: t("common.actions"), key: "actions", width: 140 }
]);

const relationTypeColor = (type: string): string => {
  const map: Record<string, string> = { OneToOne: "purple", OneToMany: "blue", ManyToMany: "orange" };
  return map[type] ?? "default";
};

const filterTableOption = (input: string, option: { label: string; value: string }): boolean =>
  option.label.toLowerCase().includes(input.toLowerCase());

const loadRelations = async () => {
  if (!tableKey.value) return;
  loading.value = true;
  try {
    const result = await getDynamicTableRelations(tableKey.value);
    relations.value = result.map((r, i) => ({
      _localId: String(i),
      relationType: (r.multiplicity ?? r.relationType) as DynamicRelationType,
      sourceField: r.sourceField,
      targetTableKey: r.relatedTableKey,
      targetField: r.targetField,
      cascadeRule: r.cascadeRule ?? null,
      displayName: r.displayName ?? null,
      description: r.description ?? null
    }));
  } catch (error) {
    message.error((error as Error).message || t("relation.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const loadSourceFields = async () => {
  if (!tableKey.value) return;
  try {
    const fields = await getDynamicTableFields(tableKey.value);
    sourceFieldOptions.value = fields.map((f) => ({ label: `${f.name} (${f.fieldType})`, value: f.name }));
  } catch {
    // ignore
  }
};

const loadAllTables = async () => {
  try {
    const tables = await getAllDynamicTables();
    tableOptions.value = tables
      .filter((t) => t.tableKey !== tableKey.value)
      .map((t) => ({ label: `${t.displayName} (${t.tableKey})`, value: t.tableKey }));
  } catch {
    // ignore
  }
};

const onTargetTableChange = async (targetKey: string) => {
  targetFieldOptions.value = [];
  form.targetField = "";
  if (!targetKey) return;
  try {
    const fields = await getDynamicTableFields(targetKey);
    targetFieldOptions.value = fields.map((f) => ({ label: `${f.name} (${f.fieldType})`, value: f.name }));
  } catch {
    // ignore
  }
};

const openCreateDrawer = () => {
  editingRelation.value = null;
  Object.assign(form, {
    relationType: "OneToMany",
    sourceField: "",
    targetTableKey: "",
    targetField: "",
    cascadeRule: "",
    displayName: "",
    description: ""
  });
  drawerOpen.value = true;
};

const openEditDrawer = async (record: RelationRow) => {
  editingRelation.value = record;
  Object.assign(form, {
    relationType: record.relationType,
    sourceField: record.sourceField,
    targetTableKey: record.targetTableKey,
    targetField: record.targetField,
    cascadeRule: record.cascadeRule ?? "",
    displayName: record.displayName ?? "",
    description: record.description ?? ""
  });
  await onTargetTableChange(record.targetTableKey);
  drawerOpen.value = true;
};

const resetDrawer = () => {
  drawerOpen.value = false;
  editingRelation.value = null;
};

const buildPayload = (): import("@/types/dynamic-tables").DynamicRelationDefinition[] => {
  return relations.value.map((r) => ({
    relatedTableKey: r.targetTableKey,
    sourceField: r.sourceField,
    targetField: r.targetField,
    relationType: r.relationType,
    multiplicity: r.relationType as "OneToOne" | "OneToMany" | "ManyToMany",
    cascadeRule: r.cascadeRule ?? null,
    displayName: r.displayName ?? null,
    description: r.description ?? null
  }));
};

const handleSave = async () => {
  if (!tableKey.value) return;
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }
  saving.value = true;
  try {
    const newEntry: RelationRow = {
      _localId: String(Date.now()),
      relationType: form.relationType,
      sourceField: form.sourceField,
      targetTableKey: form.targetTableKey,
      targetField: form.targetField,
      cascadeRule: form.cascadeRule || null,
      displayName: form.displayName.trim() || null,
      description: form.description.trim() || null
    };
    if (editingRelation.value) {
      const idx = relations.value.findIndex((r) => r._localId === editingRelation.value!._localId);
      if (idx >= 0) relations.value[idx] = newEntry;
    } else {
      relations.value.push(newEntry);
    }
    await setDynamicTableRelations(tableKey.value, { relations: buildPayload() });
    message.success(editingRelation.value ? t("relation.updateSuccess") : t("relation.createSuccess"));
    resetDrawer();
    await loadRelations();
  } catch (error) {
    message.error((error as Error).message || t("relation.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const handleDelete = async (localId: string) => {
  if (!tableKey.value) return;
  try {
    relations.value = relations.value.filter((r) => r._localId !== localId);
    await setDynamicTableRelations(tableKey.value, { relations: buildPayload() });
    message.success(t("relation.deleteSuccess"));
  } catch (error) {
    message.error((error as Error).message || t("relation.deleteFailed"));
    await loadRelations();
  }
};

const goBack = () => {
  if (appId.value) {
    void router.push(`/apps/${appId.value}/data`);
  } else {
    void router.back();
  }
};

const openErd = () => {
  if (appId.value) void router.push(`/apps/${appId.value}/data/erd`);
};

onMounted(() => {
  void loadRelations();
  void loadSourceFields();
  void loadAllTables();
});

watch(tableKey, () => {
  void loadRelations();
  void loadSourceFields();
});
</script>

<style scoped>
.relation-design-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 120px);
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0,0,0,.03), 0 1px 6px -1px rgba(0,0,0,.02);
  overflow: hidden;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 24px;
  border-bottom: 1px solid #f0f0f0;
  background: #fff;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.page-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f1f1f;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
}

.relation-table {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}
</style>
