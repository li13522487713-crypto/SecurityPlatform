<template>
  <div class="design-page">
    <div class="design-header">
      <div class="header-left">
        <a-button @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
          {{ t("dynamicTable.backToList") }}
        </a-button>
        <span class="header-title">{{ tableSummary?.displayName ?? tableKey }} — {{ t("dynamicTable.designPageTitle") }}</span>
        <a-tag v-if="tableSummary" color="blue">{{ tableKey }}</a-tag>
      </div>
      <div class="header-actions">
        <a-button @click="loadFields">
          <template #icon><ReloadOutlined /></template>
        </a-button>
        <a-button @click="openAddField">
          <template #icon><PlusOutlined /></template>
          {{ t("dynamicTable.addField") }}
        </a-button>
        <a-button
          :disabled="!hasChanges"
          @click="previewChanges"
        >
          <template #icon><EyeOutlined /></template>
          {{ t("dynamicTable.previewSql") }}
        </a-button>
        <a-button
          type="primary"
          :disabled="!hasChanges"
          :loading="applying"
          @click="applyChanges"
        >
          {{ t("dynamicTable.applyChanges") }}
        </a-button>
      </div>
    </div>

    <div class="design-body">
      <a-spin :spinning="loading">
        <a-table
          :data-source="fields"
          :columns="fieldColumns"
          size="small"
          :pagination="false"
          row-key="name"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'name'">
              <span class="field-name-cell">
                <KeyOutlined v-if="record.isPrimaryKey" style="color: #faad14; margin-right: 4px" />
                {{ record.name }}
              </span>
            </template>
            <template v-else-if="column.key === 'displayName'">
              <a-input
                v-if="!record.isPrimaryKey && isFieldModified(record.name)"
                v-model:value="record.displayName"
                size="small"
              />
              <span v-else>{{ record.displayName ?? '-' }}</span>
            </template>
            <template v-else-if="column.key === 'allowNull'">
              <a-tag :color="record.allowNull ? 'default' : 'red'">
                {{ record.allowNull ? t("dynamicTable.yes") : t("dynamicTable.no") }}
              </a-tag>
            </template>
            <template v-else-if="column.key === 'isUnique'">
              <a-tag v-if="record.isUnique" color="blue">{{ t("dynamicTable.yes") }}</a-tag>
              <span v-else>-</span>
            </template>
            <template v-else-if="column.key === 'isAutoIncrement'">
              <a-tag v-if="record.isAutoIncrement" color="purple">{{ t("dynamicTable.yes") }}</a-tag>
              <span v-else>-</span>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-popconfirm
                v-if="!record.isPrimaryKey"
                :title="t('dynamicTable.removeFieldConfirm')"
                @confirm="markFieldRemoved(record.name)"
              >
                <a style="color: #ff4d4f">{{ t("dynamicTable.removeField") }}</a>
              </a-popconfirm>
            </template>
            <template v-else-if="column.key === 'sortOrder'">
              {{ index + 1 }}
            </template>
          </template>
        </a-table>

        <div v-if="addedFields.length > 0" class="added-fields-section">
          <div class="section-subtitle">{{ t("dynamicTable.addField") }}</div>
          <a-table
            :data-source="addedFields"
            :columns="addedFieldColumns"
            size="small"
            :pagination="false"
            row-key="name"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.key === 'name'">
                <a-input v-model:value="record.name" size="small" style="width: 160px" />
              </template>
              <template v-else-if="column.key === 'displayName'">
                <a-input v-model:value="record.displayName" size="small" />
              </template>
              <template v-else-if="column.key === 'fieldType'">
                <a-select v-model:value="record.fieldType" size="small" style="width: 110px">
                  <a-select-option v-for="ft in fieldTypes" :key="ft" :value="ft">{{ ft }}</a-select-option>
                </a-select>
              </template>
              <template v-else-if="column.key === 'allowNull'">
                <a-switch v-model:checked="record.allowNull" size="small" />
              </template>
              <template v-else-if="column.key === 'actions'">
                <a style="color: #ff4d4f" @click="addedFields.splice(index, 1)">{{ t("dynamicTable.removeField") }}</a>
              </template>
            </template>
          </a-table>
        </div>

        <div v-if="removedFields.length > 0" class="removed-fields-section">
          <a-alert type="warning" show-icon>
            <template #message>
              {{ t("dynamicTable.removeField") }}: {{ removedFields.join(', ') }}
            </template>
          </a-alert>
        </div>
      </a-spin>
    </div>

    <a-modal
      v-model:open="previewVisible"
      :title="t('dynamicTable.previewTitle')"
      :footer="null"
      width="640px"
    >
      <div v-if="previewData">
        <div class="preview-label">{{ t("dynamicTable.previewSqlScripts") }}</div>
        <pre class="preview-sql">{{ previewData.sqlScripts.join('\n') }}</pre>
        <div v-if="previewData.rollbackHint" style="margin-top: 12px;">
          <div class="preview-label">{{ t("dynamicTable.previewRollback") }}</div>
          <a-alert type="info" :message="previewData.rollbackHint" />
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  ReloadOutlined,
  PlusOutlined,
  EyeOutlined,
  KeyOutlined
} from "@ant-design/icons-vue";
import { useAppContext } from "@/composables/useAppContext";
import {
  getDynamicTableFields,
  getDynamicTableSummary,
  alterDynamicTable,
  alterDynamicTablePreview
} from "@/services/api-dynamic-tables";
import type {
  DynamicFieldDefinition,
  DynamicTableAlterPreviewResponse,
  DynamicTableSummary
} from "@/types/dynamic-tables";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const { appKey } = useAppContext();

const tableKey = computed(() => String(route.params.tableKey ?? ""));
const loading = ref(false);
const applying = ref(false);
const fields = ref<DynamicFieldDefinition[]>([]);
const tableSummary = ref<DynamicTableSummary | null>(null);
const addedFields = reactive<DynamicFieldDefinition[]>([]);
const removedFields = reactive<string[]>([]);
const modifiedFieldNames = reactive<Set<string>>(new Set());
const previewVisible = ref(false);
const previewData = ref<DynamicTableAlterPreviewResponse | null>(null);

const fieldTypes = ["Int", "Long", "Decimal", "String", "Text", "Bool", "DateTime", "Date"];

const hasChanges = computed(() =>
  addedFields.length > 0 || removedFields.length > 0 || modifiedFieldNames.size > 0
);

const isFieldModified = (name: string) => modifiedFieldNames.has(name);

const fieldColumns = computed(() => [
  { title: t("dynamicTable.fieldName"), dataIndex: "name", key: "name", width: 180 },
  { title: t("dynamicTable.fieldDisplayName"), dataIndex: "displayName", key: "displayName", width: 160 },
  { title: t("dynamicTable.fieldType"), dataIndex: "fieldType", key: "fieldType", width: 110 },
  { title: t("dynamicTable.allowNull"), dataIndex: "allowNull", key: "allowNull", width: 90 },
  { title: t("dynamicTable.fieldUnique"), dataIndex: "isUnique", key: "isUnique", width: 80 },
  { title: t("dynamicTable.fieldAutoIncrement"), dataIndex: "isAutoIncrement", key: "isAutoIncrement", width: 80 },
  { title: t("dynamicTable.fieldSortOrder"), key: "sortOrder", width: 70 },
  { title: t("common.actions"), key: "actions", width: 80 }
]);

const addedFieldColumns = computed(() => [
  { title: t("dynamicTable.fieldName"), key: "name", width: 180 },
  { title: t("dynamicTable.fieldDisplayName"), key: "displayName", width: 160 },
  { title: t("dynamicTable.fieldType"), key: "fieldType", width: 120 },
  { title: t("dynamicTable.allowNull"), key: "allowNull", width: 90 },
  { title: t("common.actions"), key: "actions", width: 80 }
]);

const loadFields = async () => {
  if (!tableKey.value) return;
  loading.value = true;
  try {
    fields.value = await getDynamicTableFields(tableKey.value);
    addedFields.length = 0;
    removedFields.length = 0;
    modifiedFieldNames.clear();
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.loadFieldsFailed"));
  } finally {
    loading.value = false;
  }
};

const loadSummary = async () => {
  if (!tableKey.value) return;
  try {
    tableSummary.value = await getDynamicTableSummary(tableKey.value);
  } catch {
    tableSummary.value = null;
  }
};

const goBack = () => {
  void router.push(`/apps/${appKey.value}/data`);
};

const openAddField = () => {
  addedFields.push({
    name: "",
    displayName: "",
    fieldType: "String",
    length: 255,
    allowNull: true,
    isPrimaryKey: false,
    isAutoIncrement: false,
    isUnique: false,
    defaultValue: null,
    sortOrder: fields.value.length + addedFields.length + 1
  });
};

const markFieldRemoved = (name: string) => {
  if (!removedFields.includes(name)) {
    removedFields.push(name);
    fields.value = fields.value.filter((f) => f.name !== name);
  }
};

const buildAlterRequest = () => ({
  addFields: addedFields.filter((f) => f.name.trim()),
  updateFields: Array.from(modifiedFieldNames).map((name) => {
    const field = fields.value.find((f) => f.name === name);
    return {
      name,
      displayName: field?.displayName ?? null,
      allowNull: field?.allowNull ?? null,
      isUnique: field?.isUnique ?? null,
      defaultValue: field?.defaultValue ?? null,
      sortOrder: field?.sortOrder ?? null
    };
  }),
  removeFields: [...removedFields]
});

const previewChanges = async () => {
  if (!tableKey.value) return;
  try {
    previewData.value = await alterDynamicTablePreview(tableKey.value, buildAlterRequest());
    previewVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.alterFailed"));
  }
};

const applyChanges = async () => {
  if (!tableKey.value) return;
  applying.value = true;
  try {
    await alterDynamicTable(tableKey.value, buildAlterRequest());
    message.success(t("dynamicTable.alterSuccess"));
    void loadFields();
    void loadSummary();
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.alterFailed"));
  } finally {
    applying.value = false;
  }
};

onMounted(() => {
  void loadSummary();
  void loadFields();
});

watch(tableKey, () => {
  void loadSummary();
  void loadFields();
});
</script>

<style scoped>
.design-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 120px);
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.03);
  overflow: hidden;
}

.design-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 24px;
  border-bottom: 1px solid #f0f0f0;
  flex-shrink: 0;
  flex-wrap: wrap;
  gap: 8px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f1f1f;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.design-body {
  flex: 1;
  padding: 16px 24px;
  overflow: auto;
}

.field-name-cell {
  font-family: 'Courier New', monospace;
  font-size: 13px;
}

.added-fields-section {
  margin-top: 24px;
}

.section-subtitle {
  font-size: 14px;
  font-weight: 600;
  color: #1f1f1f;
  margin-bottom: 12px;
}

.removed-fields-section {
  margin-top: 16px;
}

.preview-label {
  font-size: 13px;
  font-weight: 600;
  color: #595959;
  margin-bottom: 8px;
}

.preview-sql {
  background: #f6f6f6;
  border: 1px solid #e8e8e8;
  border-radius: 6px;
  padding: 12px 16px;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 300px;
  overflow-y: auto;
}
</style>
