<template>
  <div class="app-builder-page">
    <!-- 左侧页面树 -->
    <div class="builder-sidebar">
      <div class="sidebar-header">
        <a-button size="small" @click="goBack">{{ t("lowcodeBuilder.back") }}</a-button>
        <h3>{{ appDetail?.name ?? t("lowcodeBuilder.loading") }}</h3>
      </div>
      <div class="sidebar-actions">
        <a-button type="primary" size="small" block @click="handleAddPage">{{ t("lowcodeBuilder.createPage") }}</a-button>
        <a-button size="small" block style="margin-top: 8px" @click="openAppVersionDrawer">{{ t("lowcodeBuilder.versionHistory") }}</a-button>
      </div>
      <div class="page-tree">
        <div
          v-for="page in pages"
          :key="page.id"
          class="page-tree-item"
          :class="{ active: selectedPageId === page.id }"
          @click="selectPage(page.id)"
        >
          <span class="page-icon">{{ pageTypeIcon(page.pageType) }}</span>
          <span class="page-name" :style="{ paddingLeft: `${(pageDepthMap[page.id] ?? 0) * 12}px` }">{{ page.name }}</span>
          <a-tag v-if="page.isPublished" color="green" size="small">{{ t("lowcode.builderExtra.published") }}</a-tag>
          <a-dropdown trigger="click" @click.stop>
            <a-button type="text" size="small">...</a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="edit" @click="handleEditPage(page)">{{ t("lowcode.builderExtra.editInfo") }}</a-menu-item>
                <a-menu-item key="versions" @click="handleOpenVersionHistory(page)">{{ t("lowcode.builderExtra.pageVersions") }}</a-menu-item>
                <a-menu-item v-if="!page.isPublished" key="publish" @click="handlePublishPage(page.id)">{{ t("lowcode.builderExtra.publishBtn") }}</a-menu-item>
                <a-menu-item key="delete" danger @click="handleDeletePage(page.id)">{{ t("lowcode.builderExtra.delete") }}</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
        <div v-if="pages.length === 0 && !loading" class="empty-hint">
          {{ t("lowcodeBuilder.emptyPageHint") }}
        </div>
      </div>
    </div>

    <!-- 右侧设计器区域 -->
    <div class="builder-main">
      <template v-if="selectedPageId && currentSchema">
        <div class="main-toolbar">
          <div class="main-toolbar-left">
            <span class="page-title">{{ currentPageName }}</span>
            <a-tooltip :title="t('lowcode.builderExtra.undo')">
              <a-button size="small" :disabled="!schemaHistory.canUndo" @click="handleUndo">
                ↩
              </a-button>
            </a-tooltip>
            <a-tooltip :title="t('lowcode.builderExtra.redo')">
              <a-button size="small" :disabled="!schemaHistory.canRedo" @click="handleRedo">
                ↪
              </a-button>
            </a-tooltip>
          </div>
          <div class="main-toolbar-actions">
            <a-radio-group v-model:value="devicePreview" button-style="solid" size="small">
              <a-radio-button value="desktop">PC</a-radio-button>
              <a-radio-button value="tablet">Tablet</a-radio-button>
              <a-radio-button value="mobile">Mobile</a-radio-button>
            </a-radio-group>
            <a-select
              v-model:value="selectedEnvironmentCode"
              style="width: 180px"
              :options="environmentOptions"
              allow-clear
              :placeholder="t('lowcode.builderExtra.phPreviewEnv')"
              @change="handleEnvironmentChange"
            />
            <a-button @click="openEnvironmentManager">{{ t("lowcode.builderExtra.envManager") }}</a-button>
            <a-button @click="handleSaveAsTemplate">{{ t("lowcode.builderExtra.saveAsTemplate") }}</a-button>
            <a-button :loading="saving" @click="handleSavePageSchema">{{ t("lowcode.builderExtra.save") }}</a-button>
            <a-button type="primary" :loading="publishing" @click="handlePublishPage(selectedPageId!)">{{ t("lowcode.builderExtra.publishBtn") }}</a-button>
          </div>
        </div>
        <div :style="{ maxWidth: devicePreview === 'mobile' ? '375px' : devicePreview === 'tablet' ? '768px' : '100%', margin: '0 auto' }">
          <component
            :is="AmisEditor"
            ref="pageEditorRef"
            :schema="currentSchema"
            :schema-revision="currentSchemaRevision"
            :is-mobile="devicePreview === 'mobile'"
            height="calc(100vh - 112px)"
            @change="handlePageSchemaChange"
          />
        </div>
      </template>
      <template v-else>
        <div class="empty-main">
          <p>{{ t("lowcodeBuilder.emptyMainHint") }}</p>
        </div>
      </template>
    </div>

    <!-- 新建/编辑页面对话框 -->
    <a-modal
      v-model:open="pageFormVisible"
      :title="pageFormMode === 'create' ? t('lowcodeBuilder.createPageModalTitle') : t('lowcodeBuilder.editPageModalTitle')"
      :ok-text="t('common.confirm')"
      :cancel-text="t('common.cancel')"
      @ok="handlePageFormSubmit"
    >
      <a-form layout="vertical">
        <a-form-item v-if="pageFormMode === 'create'" :label="t('lowcodeBuilder.pageKeyLabel')" required>
          <a-input v-model:value="pageForm.pageKey" :placeholder="t('lowcodeBuilder.pageKeyPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.pageNameLabel')" required>
          <a-input v-model:value="pageForm.name" :placeholder="t('lowcodeBuilder.pageNamePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.pageTypeLabel')" required>
          <a-select v-model:value="pageForm.pageType">
            <a-select-option value="List">{{ t("lowcodeBuilder.pageTypeList") }}</a-select-option>
            <a-select-option value="Form">{{ t("lowcodeBuilder.pageTypeForm") }}</a-select-option>
            <a-select-option value="Detail">{{ t("lowcodeBuilder.pageTypeDetail") }}</a-select-option>
            <a-select-option value="Dashboard">{{ t("lowcodeBuilder.pageTypeDashboard") }}</a-select-option>
            <a-select-option value="Blank">{{ t("lowcodeBuilder.pageTypeBlank") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.routePathLabel')">
          <a-input v-model:value="pageForm.routePath" :placeholder="t('lowcodeBuilder.routePathPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.pageDescriptionLabel')">
          <a-textarea v-model:value="pageForm.description" :rows="2" />
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.sortOrderLabel')">
          <a-input-number v-model:value="pageForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-drawer
      v-model:open="appVersionDrawerVisible"
      :title="t('lowcodeBuilder.versionDrawerTitle')"
      placement="right"
      width="720"
      :destroy-on-close="true"
    >
      <a-table
        :columns="appVersionColumns"
        :data-source="appVersionItems"
        :loading="appVersionLoading"
        row-key="id"
        :pagination="{
          total: appVersionTotal,
          current: appVersionPageIndex,
          pageSize: appVersionPageSize,
          showQuickJumper: true,
          onChange: onAppVersionPageChange
        }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'actionType'">
            <a-tag :color="record.actionType === 'Publish' ? 'blue' : 'orange'">
              {{ record.actionType }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'sourceVersionId'">
            {{ record.sourceVersionId ?? "-" }}
          </template>
          <template v-else-if="column.key === 'note'">
            {{ record.note ?? "-" }}
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-popconfirm
              :title="t('lowcodeBuilder.rollbackConfirm')"
              :ok-text="t('lowcodeBuilder.rollback')"
              :cancel-text="t('common.cancel')"
              @confirm="handleRollbackAppVersion(record.id)"
            >
              <a-button
                type="link"
                size="small"
                :loading="appRollbackingVersionId === record.id"
              >
                {{ t("lowcodeBuilder.rollback") }}
              </a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-drawer>

    <!-- 页面版本历史 -->
    <a-modal
      v-model:open="versionModalVisible"
      :title="t('lowcode.appBuilder.pageVerTitle')"
      :footer="null"
      width="680px"
    >
      <a-table
        :data-source="pageVersions"
        :loading="pageVersionLoading"
        :pagination="false"
        row-key="id"
        size="small"
      >
        <a-table-column key="snapshotVersion" :title="t('lowcode.appBuilder.colVerNo')" data-index="snapshotVersion" width="120px" />
        <a-table-column key="createdAt" :title="t('lowcode.appBuilder.colPublishedAt')" data-index="createdAt" width="220px" />
        <a-table-column key="createdBy" :title="t('lowcode.appBuilder.colPublisher')" data-index="createdBy" width="120px" />
        <a-table-column key="action" :title="t('common.actions')" width="140px">
          <template #default="{ record }">
            <a-button size="small" @click="handleRollbackPageVersion(record.id)">{{ t("lowcode.appBuilder.rollbackThis") }}</a-button>
          </template>
        </a-table-column>
      </a-table>
    </a-modal>

    <a-modal
      v-model:open="environmentModalVisible"
      :title="t('lowcode.appBuilder.envTitle')"
      :footer="null"
      width="760px"
    >
      <div style="margin-bottom: 12px;">
        <a-button type="primary" @click="openEnvironmentForm('create')">{{ t("lowcode.appBuilder.newEnv") }}</a-button>
      </div>
      <a-table
        :data-source="environments"
        :loading="environmentLoading"
        :pagination="false"
        row-key="id"
        size="small"
      >
        <a-table-column key="name" :title="t('lowcode.appBuilder.colEnvName')" data-index="name" width="160px" />
        <a-table-column key="code" :title="t('lowcode.appBuilder.colEnvCode')" data-index="code" width="120px" />
        <a-table-column key="isDefault" :title="t('lowcode.appBuilder.colIsDefault')" width="80px">
          <template #default="{ record }">
            <a-tag v-if="record.isDefault" color="green">{{ t("lowcode.appBuilder.tagDefault") }}</a-tag>
            <span v-else>-</span>
          </template>
        </a-table-column>
        <a-table-column key="isActive" :title="t('lowcode.appBuilder.colEnvStatus')" width="80px">
          <template #default="{ record }">
            <a-tag :color="record.isActive ? 'blue' : 'default'">{{
              record.isActive ? t("lowcode.appBuilder.envEnabled") : t("lowcode.appBuilder.envDisabled")
            }}</a-tag>
          </template>
        </a-table-column>
        <a-table-column key="description" :title="t('lowcode.appBuilder.colEnvDesc')" data-index="description" />
        <a-table-column key="action" :title="t('common.actions')" width="180px">
          <template #default="{ record }">
            <a-space size="small">
              <a-button type="link" size="small" @click="openEnvironmentForm('edit', record)">{{ t("lowcode.appBuilder.envEdit") }}</a-button>
              <a-button type="link" size="small" danger @click="handleDeleteEnvironment(record.id)">{{ t("lowcode.appBuilder.envDelete") }}</a-button>
            </a-space>
          </template>
        </a-table-column>
      </a-table>
    </a-modal>

    <a-modal
      v-model:open="environmentFormVisible"
      :title="environmentFormMode === 'create' ? t('lowcode.appBuilder.modalEnvCreate') : t('lowcode.appBuilder.modalEnvEdit')"
      :ok-text="t('lowcode.appBuilder.ok')"
      :cancel-text="t('common.cancel')"
      @ok="submitEnvironmentForm"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('lowcode.appBuilder.labelEnvName')" required>
          <a-input v-model:value="environmentForm.name" />
        </a-form-item>
        <a-form-item :label="t('lowcode.appBuilder.labelEnvCode')" required>
          <a-input v-model:value="environmentForm.code" :disabled="environmentFormMode === 'edit'" />
        </a-form-item>
        <a-form-item :label="t('lowcode.appBuilder.labelVarJson')" required>
          <a-textarea v-model:value="environmentForm.variablesJson" :rows="6" />
        </a-form-item>
        <a-form-item :label="t('lowcode.appBuilder.labelDesc')">
          <a-textarea v-model:value="environmentForm.description" :rows="2" />
        </a-form-item>
        <a-form-item :label="t('lowcode.appBuilder.labelStatus')">
          <a-switch v-model:checked="environmentForm.isActive" />
        </a-form-item>
        <a-form-item :label="t('lowcode.appBuilder.labelDefaultEnv')">
          <a-switch v-model:checked="environmentForm.isDefault" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, markRaw, onMounted, onUnmounted, reactive, ref, shallowRef } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message, Modal } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useSchemaHistoryStore } from "@/stores/schemaHistory";
import type {
  LowCodeAppDetail,
  LowCodeAppVersionListItem,
  LowCodeEnvironmentListItem,
  LowCodePageListItem,
  LowCodePageTreeNode,
  LowCodePageVersionListItem
} from "@/types/lowcode";
import {
  getLowCodeAppDetail,
  getLowCodeAppVersionsPaged,
  rollbackLowCodeAppVersion,
  getLowCodeEnvironments,
  getLowCodeEnvironmentDetail,
  getLowCodePageDetail,
  getLowCodeRuntimePageSchema,
  getLowCodePageTree,
  getLowCodePageVersions,
  createLowCodeEnvironment,
  updateLowCodeEnvironment,
  deleteLowCodeEnvironment,
  createLowCodePage,
  updateLowCodePage,
  updateLowCodePageSchema,
  publishLowCodePage,
  rollbackLowCodePage,
  deleteLowCodePage
} from "@/services/lowcode";
import { createTemplate } from "@/services/templates";

const route = useRoute();
const router = useRouter();
const { t, locale } = useI18n();
const AmisEditor = defineAsyncComponent(() => import("@/components/amis/AmisEditor.vue"));
const appId = computed(() => String(route.params.id ?? route.params.appId ?? ""));
const MAX_PAGE_SCHEMA_CACHE = 3;

const schemaHistory = useSchemaHistoryStore();
const isMounted = ref(false);
let historyTimer: number | undefined;
onUnmounted(() => {
  isMounted.value = false;
  document.removeEventListener("keydown", handleKeyDown);
  if (historyTimer) {
    window.clearTimeout(historyTimer);
    historyTimer = undefined;
  }
  schemaHistory.reset();
  pageSchemas.value = {};
  pageSchemaRevisions.value = {};
});

function handleKeyDown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === "z" && !e.shiftKey) {
    e.preventDefault();
    handleUndo();
  }
  if ((e.ctrlKey || e.metaKey) && ((e.key === "z" && e.shiftKey) || e.key === "y")) {
    e.preventDefault();
    handleRedo();
  }
}

function handleUndo() {
  const prev = schemaHistory.undo();
  if (prev && selectedPageId.value) {
    rememberPageSchema(selectedPageId.value, prev);
  }
}

function handleRedo() {
  const next = schemaHistory.redo();
  if (next && selectedPageId.value) {
    rememberPageSchema(selectedPageId.value, next);
  }
}

const devicePreview = ref<"desktop" | "tablet" | "mobile">("desktop");
const loading = ref(true);
const saving = ref(false);
const publishing = ref(false);
const appDetail = ref<LowCodeAppDetail | null>(null);
const pages = ref<LowCodePageListItem[]>([]);
const selectedPageId = ref<string | null>(null);
const pageEditorRef = ref<{ getSchema: () => Record<string, unknown> } | null>(null);
const appVersionDrawerVisible = ref(false);
const appVersionLoading = ref(false);
const appVersionItems = ref<LowCodeAppVersionListItem[]>([]);
const appVersionTotal = ref(0);
const appVersionPageIndex = ref(1);
const appVersionPageSize = ref(10);
const appRollbackingVersionId = ref<string | null>(null);
const appVersionColumns = computed(() => [
  { title: t("lowcodeBuilder.version"), dataIndex: "version", key: "version", width: 100 },
  { title: t("lowcodeBuilder.actionType"), key: "actionType", width: 110 },
  { title: t("lowcodeBuilder.sourceVersion"), key: "sourceVersionId", width: 150 },
  { title: t("lowcodeBuilder.note"), key: "note" },
  { title: t("lowcodeBuilder.createdAt"), key: "createdAt", width: 170 },
  { title: t("lowcodeBuilder.createdBy"), dataIndex: "createdBy", key: "createdBy", width: 100 },
  { title: t("common.actions"), key: "actions", width: 90 }
]);

// Page schemas cache
const pageSchemas = shallowRef<Record<string, Record<string, unknown>>>({});
const pageDepthMap = ref<Record<string, number>>({});
const pageSchemaRevisions = ref<Record<string, number>>({});

const currentSchema = computed(() => {
  if (!selectedPageId.value) return null;
  return pageSchemas.value[selectedPageId.value] ?? null;
});
const currentSchemaRevision = computed(() => {
  if (!selectedPageId.value) {
    return 0;
  }
  return pageSchemaRevisions.value[selectedPageId.value] ?? 0;
});

const currentPageName = computed(() => {
  return pages.value.find(p => p.id === selectedPageId.value)?.name ?? "";
});

const pageFormVisible = ref(false);
const pageFormMode = ref<"create" | "edit">("create");
const editingPageId = ref<string | null>(null);
const pageForm = reactive({
  pageKey: "",
  name: "",
  pageType: "List",
  routePath: "",
  description: "",
  sortOrder: 0
});

const versionModalVisible = ref(false);
const pageVersionLoading = ref(false);
const versionTargetPageId = ref<string | null>(null);
const pageVersions = ref<LowCodePageVersionListItem[]>([]);
const environments = ref<LowCodeEnvironmentListItem[]>([]);
const environmentLoading = ref(false);
const selectedEnvironmentCode = ref<string>();
const environmentModalVisible = ref(false);
const environmentFormVisible = ref(false);
const environmentFormMode = ref<"create" | "edit">("create");
const editingEnvironmentId = ref<string | null>(null);
const environmentForm = reactive({
  name: "",
  code: "",
  description: "",
  variablesJson: "{\n  \"API_BASE\": \"https://api.example.com\"\n}",
  isDefault: false,
  isActive: true
});

const environmentOptions = computed(() => environments.value
  .filter(item => item.isActive)
  .map(item => ({
    label: `${item.name} (${item.code})`,
    value: item.code
  })));

const pageTypeIcon = (type: string) => {
  const icons: Record<string, string> = {
    List: "T",
    Form: "F",
    Detail: "D",
    Dashboard: "B",
    Blank: "P"
  };
  return icons[type] ?? "P";
};

const resolveTemplateCategory = (pageType: string): number => {
  if (pageType === "Form") return 0;
  if (pageType === "List" || pageType === "Detail" || pageType === "Dashboard") return 1;
  return 1;
};

const generateDefaultSchema = (pageType: string, pageName: string): Record<string, unknown> => {
  const statTitle = t("lowcode.appBuilder.statCardTitle");
  const loadingText = t("lowcode.appBuilder.loadingData");
  const nameLabel = t("lowcode.appBuilder.fieldName");
  switch (pageType) {
    case "List":
      return {
        type: "page",
        title: pageName,
        body: [{
          type: "crud",
          api: "/api/v1/dynamic-tables",
          columns: [
            { name: "id", label: "ID" },
            { name: "name", label: nameLabel }
          ]
        }]
      };
    case "Form":
      return {
        type: "page",
        title: pageName,
        body: [{
          type: "form",
          api: "/api/v1/dynamic-tables",
          body: [
            { type: "input-text", name: "name", label: nameLabel, required: true }
          ]
        }]
      };
    case "Dashboard":
      return {
        type: "page",
        title: pageName,
        body: [{
          type: "grid",
          columns: [
            { body: [{ type: "card", header: { title: statTitle }, body: loadingText }] },
            { body: [{ type: "card", header: { title: statTitle }, body: loadingText }] }
          ]
        }]
      };
    default:
      return { type: "page", title: pageName, body: [] };
  }
};

const parseSchemaJson = (schemaJson: string | null | undefined): Record<string, unknown> | null => {
  if (!schemaJson) {
    return null;
  }

  try {
    const parsed = JSON.parse(schemaJson) as unknown;
    if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
      return null;
    }
    return parsed as Record<string, unknown>;
  } catch {
    return null;
  }
};

const rememberPageSchema = (pageId: string, schema: Record<string, unknown>) => {
  const nextSchemas = {
    ...pageSchemas.value,
    [pageId]: markRaw(schema)
  };

  const lruKeys = Object.keys(nextSchemas);
  while (lruKeys.length > MAX_PAGE_SCHEMA_CACHE) {
    const evictKey = lruKeys.find((key) => key !== pageId);
    if (!evictKey) {
      break;
    }
    delete nextSchemas[evictKey];
    const nextRevisions = { ...pageSchemaRevisions.value };
    delete nextRevisions[evictKey];
    pageSchemaRevisions.value = nextRevisions;
    lruKeys.splice(lruKeys.indexOf(evictKey), 1);
  }

  pageSchemas.value = nextSchemas;
  pageSchemaRevisions.value = {
    ...pageSchemaRevisions.value,
    [pageId]: (pageSchemaRevisions.value[pageId] ?? 0) + 1
  };
};

const dropPageSchema = (pageId: string) => {
  if (!pageSchemas.value[pageId]) {
    return;
  }

  const nextSchemas = { ...pageSchemas.value };
  delete nextSchemas[pageId];
  pageSchemas.value = nextSchemas;

  const nextRevisions = { ...pageSchemaRevisions.value };
  delete nextRevisions[pageId];
  pageSchemaRevisions.value = nextRevisions;
};

const flattenPageTree = (
  treeNodes: LowCodePageTreeNode[],
  depth = 0,
  target: LowCodePageListItem[] = [],
  depthRecord: Record<string, number> = {}
): { list: LowCodePageListItem[]; depthRecord: Record<string, number> } => {
  for (const node of treeNodes) {
    const { children, ...item } = node;
    target.push(item);
    depthRecord[item.id] = depth;
    flattenPageTree(children, depth + 1, target, depthRecord);
  }
  return { list: target, depthRecord };
};

const loadApp = async () => {
  loading.value = true;
  try {
    const [detail, pageTree, envs]  = await Promise.all([
      getLowCodeAppDetail(appId.value),
      getLowCodePageTree(appId.value),
      getLowCodeEnvironments(appId.value)
    ]);

    if (!isMounted.value) return;
    appDetail.value = detail;
    environments.value = envs;
    if (!selectedEnvironmentCode.value) {
      selectedEnvironmentCode.value = envs.find(item => item.isDefault)?.code;
    }
    const flattened = flattenPageTree(pageTree);
    pages.value = flattened.list;
    pageDepthMap.value = flattened.depthRecord;

    if (selectedPageId.value && !flattened.list.some(page => page.id === selectedPageId.value)) {
      selectedPageId.value = null;
    }
    if (!selectedPageId.value && flattened.list.length > 0) {
      await selectPage(flattened.list[0].id);

      if (!isMounted.value) return;
    }
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.loadAppFailed"));
  } finally {
    loading.value = false;
  }
};

const selectPage = async (pageId: string) => {
  selectedPageId.value = pageId;

  if (!pageSchemas.value[pageId]) {
    const page = pages.value.find(p => p.id === pageId);
    if (page) {
      try {
        const [detail, runtime]  = await Promise.all([
          getLowCodePageDetail(pageId),
          getLowCodeRuntimePageSchema(pageId, "draft", selectedEnvironmentCode.value)
        ]);

        if (!isMounted.value) return;
        rememberPageSchema(
          pageId,
          parseSchemaJson(runtime.schemaJson)
          ?? generateDefaultSchema(detail.pageType, detail.name)
        );
      } catch {
        rememberPageSchema(pageId, { type: "page", title: page.name, body: [] });
      }
    }
  }

  const loadedSchema = pageSchemas.value[pageId];
  if (loadedSchema) {
    schemaHistory.init(loadedSchema);
  }
};

const handleAddPage = () => {
  pageFormMode.value = "create";
  editingPageId.value = null;
  pageForm.pageKey = "";
  pageForm.name = "";
  pageForm.pageType = "List";
  pageForm.routePath = "";
  pageForm.description = "";
  pageForm.sortOrder = pages.value.length;
  pageFormVisible.value = true;
};

const handleEditPage = (page: LowCodePageListItem) => {
  pageFormMode.value = "edit";
  editingPageId.value = page.id;
  pageForm.pageKey = page.pageKey;
  pageForm.name = page.name;
  pageForm.pageType = page.pageType;
  pageForm.routePath = page.routePath ?? "";
  pageForm.description = page.description ?? "";
  pageForm.sortOrder = page.sortOrder;
  pageFormVisible.value = true;
};

const handlePageFormSubmit = async () => {
  if (!pageForm.name.trim()) {
    message.warning(t("lowcodeBuilder.pageNameRequired"));
    return;
  }

  try {
    if (pageFormMode.value === "create") {
      if (!pageForm.pageKey.trim()) {
        message.warning(t("lowcodeBuilder.pageKeyRequired"));
        return;
      }
      const schema = generateDefaultSchema(pageForm.pageType, pageForm.name);
      await createLowCodePage(appId.value, {
        pageKey: pageForm.pageKey,
        name: pageForm.name,
        pageType: pageForm.pageType,
        schemaJson: JSON.stringify(schema),
        routePath: pageForm.routePath || undefined,
        description: pageForm.description || undefined,
        sortOrder: pageForm.sortOrder
      });

      if (!isMounted.value) return;
      message.success(t("lowcodeBuilder.createPageSuccess"));
    } else if (editingPageId.value) {
      let currentSchema = pageSchemas.value[editingPageId.value];
      if (!currentSchema) {
        const pageDetail  = await getLowCodePageDetail(editingPageId.value);

        if (!isMounted.value) return;
        currentSchema = parseSchemaJson(pageDetail.schemaJson)
          ?? generateDefaultSchema(pageDetail.pageType, pageDetail.name);
        rememberPageSchema(editingPageId.value, currentSchema);
      }
      await updateLowCodePage(editingPageId.value, {
        name: pageForm.name,
        pageType: pageForm.pageType,
        schemaJson: JSON.stringify(currentSchema),
        routePath: pageForm.routePath || undefined,
        description: pageForm.description || undefined,
        sortOrder: pageForm.sortOrder
      });

      if (!isMounted.value) return;
      message.success(t("lowcodeBuilder.updatePageSuccess"));
    }
    pageFormVisible.value = false;
    await loadApp();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.pageOperationFailed"));
  }
};

const handlePageSchemaChange = (newSchema: Record<string, unknown>) => {
  if (selectedPageId.value) {
    rememberPageSchema(selectedPageId.value, newSchema);
    if (historyTimer) {
      window.clearTimeout(historyTimer);
    }
    historyTimer = window.setTimeout(() => {
      schemaHistory.pushState(newSchema);
    }, 400);
  }
};

const handleSavePageSchema = async () => {
  if (!selectedPageId.value) return;

  saving.value = true;
  try {
    const currentSchema = pageEditorRef.value?.getSchema()
      ?? pageSchemas.value[selectedPageId.value];
    if (currentSchema) {
      await updateLowCodePageSchema(selectedPageId.value, JSON.stringify(currentSchema));

      if (!isMounted.value) return;
      message.success(t("lowcodeBuilder.saveSchemaSuccess"));
    }
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.saveSchemaFailed"));
  } finally {
    saving.value = false;
  }
};

const handleSaveAsTemplate = async () => {
  if (!selectedPageId.value) {
    message.warning(t("lowcode.appBuilder.warnSelectPage"));
    return;
  }
  const page = pages.value.find((item) => item.id === selectedPageId.value);
  if (!page) {
    message.warning(t("lowcode.appBuilder.warnNoPage"));
    return;
  }
  const schemaToSave = pageEditorRef.value?.getSchema() ?? pageSchemas.value[selectedPageId.value];
  if (!schemaToSave) {
    message.warning(t("lowcode.appBuilder.warnNoSchema"));
    return;
  }

  try {
    const appName = appDetail.value?.name ?? t("lowcode.appBuilder.appFallback");
    await createTemplate({
      name: t("lowcode.appBuilder.templateNameTpl", { app: appName, page: page.name }),
      category: resolveTemplateCategory(page.pageType),
      schemaJson: JSON.stringify(schemaToSave),
      description: t("lowcode.appBuilder.templateDescTpl", { app: appDetail.value?.name ?? "", page: page.name }),
      tags: `lowcode,page,${page.pageType.toLowerCase()}`,
      version: "1.0.0"
    });

    if (!isMounted.value) return;
    message.success(t("lowcode.appBuilder.templateSaved"));
  } catch (error) {
    message.error((error as Error).message || t("lowcode.appBuilder.saveTemplateFailed"));
  }
};

const handlePublishPage = async (pageId: string) => {
  publishing.value = true;
  try {
    await publishLowCodePage(pageId);

    if (!isMounted.value) return;
    message.success(t("lowcodeBuilder.publishSuccess"));
    await loadApp();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.publishFailed"));
  } finally {
    publishing.value = false;
  }
};

const handleOpenVersionHistory = async (page: LowCodePageListItem) => {
  versionModalVisible.value = true;
  versionTargetPageId.value = page.id;
  pageVersionLoading.value = true;
  try {
    pageVersions.value = await getLowCodePageVersions(page.id);

    if (!isMounted.value) return;
  } catch (error) {
    pageVersions.value = [];
    message.error((error as Error).message || t("lowcode.appBuilder.loadVerFailed"));
  } finally {
    pageVersionLoading.value = false;
  }
};

const handleRollbackPageVersion = async (versionId: string) => {
  if (!versionTargetPageId.value) {
    return;
  }

  Modal.confirm({
    title: t("lowcode.appBuilder.rollbackModalTitle"),
    content: t("lowcode.appBuilder.rollbackModalContent"),
    okText: t("lowcode.appBuilder.rollbackConfirmBtn"),
    cancelText: t("common.cancel"),
    onOk: async () => {
      await rollbackLowCodePage(versionTargetPageId.value!, versionId);

      if (!isMounted.value) return;
      message.success(t("lowcode.appBuilder.rollbackSuccess"));
      versionModalVisible.value = false;
      pageSchemas.value = {};
      pageSchemaRevisions.value = {};
      await loadApp();

      if (!isMounted.value) return;
      if (selectedPageId.value) {
        await selectPage(selectedPageId.value);

        if (!isMounted.value) return;
      }
    }
  });
};

const handleEnvironmentChange = async () => {
  if (!selectedPageId.value) {
    return;
  }
  dropPageSchema(selectedPageId.value);
  await selectPage(selectedPageId.value);

  if (!isMounted.value) return;
};

const openEnvironmentManager = async () => {
  environmentModalVisible.value = true;
  environmentLoading.value = true;
  try {
    environments.value = await getLowCodeEnvironments(appId.value);

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("lowcode.appBuilder.loadEnvFailed"));
  } finally {
    environmentLoading.value = false;
  }
};

const openEnvironmentForm = async (mode: "create" | "edit", item?: LowCodeEnvironmentListItem) => {
  environmentFormMode.value = mode;
  editingEnvironmentId.value = item?.id ?? null;
  if (mode === "edit" && item?.id) {
    try {
      const detail  = await getLowCodeEnvironmentDetail(item.id);

      if (!isMounted.value) return;
      environmentForm.name = detail.name;
      environmentForm.code = detail.code;
      environmentForm.description = detail.description ?? "";
      environmentForm.variablesJson = detail.variablesJson;
      environmentForm.isDefault = detail.isDefault;
      environmentForm.isActive = detail.isActive;
    } catch (error) {
      message.error((error as Error).message || t("lowcode.appBuilder.loadEnvDetailFailed"));
      return;
    }
  } else {
    environmentForm.name = "";
    environmentForm.code = "";
    environmentForm.description = "";
    environmentForm.variablesJson = "{\n  \"API_BASE\": \"https://api.example.com\"\n}";
    environmentForm.isDefault = false;
    environmentForm.isActive = true;
  }
  environmentFormVisible.value = true;
};

const submitEnvironmentForm = async () => {
  if (!environmentForm.name.trim() || !environmentForm.code.trim()) {
    message.warning(t("lowcode.appBuilder.warnEnvNameCode"));
    return;
  }

  try {
    JSON.parse(environmentForm.variablesJson);
  } catch {
    message.warning(t("lowcode.appBuilder.warnVarJson"));
    return;
  }

  try {
    if (environmentFormMode.value === "create") {
      await createLowCodeEnvironment(appId.value, {
        name: environmentForm.name,
        code: environmentForm.code,
        description: environmentForm.description || undefined,
        isDefault: environmentForm.isDefault,
        variablesJson: environmentForm.variablesJson
      });

      if (!isMounted.value) return;
      message.success(t("lowcode.appBuilder.envCreateOk"));
    } else if (editingEnvironmentId.value) {
      await updateLowCodeEnvironment(editingEnvironmentId.value, {
        name: environmentForm.name,
        description: environmentForm.description || undefined,
        isDefault: environmentForm.isDefault,
        isActive: environmentForm.isActive,
        variablesJson: environmentForm.variablesJson
      });

      if (!isMounted.value) return;
      message.success(t("lowcode.appBuilder.envUpdateOk"));
    }

    environmentFormVisible.value = false;
    environments.value = await getLowCodeEnvironments(appId.value);

    if (!isMounted.value) return;
    if (!selectedEnvironmentCode.value || !environments.value.some(item => item.code === selectedEnvironmentCode.value)) {
      selectedEnvironmentCode.value = environments.value.find(item => item.isDefault)?.code;
    }
    if (selectedPageId.value) {
      dropPageSchema(selectedPageId.value);
      await selectPage(selectedPageId.value);

      if (!isMounted.value) return;
    }
  } catch (error) {
    message.error((error as Error).message || t("lowcode.appBuilder.envSaveFailed"));
  }
};

const handleDeleteEnvironment = (id: string) => {
  Modal.confirm({
    title: t("lowcode.appBuilder.deleteEnvTitle"),
    content: t("lowcode.appBuilder.deleteEnvContent"),
    okText: t("lowcode.appBuilder.envDelete"),
    cancelText: t("common.cancel"),
    onOk: async () => {
      await deleteLowCodeEnvironment(id);

      if (!isMounted.value) return;
      message.success(t("lowcode.appBuilder.envDeleted"));
      environments.value = await getLowCodeEnvironments(appId.value);

      if (!isMounted.value) return;
      if (selectedEnvironmentCode.value && !environments.value.some(item => item.code === selectedEnvironmentCode.value)) {
        selectedEnvironmentCode.value = environments.value.find(item => item.isDefault)?.code;
      }
      if (selectedPageId.value) {
        dropPageSchema(selectedPageId.value);
        await selectPage(selectedPageId.value);

        if (!isMounted.value) return;
      }
    }
  });
};

const handleDeletePage = async (pageId: string) => {
  try {
    await deleteLowCodePage(pageId);

    if (!isMounted.value) return;
    if (selectedPageId.value === pageId) {
      selectedPageId.value = null;
    }
    dropPageSchema(pageId);
    message.success(t("lowcodeBuilder.deleteSuccess"));
    await loadApp();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.deleteFailed"));
  }
};

const loadAppVersions = async () => {
  appVersionLoading.value = true;
  try {
    const result  = await getLowCodeAppVersionsPaged(appId.value, {
      pageIndex: appVersionPageIndex.value,
      pageSize: appVersionPageSize.value
    });

    if (!isMounted.value) return;
    appVersionItems.value = result.items;
    appVersionTotal.value = result.total;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.loadVersionFailed"));
  } finally {
    appVersionLoading.value = false;
  }
};

const openAppVersionDrawer = async () => {
  appVersionDrawerVisible.value = true;
  appVersionPageIndex.value = 1;
  await loadAppVersions();

  if (!isMounted.value) return;
};

const onAppVersionPageChange = (page: number, pageSizeValue: number) => {
  appVersionPageIndex.value = page;
  appVersionPageSize.value = pageSizeValue;
  loadAppVersions();
};

const handleRollbackAppVersion = async (versionId: string) => {
  appRollbackingVersionId.value = versionId;
  try {
    const newVersion  = await rollbackLowCodeAppVersion(appId.value, versionId);

    if (!isMounted.value) return;
    message.success(t("lowcodeBuilder.rollbackSuccess", { version: newVersion }));
    await Promise.all([loadApp(), loadAppVersions()]);

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.rollbackFailed"));
  } finally {
    appRollbackingVersionId.value = null;
  }
};

const formatTime = (time: string) => {
  try {
    const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
    return new Date(time).toLocaleString(loc);
  } catch {
    return time;
  }
};

const goBack = () => {
  router.push("/console");
};

onMounted(() => {
  isMounted.value = true;
  document.addEventListener("keydown", handleKeyDown);
  loadApp();
});
</script>

<style scoped>
.app-builder-page {
  display: flex;
  height: 100vh;
  overflow: hidden;
}

.builder-sidebar {
  width: 260px;
  background: #fff;
  border-right: 1px solid #e8e8e8;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
}

.sidebar-header {
  padding: 12px 16px;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.sidebar-header h3 {
  margin: 0;
  font-size: 14px;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sidebar-actions {
  padding: 8px 16px;
}

.page-tree {
  flex: 1;
  overflow-y: auto;
  padding: 4px 8px;
}

.page-tree-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.2s;
}

.page-tree-item:hover {
  background: #f5f5f5;
}

.page-tree-item.active {
  background: #e6f7ff;
  color: #1890ff;
}

.page-icon {
  width: 24px;
  height: 24px;
  border-radius: 4px;
  background: #f0f0f0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 600;
  color: #666;
  flex-shrink: 0;
}

.page-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.empty-hint {
  padding: 24px;
  text-align: center;
  color: #999;
  font-size: 13px;
}

.builder-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.main-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  background: #fff;
  border-bottom: 1px solid #e8e8e8;
  height: 48px;
}

.main-toolbar-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-title {
  font-size: 14px;
  font-weight: 500;
}

.main-toolbar-actions {
  display: flex;
  gap: 8px;
}

.empty-main {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #999;
  font-size: 16px;
}
</style>
