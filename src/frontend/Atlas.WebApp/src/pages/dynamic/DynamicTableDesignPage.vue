<template>
  <a-card class="page-card" :title="pageTitle">
    <template #extra>
      <a-space>
        <a-button @click="goBackToCrud">{{ t("dynamic.backToCrud", "返回数据页") }}</a-button>
        <a-button :loading="previewLoading" @click="handlePreview">
          {{ t("dynamic.previewAlter", "预览变更") }}
        </a-button>
        <a-button type="primary" :loading="applyLoading" @click="handleApply">
          {{ t("dynamic.applyAlter", "应用变更") }}
        </a-button>
      </a-space>
    </template>

    <a-spin :spinning="loading">
      <a-alert
        type="info"
        show-icon
        :message="t('dynamic.designHint', '第一版仅支持：新增字段、删除非保护字段、更新显示名与排序')"
        :description="t('dynamic.designRiskHint', '高风险变更（类型/可空性/默认值/唯一性/主键）已限制，提交前请先预览。')"
        style="margin-bottom: 12px"
      />

      <a-card size="small" :title="t('dynamic.existingFields', '现有字段')">
        <a-empty v-if="existingRows.length === 0" :description="t('dynamic.noData', '暂无数据')" />
        <a-space v-else direction="vertical" style="width: 100%" :size="8">
          <a-row
            v-for="row in existingRows"
            :key="`existing-${row.name}`"
            :gutter="[8, 8]"
            align="middle"
            style="border: 1px solid #f0f0f0; border-radius: 6px; padding: 8px"
          >
            <a-col :xs="24" :md="4">
              <a-typography-text strong>{{ row.name }}</a-typography-text>
              <div style="color: rgba(0, 0, 0, 0.45); font-size: 12px">{{ row.fieldType }}</div>
            </a-col>
            <a-col :xs="24" :md="6">
              <a-input v-model:value="row.displayName" :placeholder="t('dynamic.displayName', '显示名')" />
            </a-col>
            <a-col :xs="24" :md="4">
              <a-input-number v-model:value="row.sortOrder" :min="0" :precision="0" style="width: 100%" />
            </a-col>
            <a-col :xs="24" :md="6">
              <a-space>
                <a-tag v-if="row.isPrimaryKey" color="processing">{{ t("dynamic.primaryKey", "主键") }}</a-tag>
                <a-tag v-if="isProtectedField(row.name)" color="warning">{{ t("dynamic.protectedField", "受保护") }}</a-tag>
              </a-space>
            </a-col>
            <a-col :xs="24" :md="4">
              <a-switch
                v-model:checked="row.markedForRemove"
                :disabled="!canRemoveField(row)"
                checked-children="删除"
                un-checked-children="保留"
              />
            </a-col>
          </a-row>
        </a-space>
      </a-card>

      <a-card
        size="small"
        :title="t('dynamic.newFields', '新增字段')"
        style="margin-top: 12px"
      >
        <template #extra>
          <a-button size="small" type="dashed" @click="addNewRow">{{ t("dynamic.addField", "新增字段") }}</a-button>
        </template>

        <a-empty v-if="newRows.length === 0" :description="t('dynamic.noAddedFields', '暂无新增字段')" />
        <a-space v-else direction="vertical" style="width: 100%" :size="8">
          <a-row
            v-for="(row, index) in newRows"
            :key="`new-${index}`"
            :gutter="[8, 8]"
            align="middle"
            style="border: 1px solid #f0f0f0; border-radius: 6px; padding: 8px"
          >
            <a-col :xs="24" :md="3">
              <a-input v-model:value="row.name" :placeholder="t('dynamic.fieldName', '字段名')" />
            </a-col>
            <a-col :xs="24" :md="3">
              <a-input v-model:value="row.displayName" :placeholder="t('dynamic.displayName', '显示名')" />
            </a-col>
            <a-col :xs="24" :md="3">
              <a-select v-model:value="row.fieldType" style="width: 100%" :options="fieldTypeOptions" />
            </a-col>
            <a-col :xs="24" :md="2">
              <a-input-number
                v-model:value="row.length"
                :disabled="row.fieldType !== 'String'"
                :min="1"
                :max="4000"
                :precision="0"
                style="width: 100%"
              />
            </a-col>
            <a-col :xs="24" :md="2">
              <a-input-number
                v-model:value="row.precision"
                :disabled="row.fieldType !== 'Decimal'"
                :min="1"
                :max="38"
                :precision="0"
                style="width: 100%"
              />
            </a-col>
            <a-col :xs="24" :md="2">
              <a-input-number
                v-model:value="row.scale"
                :disabled="row.fieldType !== 'Decimal'"
                :min="0"
                :max="18"
                :precision="0"
                style="width: 100%"
              />
            </a-col>
            <a-col :xs="24" :md="2">
              <a-input-number v-model:value="row.sortOrder" :min="0" :precision="0" style="width: 100%" />
            </a-col>
            <a-col :xs="24" :md="4">
              <a-space>
                <a-checkbox v-model:checked="row.allowNull">{{ t("dynamic.allowNull", "可空") }}</a-checkbox>
                <a-checkbox v-model:checked="row.isUnique">{{ t("dynamic.unique", "唯一") }}</a-checkbox>
              </a-space>
            </a-col>
            <a-col :xs="24" :md="3">
              <a-button danger @click="removeNewRow(index)">{{ t("common.delete", "删除") }}</a-button>
            </a-col>
          </a-row>
        </a-space>
      </a-card>

      <a-card
        v-if="previewData"
        size="small"
        :title="t('dynamic.previewResult', '预览结果')"
        style="margin-top: 12px"
      >
        <a-space direction="vertical" style="width: 100%" :size="8">
          <a-tag color="blue">{{ t("dynamic.operationType", "操作类型") }}: {{ previewData.operationType }}</a-tag>
          <a-list
            size="small"
            bordered
            :data-source="previewData.sqlScripts"
            :render-item="renderPreviewScript"
          />
          <a-alert
            v-if="previewData.rollbackHint"
            type="warning"
            show-icon
            :message="previewData.rollbackHint"
          />
        </a-space>
      </a-card>
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, h, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type {
  DynamicFieldDefinition,
  DynamicFieldType,
  DynamicTableAlterPreviewResponse,
  DynamicTableAlterRequest,
  DynamicFieldUpdateDefinition
} from "@/types/dynamic-tables";
import {
  alterDynamicTableSchema,
  getDynamicTableDetail,
  getDynamicTableFields,
  previewDynamicTableAlter
} from "@/services/dynamic-tables";

interface ExistingFieldRow {
  name: string;
  displayName: string;
  fieldType: DynamicFieldType;
  sortOrder: number;
  isPrimaryKey: boolean;
  isAutoIncrement: boolean;
  markedForRemove: boolean;
  originalDisplayName: string;
  originalSortOrder: number;
}

interface NewFieldRow {
  name: string;
  displayName: string;
  fieldType: DynamicFieldType;
  length: number | null;
  precision: number | null;
  scale: number | null;
  allowNull: boolean;
  isUnique: boolean;
  defaultValue: string | null;
  sortOrder: number;
}

const protectedFieldSet = new Set(["id", "createdAt", "createdBy", "updatedAt", "updatedBy", "TenantIdValue"]);
const fieldTypeOptions = [
  { label: "Int", value: "Int" },
  { label: "Long", value: "Long" },
  { label: "Decimal", value: "Decimal" },
  { label: "String", value: "String" },
  { label: "Text", value: "Text" },
  { label: "Bool", value: "Bool" },
  { label: "DateTime", value: "DateTime" },
  { label: "Date", value: "Date" }
];

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const loading = ref(false);
const previewLoading = ref(false);
const applyLoading = ref(false);
const tableDisplayName = ref("");
const existingRows = ref<ExistingFieldRow[]>([]);
const newRows = ref<NewFieldRow[]>([]);
const previewData = ref<DynamicTableAlterPreviewResponse | null>(null);

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.params.tableKey === "string" ? decodeURIComponent(route.params.tableKey) : ""));
const pageTitle = computed(() =>
  tableDisplayName.value
    ? `${tableDisplayName.value} - ${t("dynamic.fieldDesign", "字段设计")}`
    : t("dynamic.fieldDesign", "字段设计")
);

const isProtectedField = (name: string) => protectedFieldSet.has(name);
const canRemoveField = (row: ExistingFieldRow) => !row.isPrimaryKey && !row.isAutoIncrement && !isProtectedField(row.name);
const renderPreviewScript = (item: string) => h("div", item);

const goBackToCrud = () => {
  if (!appId.value || !tableKey.value) {
    return;
  }
  void router.push(`/apps/${encodeURIComponent(appId.value)}/data/${encodeURIComponent(tableKey.value)}`);
};

const addNewRow = () => {
  const nextSortOrder = existingRows.value.length + newRows.value.length + 1;
  newRows.value.push({
    name: "",
    displayName: "",
    fieldType: "String",
    length: 50,
    precision: null,
    scale: null,
    allowNull: true,
    isUnique: false,
    defaultValue: null,
    sortOrder: nextSortOrder
  });
};

const removeNewRow = (index: number) => {
  newRows.value.splice(index, 1);
};

const mapExistingRows = (fields: DynamicFieldDefinition[]) => {
  existingRows.value = fields
    .map((field, index) => ({
      name: field.name,
      displayName: field.displayName || field.name,
      fieldType: field.fieldType,
      sortOrder: field.sortOrder ?? index + 1,
      isPrimaryKey: field.isPrimaryKey,
      isAutoIncrement: field.isAutoIncrement,
      markedForRemove: false,
      originalDisplayName: field.displayName || field.name,
      originalSortOrder: field.sortOrder ?? index + 1
    }))
    .sort((a, b) => a.sortOrder - b.sortOrder);
};

const loadData = async () => {
  if (!tableKey.value) {
    return;
  }

  loading.value = true;
  try {
    const [fields, detail] = await Promise.all([
      getDynamicTableFields(tableKey.value),
      getDynamicTableDetail(tableKey.value)
    ]);
    mapExistingRows(fields);
    tableDisplayName.value = detail?.displayName || tableKey.value;
  } catch (error) {
    message.error((error as Error).message || t("dynamic.loadPageFailed", "页面加载失败"));
  } finally {
    loading.value = false;
  }
};

const buildAlterRequest = (): DynamicTableAlterRequest | null => {
  const removeFields = existingRows.value
    .filter((row) => row.markedForRemove)
    .map((row) => row.name);

  const existingSet = new Set(existingRows.value.map((row) => row.name.toLowerCase()));
  const addedNameSet = new Set<string>();
  const addFields: DynamicFieldDefinition[] = [];

  for (const row of newRows.value) {
    const name = row.name.trim();
    if (!name) {
      message.error(t("dynamic.fieldNameRequired", "新增字段名不能为空"));
      return null;
    }
    if (existingSet.has(name.toLowerCase()) || addedNameSet.has(name.toLowerCase())) {
      message.error(t("dynamic.fieldNameDuplicate", "字段名重复，请调整后重试"));
      return null;
    }

    if (row.fieldType === "String" && (!row.length || row.length <= 0)) {
      message.error(t("dynamic.stringLengthRequired", "String 类型字段必须填写长度"));
      return null;
    }

    if (row.fieldType === "Decimal") {
      if (!row.precision || row.precision <= 0 || row.scale == null || row.scale < 0 || row.scale > row.precision) {
        message.error(t("dynamic.decimalInvalid", "Decimal 精度/小数位配置不合法"));
        return null;
      }
    }

    addedNameSet.add(name.toLowerCase());
    addFields.push({
      name,
      displayName: row.displayName?.trim() || name,
      fieldType: row.fieldType,
      length: row.fieldType === "String" ? row.length : null,
      precision: row.fieldType === "Decimal" ? row.precision : null,
      scale: row.fieldType === "Decimal" ? row.scale : null,
      allowNull: row.allowNull,
      isPrimaryKey: false,
      isAutoIncrement: false,
      isUnique: row.isUnique,
      defaultValue: row.defaultValue?.trim() || null,
      sortOrder: row.sortOrder
    });
  }

  const updateFields: DynamicFieldUpdateDefinition[] = existingRows.value
    .filter((row) => !row.markedForRemove)
    .filter((row) => row.displayName !== row.originalDisplayName || row.sortOrder !== row.originalSortOrder)
    .map((row) => ({
      name: row.name,
      displayName: row.displayName,
      sortOrder: row.sortOrder
    }));

  if (addFields.length === 0 && removeFields.length === 0 && updateFields.length === 0) {
    message.warning(t("dynamic.noSchemaChanges", "未检测到结构变更"));
    return null;
  }

  return {
    addFields,
    updateFields,
    removeFields
  };
};

const handlePreview = async () => {
  if (!tableKey.value) {
    return;
  }
  const request = buildAlterRequest();
  if (!request) {
    return;
  }

  previewLoading.value = true;
  try {
    previewData.value = await previewDynamicTableAlter(tableKey.value, request);
    message.success(t("dynamic.previewReady", "预览已生成，请确认后应用"));
  } catch (error) {
    previewData.value = null;
    message.error((error as Error).message || t("dynamic.previewFailed", "预览变更失败"));
  } finally {
    previewLoading.value = false;
  }
};

const handleApply = async () => {
  if (!tableKey.value) {
    return;
  }
  const request = buildAlterRequest();
  if (!request) {
    return;
  }

  applyLoading.value = true;
  try {
    await alterDynamicTableSchema(tableKey.value, request);
    message.success(t("dynamic.alterSuccess", "结构变更已应用"));
    previewData.value = null;
    newRows.value = [];
    await loadData();
  } catch (error) {
    message.error((error as Error).message || t("dynamic.alterFailed", "结构变更失败"));
  } finally {
    applyLoading.value = false;
  }
};

onMounted(() => {
  void loadData();
});
</script>
