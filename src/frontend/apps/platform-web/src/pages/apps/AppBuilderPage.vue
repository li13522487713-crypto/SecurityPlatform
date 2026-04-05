<template>
  <div class="builder-page">
    <div class="builder-sidebar">
      <div class="sidebar-header">
        <a-button type="text" @click="goBack">{{ t("lowcodeBuilder.back") }}</a-button>
        <h3>{{ appDetail?.name || t("lowcodeBuilder.loading") }}</h3>
      </div>
      <div class="sidebar-actions">
        <a-button type="primary" block size="small" @click="openCreatePage">
          {{ t("lowcodeBuilder.createPage") }}
        </a-button>
      </div>
      <div class="page-list">
        <a-spin :spinning="loadingPages">
          <div
            v-for="item in flatPages"
            :key="item.id"
            class="page-item"
            :class="{ active: selectedPageId === item.id }"
            :style="{ paddingLeft: `${item.level * 12 + 8}px` }"
            @click="selectPage(item.id)"
          >
            <span class="page-name">{{ item.name }}</span>
            <a-tag v-if="item.isPublished" color="green" size="small">{{ t("lowcodeBuilder.published") }}</a-tag>
            <a-dropdown trigger="click" @click.stop>
              <a-button type="text" size="small">...</a-button>
              <template #overlay>
                <a-menu>
                  <a-menu-item @click="openEditPage(item.id)">{{ t("common.edit") }}</a-menu-item>
                  <a-menu-item @click="publishPage(item.id)">{{ t("lowcodeBuilder.publish") }}</a-menu-item>
                  <a-menu-item danger @click="removePage(item.id)">{{ t("common.delete") }}</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>
          </div>
          <a-empty v-if="flatPages.length === 0 && !loadingPages" :description="t('lowcodeBuilder.pageListEmpty')" />
        </a-spin>
      </div>
    </div>

    <div class="builder-main">
      <template v-if="selectedPageId && currentSchema">
        <div class="main-toolbar">
          <a-space>
            <a-button size="small" :disabled="!schemaHistory.canUndo" @click="handleUndo">
              {{ t("lowcodeBuilder.undo") }}
            </a-button>
            <a-button size="small" :disabled="!schemaHistory.canRedo" @click="handleRedo">
              {{ t("lowcodeBuilder.redo") }}
            </a-button>
          </a-space>
          <a-space>
            <a-button size="small" @click="saveAsTemplate">{{ t("lowcodeBuilder.saveTemplate") }}</a-button>
            <a-button size="small" :loading="saving" @click="saveSchema">{{ t("lowcodeBuilder.save") }}</a-button>
            <a-button type="primary" size="small" :loading="publishing" @click="publishPage(selectedPageId)">
              {{ t("lowcodeBuilder.publish") }}
            </a-button>
          </a-space>
        </div>
        <AmisEditor
          ref="editorRef"
          :schema="currentSchema"
          :schema-revision="schemaRevision"
          height="calc(100vh - 110px)"
          @change="handleSchemaChange"
          @save="handleSchemaChange"
        />
      </template>
      <a-empty v-else :description="t('lowcodeBuilder.selectPageHint')" />
    </div>

    <a-modal
      v-model:open="pageFormVisible"
      :title="pageFormMode === 'create' ? t('lowcodeBuilder.pageFormCreate') : t('lowcodeBuilder.pageFormEdit')"
      @ok="submitPageForm"
    >
      <a-form layout="vertical">
        <a-form-item v-if="pageFormMode === 'create'" :label="t('lowcodeBuilder.pageKey')" required>
          <a-input v-model:value="pageForm.pageKey" />
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.pageName')" required>
          <a-input v-model:value="pageForm.name" />
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.pageType')" required>
          <a-select v-model:value="pageForm.pageType">
            <a-select-option value="List">{{ t("lowcodeBuilder.pageTypeList") }}</a-select-option>
            <a-select-option value="Form">{{ t("lowcodeBuilder.pageTypeForm") }}</a-select-option>
            <a-select-option value="Detail">{{ t("lowcodeBuilder.pageTypeDetail") }}</a-select-option>
            <a-select-option value="Dashboard">{{ t("lowcodeBuilder.pageTypeDashboard") }}</a-select-option>
            <a-select-option value="Blank">{{ t("lowcodeBuilder.pageTypeBlank") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.routePath')">
          <a-input v-model:value="pageForm.routePath" />
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.description')">
          <a-textarea v-model:value="pageForm.description" :rows="2" />
        </a-form-item>
        <a-form-item :label="t('lowcodeBuilder.sortOrder')">
          <a-input-number v-model:value="pageForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message, Modal } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useSchemaHistoryStore } from "@/stores/schemaHistory";
import type { LowCodeAppDetail } from "@/types/platform-console";
import type { LowCodePageCreateRequest, LowCodePageTreeNode, LowCodePageUpdateRequest } from "@/types/lowcode";
import {
  createLowCodePage,
  deleteLowCodePage,
  getLowCodeAppDetail,
  getLowCodePageDetail,
  getLowCodePageTree,
  getLowCodeRuntimePageSchema,
  publishLowCodePage,
  updateLowCodePage,
  updateLowCodePageSchema,
} from "@/services/api-lowcode";
import { createTemplate } from "@/services/templates";

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const schemaHistory = useSchemaHistoryStore();
const AmisEditor = defineAsyncComponent(() => import("@/components/amis/AmisEditor.vue"));

type SchemaValue = Record<string, object | string | number | boolean | null>;

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const appDetail = ref<LowCodeAppDetail | null>(null);
const pageTree = ref<LowCodePageTreeNode[]>([]);
const loadingPages = ref(false);
const selectedPageId = ref("");
const currentSchema = ref<SchemaValue | null>(null);
const schemaRevision = ref(0);
const saving = ref(false);
const publishing = ref(false);
const editorRef = ref<{ getSchema: () => SchemaValue } | null>(null);

const pageFormVisible = ref(false);
const pageFormMode = ref<"create" | "edit">("create");
const editingPageId = ref("");
const pageForm = reactive({
  pageKey: "",
  name: "",
  pageType: "List",
  routePath: "",
  description: "",
  sortOrder: 0,
});

const flattenTree = (nodes: LowCodePageTreeNode[], level: number): Array<LowCodePageTreeNode & { level: number }> =>
  nodes.flatMap((node) => [
    { ...node, level },
    ...(node.children ? flattenTree(node.children, level + 1) : []),
  ]);

const flatPages = computed(() => flattenTree(pageTree.value, 0));

const loadApp = async () => {
  if (!appId.value) {
    return;
  }
  try {
    appDetail.value = await getLowCodeAppDetail(appId.value);
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.loadFailed"));
  }
};

const loadPages = async () => {
  if (!appId.value) {
    return;
  }
  loadingPages.value = true;
  try {
    pageTree.value = await getLowCodePageTree(appId.value);
    if (!selectedPageId.value && flatPages.value.length > 0) {
      await selectPage(flatPages.value[0].id);
    }
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.loadFailed"));
  } finally {
    loadingPages.value = false;
  }
};

const selectPage = async (pageId: string) => {
  selectedPageId.value = pageId;
  try {
    const runtime = await getLowCodeRuntimePageSchema(pageId, "draft");
    currentSchema.value = runtime.schema;
    schemaHistory.init(runtime.schema);
    schemaRevision.value += 1;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.loadFailed"));
  }
};

const handleSchemaChange = (schema: SchemaValue) => {
  currentSchema.value = schema;
  schemaHistory.pushState(schema);
};

const handleUndo = () => {
  const schema = schemaHistory.undo();
  if (!schema) {
    return;
  }
  currentSchema.value = schema;
  schemaRevision.value += 1;
};

const handleRedo = () => {
  const schema = schemaHistory.redo();
  if (!schema) {
    return;
  }
  currentSchema.value = schema;
  schemaRevision.value += 1;
};

const saveSchema = async () => {
  if (!selectedPageId.value) {
    return;
  }
  const schema = editorRef.value?.getSchema() || currentSchema.value;
  if (!schema) {
    return;
  }
  saving.value = true;
  try {
    await updateLowCodePageSchema(selectedPageId.value, JSON.stringify(schema));
    message.success(t("lowcodeBuilder.saveSuccess"));
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const publishPage = async (pageId: string) => {
  publishing.value = true;
  try {
    await publishLowCodePage(pageId);
    message.success(t("lowcodeBuilder.publishSuccess"));
    await loadPages();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.publishFailed"));
  } finally {
    publishing.value = false;
  }
};

const resetPageForm = () => {
  pageForm.pageKey = "";
  pageForm.name = "";
  pageForm.pageType = "List";
  pageForm.routePath = "";
  pageForm.description = "";
  pageForm.sortOrder = 0;
};

const openCreatePage = () => {
  pageFormMode.value = "create";
  editingPageId.value = "";
  resetPageForm();
  pageFormVisible.value = true;
};

const openEditPage = async (pageId: string) => {
  pageFormMode.value = "edit";
  editingPageId.value = pageId;
  try {
    const detail = await getLowCodePageDetail(pageId);
    pageForm.pageKey = detail.pageKey;
    pageForm.name = detail.name;
    pageForm.pageType = detail.pageType;
    pageForm.routePath = detail.routePath || "";
    pageForm.description = detail.description || "";
    pageForm.sortOrder = detail.sortOrder;
    pageFormVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.loadFailed"));
  }
};

const submitPageForm = async () => {
  if (!pageForm.name.trim() || (pageFormMode.value === "create" && !pageForm.pageKey.trim())) {
    message.warning(t("validation.required"));
    return;
  }
  try {
    if (pageFormMode.value === "create") {
      const request: LowCodePageCreateRequest = {
        pageKey: pageForm.pageKey.trim(),
        name: pageForm.name.trim(),
        pageType: pageForm.pageType,
        routePath: pageForm.routePath.trim() || undefined,
        description: pageForm.description.trim() || undefined,
        sortOrder: pageForm.sortOrder,
      };
      await createLowCodePage(appId.value, request);
    } else {
      const request: LowCodePageUpdateRequest = {
        name: pageForm.name.trim(),
        pageType: pageForm.pageType,
        routePath: pageForm.routePath.trim() || undefined,
        description: pageForm.description.trim() || undefined,
        sortOrder: pageForm.sortOrder,
      };
      await updateLowCodePage(editingPageId.value, request);
    }
    pageFormVisible.value = false;
    await loadPages();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.saveFailed"));
  }
};

const removePage = async (pageId: string) => {
  Modal.confirm({
    title: t("lowcodeBuilder.deleteConfirm"),
    onOk: async () => {
      await deleteLowCodePage(pageId);
      if (selectedPageId.value === pageId) {
        selectedPageId.value = "";
        currentSchema.value = null;
      }
      await loadPages();
    },
  });
};

const saveAsTemplate = async () => {
  if (!currentSchema.value || !selectedPageId.value) {
    return;
  }
  try {
    await createTemplate({
      name: `${appDetail.value?.name || "App"}-${selectedPageId.value}`,
      category: 1,
      schemaJson: JSON.stringify(currentSchema.value),
      description: "",
      tags: "lowcode",
      version: "1.0.0",
    });
    message.success(t("lowcodeBuilder.templateSuccess"));
  } catch (error) {
    message.error((error as Error).message || t("lowcodeBuilder.templateFailed"));
  }
};

const goBack = () => {
  router.back();
};

onMounted(async () => {
  await loadApp();
  await loadPages();
});
</script>

<style scoped>
.builder-page {
  display: flex;
  height: calc(100vh - 64px);
  background: #fff;
}

.builder-sidebar {
  width: 320px;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
}

.sidebar-header {
  padding: 12px;
  border-bottom: 1px solid #f0f0f0;
}

.sidebar-header h3 {
  margin: 8px 0 0;
  font-size: 15px;
}

.sidebar-actions {
  padding: 12px;
  border-bottom: 1px solid #f0f0f0;
}

.page-list {
  flex: 1;
  overflow: auto;
  padding: 8px 0;
}

.page-item {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 36px;
  cursor: pointer;
}

.page-item.active {
  background: #e6f4ff;
}

.page-name {
  flex: 1;
}

.builder-main {
  flex: 1;
  padding: 12px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.main-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
</style>
