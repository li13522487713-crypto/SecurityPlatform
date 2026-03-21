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
          <a-tag v-if="page.isPublished" color="green" size="small">已发布</a-tag>
          <a-dropdown trigger="click" @click.stop>
            <a-button type="text" size="small">...</a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="edit" @click="handleEditPage(page)">编辑信息</a-menu-item>
                <a-menu-item key="versions" @click="handleOpenVersionHistory(page)">版本历史</a-menu-item>
                <a-menu-item v-if="!page.isPublished" key="publish" @click="handlePublishPage(page.id)">发布</a-menu-item>
                <a-menu-item key="delete" danger @click="handleDeletePage(page.id)">删除</a-menu-item>
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
          <span class="page-title">{{ currentPageName }}</span>
          <div class="main-toolbar-actions">
            <a-select
              v-model:value="selectedEnvironmentCode"
              style="width: 180px"
              :options="environmentOptions"
              allow-clear
              placeholder="预览环境"
              @change="handleEnvironmentChange"
            />
            <a-button @click="openEnvironmentManager">环境管理</a-button>
            <a-button @click="handleSaveAsTemplate">保存为模板</a-button>
            <a-button :loading="saving" @click="handleSavePageSchema">保存</a-button>
            <a-button type="primary" :loading="publishing" @click="handlePublishPage(selectedPageId!)">发布</a-button>
          </div>
        </div>
        <AmisEditor
          ref="pageEditorRef"
          :schema="currentSchema"
          height="calc(100vh - 112px)"
          @change="handlePageSchemaChange"
        />
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
      title="页面版本历史"
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
        <a-table-column key="snapshotVersion" title="版本号" data-index="snapshotVersion" width="120px" />
        <a-table-column key="createdAt" title="发布时间" data-index="createdAt" width="220px" />
        <a-table-column key="createdBy" title="发布人" data-index="createdBy" width="120px" />
        <a-table-column key="action" title="操作" width="140px">
          <template #default="{ record }">
            <a-button size="small" @click="handleRollbackPageVersion(record.id)">回滚到此版本</a-button>
          </template>
        </a-table-column>
      </a-table>
    </a-modal>

    <a-modal
      v-model:open="environmentModalVisible"
      title="环境管理"
      :footer="null"
      width="760px"
    >
      <div style="margin-bottom: 12px;">
        <a-button type="primary" @click="openEnvironmentForm('create')">新建环境</a-button>
      </div>
      <a-table
        :data-source="environments"
        :loading="environmentLoading"
        :pagination="false"
        row-key="id"
        size="small"
      >
        <a-table-column key="name" title="名称" data-index="name" width="160px" />
        <a-table-column key="code" title="编码" data-index="code" width="120px" />
        <a-table-column key="isDefault" title="默认" width="80px">
          <template #default="{ record }">
            <a-tag v-if="record.isDefault" color="green">默认</a-tag>
            <span v-else>-</span>
          </template>
        </a-table-column>
        <a-table-column key="isActive" title="状态" width="80px">
          <template #default="{ record }">
            <a-tag :color="record.isActive ? 'blue' : 'default'">{{ record.isActive ? "启用" : "停用" }}</a-tag>
          </template>
        </a-table-column>
        <a-table-column key="description" title="描述" data-index="description" />
        <a-table-column key="action" title="操作" width="180px">
          <template #default="{ record }">
            <a-space size="small">
              <a-button type="link" size="small" @click="openEnvironmentForm('edit', record)">编辑</a-button>
              <a-button type="link" size="small" danger @click="handleDeleteEnvironment(record.id)">删除</a-button>
            </a-space>
          </template>
        </a-table-column>
      </a-table>
    </a-modal>

    <a-modal
      v-model:open="environmentFormVisible"
      :title="environmentFormMode === 'create' ? '新建环境' : '编辑环境'"
      ok-text="确定"
      cancel-text="取消"
      @ok="submitEnvironmentForm"
    >
      <a-form layout="vertical">
        <a-form-item label="环境名称" required>
          <a-input v-model:value="environmentForm.name" />
        </a-form-item>
        <a-form-item label="环境编码" required>
          <a-input v-model:value="environmentForm.code" :disabled="environmentFormMode === 'edit'" />
        </a-form-item>
        <a-form-item label="变量 JSON" required>
          <a-textarea v-model:value="environmentForm.variablesJson" :rows="6" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="environmentForm.description" :rows="2" />
        </a-form-item>
        <a-form-item label="状态">
          <a-switch v-model:checked="environmentForm.isActive" />
        </a-form-item>
        <a-form-item label="默认环境">
          <a-switch v-model:checked="environmentForm.isDefault" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message, Modal } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import AmisEditor from "@/components/amis/AmisEditor.vue";
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
const { t } = useI18n();
const appId = computed(() => String(route.params.id ?? route.params.appId ?? ""));

const loading = ref(true);
const saving = ref(false);
const publishing = ref(false);
const appDetail = ref<LowCodeAppDetail | null>(null);
const pages = ref<LowCodePageListItem[]>([]);
const selectedPageId = ref<string | null>(null);
const pageEditorRef = ref<InstanceType<typeof AmisEditor> | null>(null);
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
const pageSchemas = ref<Record<string, Record<string, unknown>>>({});
const pageDepthMap = ref<Record<string, number>>({});

const currentSchema = computed(() => {
  if (!selectedPageId.value) return null;
  return pageSchemas.value[selectedPageId.value] ?? null;
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
            { name: "name", label: "名称" }
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
            { type: "input-text", name: "name", label: "名称", required: true }
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
            { body: [{ type: "card", header: { title: "统计卡片" }, body: "数据加载中..." }] },
            { body: [{ type: "card", header: { title: "统计卡片" }, body: "数据加载中..." }] }
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
        pageSchemas.value[pageId] = parseSchemaJson(runtime.schemaJson)
          ?? generateDefaultSchema(detail.pageType, detail.name);
      } catch {
        pageSchemas.value[pageId] = { type: "page", title: page.name, body: [] };
      }
    }
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
        pageSchemas.value[editingPageId.value] = currentSchema;
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
    pageSchemas.value[selectedPageId.value] = newSchema;
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
    message.warning("请先选择页面");
    return;
  }
  const page = pages.value.find((item) => item.id === selectedPageId.value);
  if (!page) {
    message.warning("页面不存在");
    return;
  }
  const schemaToSave = pageEditorRef.value?.getSchema() ?? pageSchemas.value[selectedPageId.value];
  if (!schemaToSave) {
    message.warning("当前页面没有可保存的 Schema");
    return;
  }

  try {
    await createTemplate({
      name: `${appDetail.value?.name ?? "应用"}-${page.name}-模板`,
      category: resolveTemplateCategory(page.pageType),
      schemaJson: JSON.stringify(schemaToSave),
      description: `由应用 ${appDetail.value?.name ?? ""} 的页面 ${page.name} 保存`,
      tags: `lowcode,page,${page.pageType.toLowerCase()}`,
      version: "1.0.0"
    });

    if (!isMounted.value) return;
    message.success("模板已保存到模板市场");
  } catch (error) {
    message.error((error as Error).message || "保存模板失败");
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
    message.error((error as Error).message || "加载版本历史失败");
  } finally {
    pageVersionLoading.value = false;
  }
};

const handleRollbackPageVersion = async (versionId: string) => {
  if (!versionTargetPageId.value) {
    return;
  }

  Modal.confirm({
    title: "确认回滚",
    content: "回滚后将以历史版本生成新的已发布版本，是否继续？",
    okText: "确认回滚",
    cancelText: "取消",
    onOk: async () => {
      await rollbackLowCodePage(versionTargetPageId.value!, versionId);

      if (!isMounted.value) return;
      message.success("回滚成功");
      versionModalVisible.value = false;
      pageSchemas.value = {};
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
  delete pageSchemas.value[selectedPageId.value];
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
    message.error((error as Error).message || "加载环境失败");
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
      message.error((error as Error).message || "加载环境详情失败");
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
    message.warning("请填写环境名称和编码");
    return;
  }

  try {
    JSON.parse(environmentForm.variablesJson);
  } catch {
    message.warning("变量 JSON 格式不正确");
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
      message.success("环境创建成功");
    } else if (editingEnvironmentId.value) {
      await updateLowCodeEnvironment(editingEnvironmentId.value, {
        name: environmentForm.name,
        description: environmentForm.description || undefined,
        isDefault: environmentForm.isDefault,
        isActive: environmentForm.isActive,
        variablesJson: environmentForm.variablesJson
      });

      if (!isMounted.value) return;
      message.success("环境更新成功");
    }

    environmentFormVisible.value = false;
    environments.value = await getLowCodeEnvironments(appId.value);

    if (!isMounted.value) return;
    if (!selectedEnvironmentCode.value || !environments.value.some(item => item.code === selectedEnvironmentCode.value)) {
      selectedEnvironmentCode.value = environments.value.find(item => item.isDefault)?.code;
    }
    if (selectedPageId.value) {
      delete pageSchemas.value[selectedPageId.value];
      await selectPage(selectedPageId.value);

      if (!isMounted.value) return;
    }
  } catch (error) {
    message.error((error as Error).message || "环境保存失败");
  }
};

const handleDeleteEnvironment = (id: string) => {
  Modal.confirm({
    title: "确认删除环境",
    content: "删除后无法恢复，是否继续？",
    okText: "删除",
    cancelText: "取消",
    onOk: async () => {
      await deleteLowCodeEnvironment(id);

      if (!isMounted.value) return;
      message.success("环境已删除");
      environments.value = await getLowCodeEnvironments(appId.value);

      if (!isMounted.value) return;
      if (selectedEnvironmentCode.value && !environments.value.some(item => item.code === selectedEnvironmentCode.value)) {
        selectedEnvironmentCode.value = environments.value.find(item => item.isDefault)?.code;
      }
      if (selectedPageId.value) {
        delete pageSchemas.value[selectedPageId.value];
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
    delete pageSchemas.value[pageId];
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
    return new Date(time).toLocaleString("zh-CN");
  } catch {
    return time;
  }
};

const goBack = () => {
  router.push("/console");
};

onMounted(() => {
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
