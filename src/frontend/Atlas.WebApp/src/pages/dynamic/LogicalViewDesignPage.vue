<template>
  <div class="view-design-page">
    <!-- 顶部页头 -->
    <div class="page-header">
      <div class="header-left">
        <a-button type="text" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
        </a-button>
        <span class="page-title">{{ t("logicalView.pageTitle") }}</span>
        <span v-if="selectedAppId" class="header-sub">{{ selectedAppId }}</span>
      </div>
      <div class="header-actions">
        <a-button @click="openDesigner">
          <template #icon><PartitionOutlined /></template>
          {{ t("logicalView.openDesigner") }}
        </a-button>
        <a-button type="primary" @click="openCreateDrawer">
          <template #icon><PlusOutlined /></template>
          {{ t("logicalView.addView") }}
        </a-button>
      </div>
    </div>

    <!-- 类型切换 -->
    <div class="mode-bar">
      <a-radio-group v-model:value="activeViewType" button-style="solid" size="small">
        <a-radio-button value="all">全部</a-radio-button>
        <a-radio-button value="Logical">{{ t("logicalView.viewTypeLogical") }}</a-radio-button>
        <a-radio-button value="NativeSql">{{ t("logicalView.viewTypeNativeSql") }}</a-radio-button>
      </a-radio-group>
      <a-input
        v-model:value="keyword"
        :placeholder="t('dynamic.searchTablesPlaceholder')"
        allow-clear
        style="width: 240px"
      >
        <template #prefix><SearchOutlined style="color: rgba(0,0,0,.25)" /></template>
      </a-input>
    </div>

    <!-- 视图列表 -->
    <div class="page-body">
      <a-spin :spinning="loading">
        <a-empty v-if="!loading && filteredViews.length === 0" :description="t('logicalView.noViews')" style="margin-top: 80px">
          <a-button type="primary" @click="openCreateDrawer">{{ t("logicalView.addView") }}</a-button>
        </a-empty>
        <a-table
          v-else
          :dataSource="filteredViews"
          :columns="columns"
          row-key="viewKey"
          :pagination="{ pageSize: 20, showSizeChanger: false }"
          size="middle"
          class="view-table"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'viewKey'">
              <code>{{ record.viewKey }}</code>
            </template>
            <template v-else-if="column.key === 'name'">
              <a @click="openEditDrawer(record)">{{ record.name }}</a>
              <div v-if="record.description" style="font-size: 12px; color: #8c8c8c">{{ record.description }}</div>
            </template>
            <template v-else-if="column.key === 'viewType'">
              <a-tag :color="record.viewType === 'NativeSql' ? 'purple' : 'blue'">
                {{ record.viewType === 'NativeSql' ? t('logicalView.viewTypeNativeSql') : t('logicalView.viewTypeLogical') }}
              </a-tag>
            </template>
            <template v-else-if="column.key === 'isPublished'">
              <a-badge :status="record.isPublished ? 'success' : 'default'" :text="record.isPublished ? t('logicalView.isPublished') : t('logicalView.notPublished')" />
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-space>
                <a-button type="link" size="small" @click="openEditDrawer(record)">{{ t("common.edit") }}</a-button>
                <a-button
                  v-if="!record.isPublished"
                  type="link"
                  size="small"
                  :loading="publishingKey === record.viewKey"
                  @click="handlePublish(record.viewKey)"
                >
                  {{ t("logicalView.publishView") }}
                </a-button>
                <a-popconfirm :title="t('logicalView.confirmDelete')" @confirm="handleDelete(record.viewKey)">
                  <a-button type="link" danger size="small">{{ t("common.delete") }}</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-spin>
    </div>

    <!-- 新建/编辑视图抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      :title="editingView ? t('logicalView.editView') : t('logicalView.addView')"
      placement="right"
      width="560"
      @close="resetDrawer"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="formRules">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item :label="t('logicalView.viewName')" name="name">
              <a-input v-model:value="form.name" :placeholder="t('logicalView.viewNamePlaceholder')" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item :label="t('logicalView.viewKey')" name="viewKey">
              <a-input v-model:value="form.viewKey" :placeholder="t('logicalView.viewKeyPlaceholder')" :disabled="!!editingView" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item :label="t('logicalView.viewType')" name="viewType">
          <a-radio-group v-model:value="form.viewType" button-style="solid" @change="onViewTypeChange">
            <a-radio-button value="Logical">{{ t("logicalView.viewTypeLogical") }}</a-radio-button>
            <a-radio-button value="NativeSql">{{ t("logicalView.viewTypeNativeSql") }}</a-radio-button>
          </a-radio-group>
        </a-form-item>

        <!-- 逻辑视图：来源表 + 输出字段 -->
        <template v-if="form.viewType === 'Logical'">
          <a-form-item :label="t('logicalView.sourceTable')" name="sourceTableKey">
            <a-select
              v-model:value="form.sourceTableKey"
              :options="tableOptions"
              :placeholder="t('relation.selectTargetTable')"
              show-search
              :filter-option="filterOption"
              @change="onSourceTableChange"
            />
          </a-form-item>
          <a-form-item :label="t('logicalView.outputFields')">
            <div class="output-field-list">
              <div
                v-for="(field, index) in form.outputFields"
                :key="index"
                class="output-field-row"
              >
                <a-select v-model:value="field.sourceFieldKey" :options="sourceFieldOptions" style="width: 140px" />
                <a-input v-model:value="field.label" :placeholder="t('logicalView.fieldLabel')" style="width: 120px" />
                <a-button danger size="small" @click="form.outputFields.splice(index, 1)">
                  <template #icon><DeleteOutlined /></template>
                </a-button>
              </div>
              <a-button size="small" type="dashed" block @click="addOutputField">
                <template #icon><PlusOutlined /></template>
                {{ t("logicalView.addOutputField") }}
              </a-button>
            </div>
          </a-form-item>
          <a-form-item :label="t('logicalView.sorts')">
            <div v-for="(sort, i) in form.sorts" :key="i" class="sort-row">
              <a-select v-model:value="sort.field" :options="sourceFieldOptions" style="width: 140px" />
              <a-select v-model:value="sort.direction" style="width: 100px">
                <a-select-option value="asc">{{ t("logicalView.sortAsc") }}</a-select-option>
                <a-select-option value="desc">{{ t("logicalView.sortDesc") }}</a-select-option>
              </a-select>
              <a-button danger size="small" @click="form.sorts.splice(i, 1)">
                <template #icon><DeleteOutlined /></template>
              </a-button>
            </div>
            <a-button size="small" type="dashed" block @click="form.sorts.push({ field: '', direction: 'asc' })">
              <template #icon><PlusOutlined /></template>
              {{ t("logicalView.addSort") }}
            </a-button>
          </a-form-item>
          <a-form-item>
            <a-checkbox v-model:checked="form.readOnly">{{ t("logicalView.readOnly") }}</a-checkbox>
          </a-form-item>
        </template>

        <!-- SQL 视图：SQL 内容 -->
        <template v-else>
          <a-form-item :label="t('logicalView.sqlContent')" name="sqlContent">
            <a-textarea
              v-model:value="form.sqlContent"
              :placeholder="t('logicalView.sqlPlaceholder')"
              :rows="8"
              style="font-family: monospace; font-size: 13px"
            />
          </a-form-item>
          <a-button :loading="previewingSql" @click="handlePreviewSql">{{ t("logicalView.previewSql") }}</a-button>
          <pre v-if="sqlPreview" class="sql-preview">{{ sqlPreview }}</pre>
        </template>

        <a-form-item :label="t('logicalView.description')" name="description">
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
import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  PlusOutlined,
  SearchOutlined,
  DeleteOutlined,
  PartitionOutlined
} from "@ant-design/icons-vue";
import type { Rule } from "ant-design-vue/es/form";
import type { DynamicViewListItem } from "@/types/dynamic-dataflow";
import {
  getDynamicViewsPaged,
  createDynamicView,
  updateDynamicView,
  deleteDynamicView,
  publishDynamicView,
  previewDynamicViewSql
} from "@/services/dynamic-views";
import { getDynamicTableFields, getAllDynamicTables } from "@/services/dynamic-tables";
import { getCurrentAppIdFromStorage } from "@/utils/app-context";

interface ViewListItem extends DynamicViewListItem {
  viewType?: "Logical" | "NativeSql";
}

interface OutputFieldRow {
  sourceFieldKey: string;
  label: string;
}

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const selectedAppId = computed(() => appId.value || getCurrentAppIdFromStorage() || "");

const loading = ref(false);
const saving = ref(false);
const previewingSql = ref(false);
const publishingKey = ref<string | null>(null);
const drawerOpen = ref(false);
const formRef = ref();
const views = ref<ViewListItem[]>([]);
const tableOptions = ref<{ label: string; value: string }[]>([]);
const sourceFieldOptions = ref<{ label: string; value: string }[]>([]);
const editingView = ref<ViewListItem | null>(null);
const keyword = ref("");
const activeViewType = ref<"all" | "Logical" | "NativeSql">("all");
const sqlPreview = ref<string | null>(null);

const form = reactive({
  viewKey: "",
  name: "",
  viewType: "Logical" as "Logical" | "NativeSql",
  sourceTableKey: "",
  outputFields: [] as OutputFieldRow[],
  sorts: [] as { field: string; direction: "asc" | "desc" }[],
  readOnly: true,
  sqlContent: "",
  description: ""
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: t("validation.required") }],
  viewKey: [
    { required: true, message: t("validation.required") },
    { pattern: /^[a-z][a-z0-9_]{1,63}$/, message: t("dynamic.tableKeyRule") }
  ]
};

const filteredViews = computed(() => {
  let list = views.value;
  if (activeViewType.value !== "all") {
    list = list.filter((v) => v.viewType === activeViewType.value);
  }
  if (keyword.value.trim()) {
    const kw = keyword.value.trim().toLowerCase();
    list = list.filter((v) => v.viewKey.toLowerCase().includes(kw) || v.name.toLowerCase().includes(kw));
  }
  return list;
});

const columns = computed(() => [
  { title: t("logicalView.viewKey"), key: "viewKey", width: 200 },
  { title: t("logicalView.viewName"), key: "name" },
  { title: t("logicalView.viewType"), key: "viewType", width: 130 },
  { title: t("logicalView.isPublished"), key: "isPublished", width: 120 },
  { title: t("common.updatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 160 },
  { title: t("common.actions"), key: "actions", width: 200 }
]);

const filterOption = (input: string, option: { label: string; value: string }): boolean =>
  option.label.toLowerCase().includes(input.toLowerCase());

const loadViews = async () => {
  loading.value = true;
  try {
    const result = await getDynamicViewsPaged({ pageIndex: 1, pageSize: 200, keyword: "" });
    views.value = result.items.map((v) => ({
      ...v,
      viewType: "Logical" as const
    }));
  } catch (error) {
    message.error((error as Error).message || t("logicalView.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const loadTableOptions = async () => {
  try {
    const tables = await getAllDynamicTables();
    tableOptions.value = tables.map((t) => ({ label: `${t.displayName} (${t.tableKey})`, value: t.tableKey }));
  } catch {
    // ignore
  }
};

const onSourceTableChange = async (tableKey: string) => {
  sourceFieldOptions.value = [];
  form.outputFields = [];
  if (!tableKey) return;
  try {
    const fields = await getDynamicTableFields(tableKey);
    sourceFieldOptions.value = fields.map((f) => ({ label: `${f.name} (${f.fieldType})`, value: f.name }));
    form.outputFields = fields.map((f) => ({ sourceFieldKey: f.name, label: f.displayName ?? f.name }));
  } catch {
    // ignore
  }
};

const addOutputField = () => {
  form.outputFields.push({ sourceFieldKey: "", label: "" });
};

const onViewTypeChange = () => {
  sqlPreview.value = null;
};

const handlePreviewSql = async () => {
  if (!form.sqlContent.trim()) {
    message.warning("SQL 内容为空");
    return;
  }
  previewingSql.value = true;
  try {
    const result = await previewDynamicViewSql({
      id: undefined,
      appId: selectedAppId.value,
      viewKey: form.viewKey || "__preview__",
      name: form.name || "preview",
      nodes: [
        {
          id: "sql-node",
          type: "sourceView",
          config: { sql: form.sqlContent }
        }
      ],
      edges: [],
      outputFields: []
    });
    sqlPreview.value = result.sql || form.sqlContent;
  } catch (error) {
    message.error((error as Error).message || t("logicalView.sqlPreviewFailed"));
  } finally {
    previewingSql.value = false;
  }
};

const openCreateDrawer = () => {
  editingView.value = null;
  Object.assign(form, {
    viewKey: "",
    name: "",
    viewType: "Logical",
    sourceTableKey: "",
    outputFields: [],
    sorts: [],
    readOnly: true,
    sqlContent: "",
    description: ""
  });
  sourceFieldOptions.value = [];
  sqlPreview.value = null;
  drawerOpen.value = true;
};

const openEditDrawer = async (record: ViewListItem) => {
  editingView.value = record;
  Object.assign(form, {
    viewKey: record.viewKey,
    name: record.name,
    viewType: record.viewType ?? "Logical",
    sourceTableKey: "",
    outputFields: [],
    sorts: [],
    readOnly: true,
    sqlContent: "",
    description: record.description ?? ""
  });
  sqlPreview.value = null;
  drawerOpen.value = true;
};

const resetDrawer = () => {
  drawerOpen.value = false;
  editingView.value = null;
  sqlPreview.value = null;
};

const handleSave = async () => {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }
  saving.value = true;
  try {
    const sourceNodeId = "src";
    const outputNodeId = "out";
    const definition = {
      appId: selectedAppId.value,
      viewKey: form.viewKey,
      name: form.name,
      description: form.description || undefined,
      nodes: form.viewType === "Logical"
        ? [
            { id: sourceNodeId, type: "sourceTable" as const, tableKey: form.sourceTableKey },
            { id: outputNodeId, type: "outputView" as const }
          ]
        : [
            { id: sourceNodeId, type: "sourceView" as const, config: { sql: form.sqlContent } },
            { id: outputNodeId, type: "outputView" as const }
          ],
      edges: [{ id: "e1", sourceNodeId, targetNodeId: outputNodeId }],
      outputFields: form.outputFields.map((f) => ({
        targetFieldKey: f.sourceFieldKey || f.label,
        targetLabel: f.label,
        targetType: "String" as const,
        source: form.viewType === "Logical" ? { nodeId: sourceNodeId, fieldKey: f.sourceFieldKey } : undefined,
        pipeline: []
      })),
      sorts: form.sorts.filter((s) => s.field)
    };
    if (editingView.value) {
      await updateDynamicView(form.viewKey, definition);
      message.success(t("logicalView.updateSuccess"));
    } else {
      await createDynamicView(definition);
      message.success(t("logicalView.createSuccess"));
    }
    resetDrawer();
    await loadViews();
  } catch (error) {
    message.error((error as Error).message || t("logicalView.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const handleDelete = async (viewKey: string) => {
  try {
    await deleteDynamicView(viewKey);
    message.success(t("logicalView.deleteSuccess"));
    await loadViews();
  } catch (error) {
    message.error((error as Error).message || t("logicalView.deleteFailed"));
  }
};

const handlePublish = async (viewKey: string) => {
  publishingKey.value = viewKey;
  try {
    await publishDynamicView(viewKey);
    message.success(t("logicalView.publishSuccess"));
    await loadViews();
  } catch (error) {
    message.error((error as Error).message || t("logicalView.publishFailed"));
  } finally {
    publishingKey.value = null;
  }
};

const goBack = () => {
  if (appId.value) {
    void router.push(`/apps/${appId.value}/data`);
  } else {
    void router.back();
  }
};

const openDesigner = () => {
  if (appId.value) void router.push(`/apps/${appId.value}/data/designer?mode=view`);
};

onMounted(() => {
  void loadViews();
  void loadTableOptions();
});
</script>

<style scoped>
.view-design-page {
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

.header-sub {
  font-size: 13px;
  color: #8c8c8c;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mode-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 24px;
  border-bottom: 1px solid #f0f0f0;
  background: #fafafa;
  flex-shrink: 0;
}

.page-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
}

.view-table {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.output-field-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.output-field-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.sort-row {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
}

.sql-preview {
  margin-top: 8px;
  padding: 10px;
  background: #1e1e1e;
  color: #d4d4d4;
  font-size: 12px;
  font-family: monospace;
  border-radius: 6px;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 200px;
  overflow-y: auto;
}
</style>
