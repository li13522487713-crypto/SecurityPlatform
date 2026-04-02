<template>
  <div class="field-design-page">
    <!-- 顶部页头：表名 + 状态 + 操作按钮 -->
    <div class="page-header">
      <div class="header-left">
        <a-button type="text" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
        </a-button>
        <span class="page-title">{{ pageTitle }}</span>
        <a-tag v-if="tableStatus" :color="tableStatusColor">{{ tableStatusLabel }}</a-tag>
      </div>
      <div class="header-actions">
        <a-button :loading="previewLoading" @click="handlePreviewDdl">
          <template #icon><CodeOutlined /></template>
          {{ t("fieldDesign.ddlPreview") }}
        </a-button>
        <a-button :loading="savingDraft" @click="handleSaveDraft">
          <template #icon><SaveOutlined /></template>
          {{ t("fieldDesign.saveDraft") }}
        </a-button>
        <a-button :loading="validating" @click="handleValidate">
          <template #icon><CheckCircleOutlined /></template>
          {{ t("fieldDesign.validateDraft") }}
        </a-button>
        <a-button type="primary" :loading="publishing" @click="handlePublish">
          <template #icon><CloudUploadOutlined /></template>
          {{ t("fieldDesign.publishDraft") }}
        </a-button>
      </div>
    </div>

    <div class="page-body">
      <!-- 左侧：字段列表 -->
      <div class="field-list-panel">
        <div class="field-list-toolbar">
          <span class="toolbar-title">{{ t("designer.entityModeling.fields") }}</span>
          <a-button size="small" type="dashed" @click="addNewField">
            <template #icon><PlusOutlined /></template>
            {{ t("fieldDesign.addField") }}
          </a-button>
        </div>
        <a-spin :spinning="loading">
          <div class="field-list">
            <div
              v-for="(field, index) in allFields"
              :key="field.name || `new-${index}`"
              class="field-item"
              :class="{
                'is-selected': selectedFieldIndex === index,
                'is-new': field._status === 'new',
                'is-modified': field._status === 'modified',
                'is-deleted': field._status === 'deleted'
              }"
              @click="selectField(index)"
            >
              <div class="field-item-left">
                <KeyOutlined v-if="field.isPrimaryKey" class="pk-icon" />
                <span class="field-physical-name">{{ field.name || t("fieldDesign.fieldName") }}</span>
                <span v-if="field.displayName" class="field-display-name">{{ field.displayName }}</span>
              </div>
              <div class="field-item-right">
                <a-tag :color="fieldTypeColor(field.fieldType)" class="type-tag">{{ field.fieldType }}</a-tag>
                <a-tag :color="statusTagColor(field._status)" class="status-tag">
                  {{ statusTagLabel(field._status) }}
                </a-tag>
              </div>
            </div>
            <div v-if="allFields.length === 0 && !loading" class="field-empty">
              <a-empty :description="t('dynamic.noData', '暂无字段')" :image="false" />
            </div>
          </div>
        </a-spin>
      </div>

      <!-- 右侧：字段配置面板 -->
      <div class="field-config-panel">
        <div v-if="selectedField === null" class="config-empty">
          <a-empty :description="t('fieldDesign.selectField')" :image="false" />
        </div>
        <div v-else class="config-content">
          <div class="config-header">
            <span class="config-title">{{ t("fieldDesign.rightPanelTitle") }}</span>
            <a-space>
              <a-popconfirm
                v-if="canDeleteField(selectedField)"
                :title="t('fieldDesign.confirmDeleteField')"
                @confirm="deleteSelectedField"
              >
                <a-button danger size="small">
                  <template #icon><DeleteOutlined /></template>
                  {{ t("fieldDesign.deleteField") }}
                </a-button>
              </a-popconfirm>
            </a-space>
          </div>

          <!-- 基础配置 -->
          <a-form layout="vertical" :model="selectedField" class="config-form">
            <div class="config-section-title">{{ t("fieldDesign.basicConfig") }}</div>
            <a-row :gutter="16">
              <a-col :span="12">
                <a-form-item :label="t('fieldDesign.fieldName')">
                  <a-input
                    v-model:value="selectedField.name"
                    :disabled="!selectedField._isNew"
                    @change="markFieldModified"
                  />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item :label="t('fieldDesign.displayName')">
                  <a-input v-model:value="selectedField.displayName" @change="markFieldModified" />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item :label="t('fieldDesign.fieldType')">
                  <a-select
                    v-model:value="selectedField.fieldType"
                    :disabled="!selectedField._isNew"
                    :options="fieldTypeOptions"
                    @change="markFieldModified"
                  />
                </a-form-item>
              </a-col>
              <a-col :span="12">
                <a-form-item :label="t('fieldDesign.defaultValue')">
                  <a-input v-model:value="selectedField.defaultValue" allow-clear @change="markFieldModified" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-row :gutter="16">
              <a-col :span="8">
                <a-form-item>
                  <a-checkbox
                    v-model:checked="selectedField.allowNull"
                    :disabled="selectedField.isPrimaryKey"
                    @change="markFieldModified"
                  >
                    {{ t("fieldDesign.allowNull") }}
                  </a-checkbox>
                </a-form-item>
              </a-col>
              <a-col :span="8">
                <a-form-item>
                  <a-checkbox
                    v-model:checked="selectedField.isUnique"
                    :disabled="selectedField.isPrimaryKey"
                    @change="markFieldModified"
                  >
                    {{ t("fieldDesign.unique") }}
                  </a-checkbox>
                </a-form-item>
              </a-col>
              <a-col :span="8">
                <a-form-item>
                  <a-checkbox v-model:checked="selectedField.isPrimaryKey" disabled>
                    {{ t("fieldDesign.isPrimaryKey") }}
                  </a-checkbox>
                </a-form-item>
              </a-col>
            </a-row>

            <!-- 高级配置（折叠） -->
            <a-collapse ghost>
              <a-collapse-panel key="advanced" :header="t('fieldDesign.advancedConfig')">
                <a-row :gutter="16">
                  <a-col v-if="selectedField.fieldType === 'String'" :span="12">
                    <a-form-item :label="t('fieldDesign.length')">
                      <a-input-number
                        v-model:value="selectedField.length"
                        :min="1"
                        :max="4000"
                        :precision="0"
                        style="width: 100%"
                        @change="markFieldModified"
                      />
                    </a-form-item>
                  </a-col>
                  <a-col v-if="selectedField.fieldType === 'Decimal'" :span="12">
                    <a-form-item :label="t('fieldDesign.precision')">
                      <a-input-number
                        v-model:value="selectedField.precision"
                        :min="1"
                        :max="38"
                        :precision="0"
                        style="width: 100%"
                        @change="markFieldModified"
                      />
                    </a-form-item>
                  </a-col>
                  <a-col v-if="selectedField.fieldType === 'Decimal'" :span="12">
                    <a-form-item :label="t('fieldDesign.scale')">
                      <a-input-number
                        v-model:value="selectedField.scale"
                        :min="0"
                        :max="18"
                        :precision="0"
                        style="width: 100%"
                        @change="markFieldModified"
                      />
                    </a-form-item>
                  </a-col>
                  <a-col :span="12">
                    <a-form-item>
                      <a-checkbox v-model:checked="selectedField.isSystemField" @change="markFieldModified">
                        {{ t("fieldDesign.isSystemField") }}
                      </a-checkbox>
                    </a-form-item>
                  </a-col>
                </a-row>
              </a-collapse-panel>
            </a-collapse>
          </a-form>
        </div>
      </div>
    </div>

    <!-- 底部 DDL 预览 -->
    <div v-if="ddlContent" class="ddl-panel">
      <div class="ddl-header">
        <span class="ddl-title">{{ t("fieldDesign.ddlPreview") }}</span>
        <a-button type="text" size="small" @click="ddlContent = null">
          <template #icon><CloseOutlined /></template>
        </a-button>
      </div>
      <pre class="ddl-content">{{ ddlContent }}</pre>
    </div>

    <!-- 草稿列表抽屉 -->
    <a-drawer
      v-model:open="draftDrawerOpen"
      :title="t('fieldDesign.draftList')"
      placement="right"
      width="480"
    >
      <a-list :data-source="draftList" :loading="draftLoading" size="small">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta>
              <template #title>
                <a-space>
                  <span>{{ item.objectKey }}</span>
                  <a-tag :color="changeTypeColor(item.changeType)">{{ item.changeType }}</a-tag>
                  <a-tag :color="riskTagColor(item.riskLevel)">{{ riskLabel(item.riskLevel) }}</a-tag>
                </a-space>
              </template>
              <template #description>
                <a-tag :color="draftStatusColor(item.status)">{{ draftStatusLabel(item.status) }}</a-tag>
                <span style="margin-left: 8px; color: #8c8c8c; font-size: 12px">{{ item.createdAt }}</span>
              </template>
            </a-list-item-meta>
            <template #actions>
              <a-popconfirm :title="t('fieldDesign.abandonDraft')" @confirm="handleAbandonDraft(item.id)">
                <a-button type="text" danger size="small">{{ t("fieldDesign.abandonDraft") }}</a-button>
              </a-popconfirm>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  PlusOutlined,
  SaveOutlined,
  CheckCircleOutlined,
  CloudUploadOutlined,
  CodeOutlined,
  DeleteOutlined,
  CloseOutlined,
  KeyOutlined
} from "@ant-design/icons-vue";
import type { DynamicFieldType } from "@/types/dynamic-tables";
import { getDynamicTableDetail, getDynamicTableFields, previewDynamicTableAlter, alterDynamicTableSchema } from "@/services/dynamic-tables";
import {
  listSchemaDrafts,
  createSchemaDraft,
  validateSchemaDraft,
  publishSchemaDrafts,
  abandonSchemaDraft,
  type SchemaDraftListItem
} from "@/services/schema-drafts";

type FieldStatus = "stable" | "new" | "modified" | "deleted";

interface FieldRow {
  name: string;
  displayName: string | null;
  fieldType: DynamicFieldType;
  length: number | null;
  precision: number | null;
  scale: number | null;
  allowNull: boolean;
  isUnique: boolean;
  isPrimaryKey: boolean;
  isAutoIncrement: boolean;
  defaultValue: string | null;
  sortOrder: number;
  isSystemField: boolean;
  _status: FieldStatus;
  _isNew: boolean;
  _originalDisplayName: string | null;
  _originalSortOrder: number;
}

const fieldTypeOptions: { label: string; value: DynamicFieldType }[] = [
  { label: "String", value: "String" },
  { label: "Text", value: "Text" },
  { label: "Int", value: "Int" },
  { label: "Long", value: "Long" },
  { label: "Decimal", value: "Decimal" },
  { label: "Bool", value: "Bool" },
  { label: "DateTime", value: "DateTime" },
  { label: "Date", value: "Date" }
];

const protectedFieldSet = new Set(["id", "createdat", "createdby", "updatedat", "updatedby", "tenantidvalue"]);

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.params.tableKey === "string" ? decodeURIComponent(route.params.tableKey) : ""));

const loading = ref(false);
const previewLoading = ref(false);
const savingDraft = ref(false);
const validating = ref(false);
const publishing = ref(false);
const draftLoading = ref(false);
const draftDrawerOpen = ref(false);

const tableDisplayName = ref("");
const tableStatus = ref<string>("");
const allFields = ref<FieldRow[]>([]);
const selectedFieldIndex = ref<number | null>(null);
const ddlContent = ref<string | null>(null);
const draftList = ref<SchemaDraftListItem[]>([]);

const pageTitle = computed(() =>
  tableDisplayName.value ? `${tableDisplayName.value} - ${t("fieldDesign.pageTitle")}` : t("fieldDesign.pageTitle")
);

const tableStatusColor = computed(() => {
  const map: Record<string, string> = { Draft: "orange", Active: "green", HasUnpublishedChanges: "gold", Archived: "default" };
  return map[tableStatus.value] ?? "default";
});

const tableStatusLabel = computed(() => {
  const map: Record<string, string> = {
    Draft: t("dynamic.statusDraft"),
    Active: t("dynamic.statusActive"),
    HasUnpublishedChanges: t("dynamic.statusHasUnpublishedChanges"),
    Archived: t("dynamic.statusArchived")
  };
  return map[tableStatus.value] ?? tableStatus.value;
});

const selectedField = computed<FieldRow | null>(() => {
  if (selectedFieldIndex.value === null || selectedFieldIndex.value >= allFields.value.length) return null;
  return allFields.value[selectedFieldIndex.value];
});

const fieldTypeColor = (type: DynamicFieldType): string => {
  const map: Partial<Record<DynamicFieldType, string>> = {
    String: "blue",
    Text: "cyan",
    Int: "purple",
    Long: "purple",
    Decimal: "orange",
    Bool: "green",
    DateTime: "geekblue",
    Date: "magenta"
  };
  return map[type] ?? "default";
};

const statusTagColor = (status: FieldStatus): string => {
  const map: Record<FieldStatus, string> = { stable: "default", new: "green", modified: "gold", deleted: "red" };
  return map[status];
};

const statusTagLabel = (status: FieldStatus): string => {
  const map: Record<FieldStatus, string> = {
    stable: t("fieldDesign.fieldStatusStable"),
    new: t("fieldDesign.fieldStatusNew"),
    modified: t("fieldDesign.fieldStatusModified"),
    deleted: t("fieldDesign.fieldStatusDeleted")
  };
  return map[status];
};

const changeTypeColor = (ct: string): string => {
  const map: Record<string, string> = { Create: "green", Update: "gold", Delete: "red" };
  return map[ct] ?? "default";
};

const riskTagColor = (risk: string): string => {
  const map: Record<string, string> = { Low: "green", Medium: "gold", High: "red" };
  return map[risk] ?? "default";
};

const riskLabel = (risk: string): string => {
  const map: Record<string, string> = {
    Low: t("fieldDesign.riskLow"),
    Medium: t("fieldDesign.riskMedium"),
    High: t("fieldDesign.riskHigh")
  };
  return map[risk] ?? risk;
};

const draftStatusColor = (status: string): string => {
  const map: Record<string, string> = { Pending: "orange", Validated: "blue", Published: "green", Abandoned: "default" };
  return map[status] ?? "default";
};

const draftStatusLabel = (status: string): string => {
  const map: Record<string, string> = {
    Pending: t("fieldDesign.draftStatusPending"),
    Validated: t("fieldDesign.draftStatusValidated"),
    Published: t("fieldDesign.draftStatusPublished"),
    Abandoned: t("fieldDesign.draftStatusAbandoned")
  };
  return map[status] ?? status;
};

const canDeleteField = (field: FieldRow): boolean => {
  return !field.isPrimaryKey && !field.isAutoIncrement && !protectedFieldSet.has(field.name.toLowerCase()) && field._status !== "deleted";
};

const selectField = (index: number) => {
  selectedFieldIndex.value = index;
};

const markFieldModified = () => {
  const f = selectedField.value;
  if (f && f._status === "stable") {
    const hasChange =
      f.displayName !== f._originalDisplayName ||
      f.sortOrder !== f._originalSortOrder;
    if (hasChange) f._status = "modified";
  }
};

const addNewField = () => {
  const nextOrder = allFields.value.length + 1;
  allFields.value.push({
    name: "",
    displayName: "",
    fieldType: "String",
    length: 50,
    precision: null,
    scale: null,
    allowNull: true,
    isUnique: false,
    isPrimaryKey: false,
    isAutoIncrement: false,
    defaultValue: null,
    sortOrder: nextOrder,
    isSystemField: false,
    _status: "new",
    _isNew: true,
    _originalDisplayName: "",
    _originalSortOrder: nextOrder
  });
  selectedFieldIndex.value = allFields.value.length - 1;
};

const deleteSelectedField = () => {
  if (selectedField.value === null) return;
  const f = selectedField.value;
  if (f._isNew) {
    const idx = selectedFieldIndex.value!;
    allFields.value.splice(idx, 1);
    selectedFieldIndex.value = null;
  } else {
    f._status = "deleted";
  }
};

const loadData = async () => {
  if (!tableKey.value) return;
  loading.value = true;
  try {
    const [fields, detail] = await Promise.all([
      getDynamicTableFields(tableKey.value),
      getDynamicTableDetail(tableKey.value)
    ]);
    allFields.value = fields
      .map((f, i) => ({
        name: f.name,
        displayName: f.displayName ?? null,
        fieldType: f.fieldType,
        length: f.length ?? null,
        precision: f.precision ?? null,
        scale: f.scale ?? null,
        allowNull: f.allowNull,
        isUnique: f.isUnique ?? false,
        isPrimaryKey: f.isPrimaryKey,
        isAutoIncrement: f.isAutoIncrement,
        defaultValue: f.defaultValue ?? null,
        sortOrder: f.sortOrder ?? i + 1,
        isSystemField: false,
        _status: "stable" as FieldStatus,
        _isNew: false,
        _originalDisplayName: f.displayName ?? null,
        _originalSortOrder: f.sortOrder ?? i + 1
      }))
      .sort((a, b) => a.sortOrder - b.sortOrder);
    tableDisplayName.value = detail?.displayName || tableKey.value;
    tableStatus.value = (detail as { status?: string })?.status ?? "";
    if (allFields.value.length > 0) selectedFieldIndex.value = 0;
  } catch (error) {
    message.error((error as Error).message || t("dynamic.loadPageFailed"));
  } finally {
    loading.value = false;
  }
};

const loadDrafts = async () => {
  if (!tableKey.value) return;
  draftLoading.value = true;
  try {
    draftList.value = await listSchemaDrafts(tableKey.value);
  } catch {
    // ignore
  } finally {
    draftLoading.value = false;
  }
};

const handlePreviewDdl = async () => {
  if (!tableKey.value) return;
  const newFields = allFields.value.filter(f => f._status === "new" && f.name.trim());
  const removeFields = allFields.value.filter(f => f._status === "deleted").map(f => f.name);
  const updateFields = allFields.value
    .filter(f => f._status === "modified")
    .map(f => ({ name: f.name, displayName: f.displayName ?? f.name, sortOrder: f.sortOrder }));

  if (newFields.length === 0 && removeFields.length === 0 && updateFields.length === 0) {
    message.warning(t("fieldDesign.noChanges"));
    return;
  }
  previewLoading.value = true;
  try {
    const result = await previewDynamicTableAlter(tableKey.value, {
      addFields: newFields.map(f => ({
        name: f.name.trim(),
        displayName: f.displayName?.trim() || f.name,
        fieldType: f.fieldType,
        length: f.fieldType === "String" ? f.length : null,
        precision: f.fieldType === "Decimal" ? f.precision : null,
        scale: f.fieldType === "Decimal" ? f.scale : null,
        allowNull: f.allowNull,
        isPrimaryKey: false,
        isAutoIncrement: false,
        isUnique: f.isUnique,
        defaultValue: f.defaultValue ?? null,
        sortOrder: f.sortOrder
      })),
      updateFields,
      removeFields
    });
    ddlContent.value = result.sqlScripts?.join("\n\n") ?? "";
  } catch (error) {
    message.error((error as Error).message || t("fieldDesign.ddlPreviewFailed"));
  } finally {
    previewLoading.value = false;
  }
};

const handleSaveDraft = async () => {
  if (!tableKey.value) return;
  const changedFields = allFields.value.filter(f => f._status !== "stable");
  if (changedFields.length === 0) {
    message.warning(t("fieldDesign.noChanges"));
    return;
  }
  savingDraft.value = true;
  try {
    for (const f of changedFields) {
      const changeType = f._status === "new" ? "Create" : f._status === "deleted" ? "Delete" : "Update";
      await createSchemaDraft({
        tableKey: tableKey.value,
        objectType: "Field",
        objectKey: f.name.trim(),
        changeType,
        afterSnapshot: f._status !== "deleted" ? {
          name: f.name,
          displayName: f.displayName,
          fieldType: f.fieldType,
          length: f.length,
          precision: f.precision,
          scale: f.scale,
          allowNull: f.allowNull,
          isUnique: f.isUnique,
          defaultValue: f.defaultValue,
          sortOrder: f.sortOrder
        } : null
      });
    }
    message.success(t("fieldDesign.saveDraftSuccess"));
    await loadDrafts();
    draftDrawerOpen.value = true;
  } catch (error) {
    message.error((error as Error).message || t("fieldDesign.saveDraftFailed"));
  } finally {
    savingDraft.value = false;
  }
};

const handleValidate = async () => {
  if (!tableKey.value) return;
  if (draftList.value.length === 0) await loadDrafts();
  const pendingDrafts = draftList.value.filter(d => d.status === "Pending");
  if (pendingDrafts.length === 0) {
    message.info(t("fieldDesign.noChanges"));
    return;
  }
  validating.value = true;
  try {
    const results = await Promise.all(pendingDrafts.map(d => validateSchemaDraft(d.id)));
    const allValid = results.every(r => r.isValid);
    if (allValid) {
      message.success(t("fieldDesign.validateSuccess"));
    } else {
      const errors = results.flatMap(r => r.messages).filter(Boolean);
      message.error(errors.join("; ") || t("fieldDesign.validateFailed"));
    }
    await loadDrafts();
  } catch (error) {
    message.error((error as Error).message || t("fieldDesign.validateFailed"));
  } finally {
    validating.value = false;
  }
};

const handlePublish = async () => {
  if (!tableKey.value) return;
  publishing.value = true;
  try {
    const result = await publishSchemaDrafts(tableKey.value);
    if (result.failedCount === 0) {
      message.success(t("fieldDesign.publishSuccess"));
      await loadData();
      await loadDrafts();
    } else {
      message.warning(`发布完成：${result.publishedCount} 成功，${result.failedCount} 失败`);
      if (result.errors.length > 0) {
        message.error(result.errors.join("; "));
      }
    }
  } catch (error) {
    message.error((error as Error).message || t("fieldDesign.publishFailed"));
  } finally {
    publishing.value = false;
  }
};

const handleAbandonDraft = async (draftId: string) => {
  try {
    await abandonSchemaDraft(draftId);
    message.success(t("fieldDesign.abandonSuccess"));
    await loadDrafts();
  } catch (error) {
    message.error((error as Error).message || t("fieldDesign.abandonFailed"));
  }
};

const goBack = () => {
  if (appId.value) {
    void router.push(`/apps/${appId.value}/data`);
  } else {
    void router.back();
  }
};

onMounted(() => {
  void loadData();
  void loadDrafts();
});

watch(tableKey, (val) => {
  if (val) {
    void loadData();
    void loadDrafts();
  }
});
</script>

<style scoped>
.field-design-page {
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
  display: flex;
  flex: 1;
  overflow: hidden;
}

/* 字段列表面板 */
.field-list-panel {
  width: 320px;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  background: #fafafa;
}

.field-list-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  border-bottom: 1px solid #f0f0f0;
}

.toolbar-title {
  font-size: 13px;
  font-weight: 600;
  color: #595959;
}

.field-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.field-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 14px;
  cursor: pointer;
  border-right: 3px solid transparent;
  transition: background 0.15s;
}

.field-item:hover {
  background: #f0f0f0;
}

.field-item.is-selected {
  background: #e6f4ff;
  border-right-color: #1677ff;
}

.field-item.is-new {
  border-left: 3px solid #52c41a;
}

.field-item.is-modified {
  border-left: 3px solid #faad14;
}

.field-item.is-deleted {
  opacity: 0.5;
  text-decoration: line-through;
}

.field-item-left {
  display: flex;
  flex-direction: column;
  gap: 2px;
  flex: 1;
  min-width: 0;
}

.pk-icon {
  color: #faad14;
  font-size: 12px;
  margin-right: 4px;
}

.field-physical-name {
  font-size: 13px;
  font-family: 'Courier New', monospace;
  color: #262626;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.field-display-name {
  font-size: 11px;
  color: #8c8c8c;
}

.field-item-right {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 3px;
  flex-shrink: 0;
  margin-left: 8px;
}

.type-tag {
  font-size: 11px;
  line-height: 18px;
  padding: 0 5px;
}

.status-tag {
  font-size: 10px;
  line-height: 16px;
  padding: 0 4px;
}

.field-empty {
  padding: 32px 16px;
  text-align: center;
}

/* 字段配置面板 */
.field-config-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.config-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.config-content {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.config-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 24px;
  border-bottom: 1px solid #f0f0f0;
  flex-shrink: 0;
}

.config-title {
  font-size: 14px;
  font-weight: 600;
  color: #1f1f1f;
}

.config-form {
  flex: 1;
  overflow-y: auto;
  padding: 20px 24px;
}

.config-section-title {
  font-size: 13px;
  font-weight: 600;
  color: #595959;
  margin-bottom: 12px;
}

/* DDL 预览面板 */
.ddl-panel {
  border-top: 1px solid #f0f0f0;
  background: #1e1e1e;
  flex-shrink: 0;
  max-height: 200px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.ddl-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 16px;
  background: #2d2d2d;
  flex-shrink: 0;
}

.ddl-title {
  font-size: 12px;
  font-weight: 600;
  color: #ababab;
}

.ddl-content {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
  margin: 0;
  font-size: 12px;
  font-family: 'Courier New', monospace;
  color: #d4d4d4;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
