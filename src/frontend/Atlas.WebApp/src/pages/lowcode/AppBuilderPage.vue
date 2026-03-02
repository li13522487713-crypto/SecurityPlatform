<template>
  <div class="app-builder-page">
    <!-- 左侧页面树 -->
    <div class="builder-sidebar">
      <div class="sidebar-header">
        <a-button size="small" @click="goBack">返回</a-button>
        <h3>{{ appDetail?.name ?? "加载中..." }}</h3>
      </div>
      <div class="sidebar-actions">
        <a-button type="primary" size="small" block @click="handleAddPage">新建页面</a-button>
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
          <span class="page-name">{{ page.name }}</span>
          <a-tag v-if="page.isPublished" color="green" size="small">已发布</a-tag>
          <a-dropdown trigger="click" @click.stop>
            <a-button type="text" size="small">...</a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="edit" @click="handleEditPage(page)">编辑信息</a-menu-item>
                <a-menu-item v-if="!page.isPublished" key="publish" @click="handlePublishPage(page.id)">发布</a-menu-item>
                <a-menu-item key="delete" danger @click="handleDeletePage(page.id)">删除</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
        <div v-if="pages.length === 0 && !loading" class="empty-hint">
          暂无页面，点击上方"新建页面"按钮创建
        </div>
      </div>
    </div>

    <!-- 右侧设计器区域 -->
    <div class="builder-main">
      <template v-if="selectedPageId && currentSchema">
        <div class="main-toolbar">
          <span class="page-title">{{ currentPageName }}</span>
          <div class="main-toolbar-actions">
            <a-button @click="handleSavePageSchema" :loading="saving">保存</a-button>
            <a-button type="primary" @click="handlePublishPage(selectedPageId!)" :loading="publishing">发布</a-button>
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
          <p>请从左侧选择或创建一个页面</p>
        </div>
      </template>
    </div>

    <!-- 新建/编辑页面对话框 -->
    <a-modal
      v-model:open="pageFormVisible"
      :title="pageFormMode === 'create' ? '新建页面' : '编辑页面'"
      ok-text="确定"
      cancel-text="取消"
      @ok="handlePageFormSubmit"
    >
      <a-form layout="vertical">
        <a-form-item v-if="pageFormMode === 'create'" label="页面标识" required>
          <a-input v-model:value="pageForm.pageKey" placeholder="如 customer-list" />
        </a-form-item>
        <a-form-item label="页面名称" required>
          <a-input v-model:value="pageForm.name" placeholder="请输入页面名称" />
        </a-form-item>
        <a-form-item label="页面类型" required>
          <a-select v-model:value="pageForm.pageType">
            <a-select-option value="List">列表页 (CRUD)</a-select-option>
            <a-select-option value="Form">表单页</a-select-option>
            <a-select-option value="Detail">详情页</a-select-option>
            <a-select-option value="Dashboard">仪表盘</a-select-option>
            <a-select-option value="Blank">空白页</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="路由路径">
          <a-input v-model:value="pageForm.routePath" placeholder="如 /app/crm/customers" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="pageForm.description" :rows="2" />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="pageForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import AmisEditor from "@/components/amis/AmisEditor.vue";
import type { LowCodeAppDetail, LowCodePageListItem } from "@/types/lowcode";
import {
  getLowCodeAppDetail,
  createLowCodePage,
  updateLowCodePage,
  updateLowCodePageSchema,
  publishLowCodePage,
  deleteLowCodePage
} from "@/services/lowcode";

const route = useRoute();
const router = useRouter();
const appId = route.params.id as string;

const loading = ref(true);
const saving = ref(false);
const publishing = ref(false);
const appDetail = ref<LowCodeAppDetail | null>(null);
const pages = ref<LowCodePageListItem[]>([]);
const selectedPageId = ref<string | null>(null);
const pageEditorRef = ref<InstanceType<typeof AmisEditor> | null>(null);

// Page schemas cache
const pageSchemas = ref<Record<string, Record<string, unknown>>>({});

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

const generateDefaultSchema = (pageType: string, pageName: string): Record<string, unknown> => {
  switch (pageType) {
    case "List":
      return {
        type: "page",
        title: pageName,
        body: [{
          type: "crud",
          api: "/api/v1/data",
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
          api: "/api/v1/data",
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

const loadApp = async () => {
  loading.value = true;
  try {
    const detail = await getLowCodeAppDetail(appId);
    appDetail.value = detail;
    pages.value = detail.pages;
  } catch (error) {
    message.error((error as Error).message || "加载应用失败");
  } finally {
    loading.value = false;
  }
};

const selectPage = async (pageId: string) => {
  selectedPageId.value = pageId;

  if (!pageSchemas.value[pageId]) {
    // Find page in pages list; schema is loaded from app detail
    const page = pages.value.find(p => p.id === pageId);
    if (page) {
      // Load full detail from app (schema is not in list item, need to reload)
      try {
        const detail = await getLowCodeAppDetail(appId);
        appDetail.value = detail;
        pages.value = detail.pages;
        // For now, set a default schema based on page type
        pageSchemas.value[pageId] = generateDefaultSchema(page.pageType, page.name);
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
    message.warning("请输入页面名称");
    return;
  }

  try {
    if (pageFormMode.value === "create") {
      if (!pageForm.pageKey.trim()) {
        message.warning("请输入页面标识");
        return;
      }
      const schema = generateDefaultSchema(pageForm.pageType, pageForm.name);
      await createLowCodePage(appId, {
        pageKey: pageForm.pageKey,
        name: pageForm.name,
        pageType: pageForm.pageType,
        schemaJson: JSON.stringify(schema),
        routePath: pageForm.routePath || undefined,
        description: pageForm.description || undefined,
        sortOrder: pageForm.sortOrder
      });
      message.success("页面创建成功");
    } else if (editingPageId.value) {
      const currentSchema = pageSchemas.value[editingPageId.value]
        ?? generateDefaultSchema(pageForm.pageType, pageForm.name);
      await updateLowCodePage(editingPageId.value, {
        name: pageForm.name,
        pageType: pageForm.pageType,
        schemaJson: JSON.stringify(currentSchema),
        routePath: pageForm.routePath || undefined,
        description: pageForm.description || undefined,
        sortOrder: pageForm.sortOrder
      });
      message.success("页面更新成功");
    }
    pageFormVisible.value = false;
    await loadApp();
  } catch (error) {
    message.error((error as Error).message || "操作失败");
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
      message.success("保存成功");
    }
  } catch (error) {
    message.error((error as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
};

const handlePublishPage = async (pageId: string) => {
  publishing.value = true;
  try {
    await publishLowCodePage(pageId);
    message.success("发布成功");
    await loadApp();
  } catch (error) {
    message.error((error as Error).message || "发布失败");
  } finally {
    publishing.value = false;
  }
};

const handleDeletePage = async (pageId: string) => {
  try {
    await deleteLowCodePage(pageId);
    if (selectedPageId.value === pageId) {
      selectedPageId.value = null;
    }
    delete pageSchemas.value[pageId];
    message.success("已删除");
    await loadApp();
  } catch (error) {
    message.error((error as Error).message || "删除失败");
  }
};

const goBack = () => {
  router.push({ name: "app-list" });
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
