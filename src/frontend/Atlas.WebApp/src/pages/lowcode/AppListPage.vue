<template>
  <div class="app-list-page">
    <div class="page-header">
      <div class="page-header-left">
        <h2>{{ t("lowcodeApp.listTitle") }}</h2>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('lowcodeApp.searchPlaceholder')"
          allow-clear
          style="width: 260px"
          @search="handleSearch"
        />
      </div>
      <div class="page-header-right">
        <a-button v-if="canManageApps" @click="handleImportClick">导入应用</a-button>
        <a-button v-if="canManageApps" @click="handleCreateFromTemplate">从模板创建</a-button>
        <a-button v-if="canManageApps" type="primary" @click="handleCreate">新建应用</a-button>
      </div>
    </div>

    <!-- 应用卡片网格 -->
    <div v-if="!loading" class="app-grid">
      <div
        v-for="app in dataSource"
        :key="app.id"
        class="app-card"
        @click="handleOpenApp(app.id)"
      >
        <div class="app-card-icon">
          {{ app.icon || app.name.slice(0, 1) }}
        </div>
        <div class="app-card-content">
          <div class="app-card-name">{{ app.name }}</div>
          <div class="app-card-key">{{ app.appKey }}</div>
          <div class="app-card-desc">{{ app.description || t("lowcodeApp.noDescription") }}</div>
        </div>
        <div class="app-card-footer">
          <a-tag :color="statusColor(app.status)">
            {{ statusLabel(app.status) }}
          </a-tag>
          <span class="app-card-version">{{ t("lowcodeApp.versionPrefix", { version: app.version }) }}</span>
        </div>
        <div class="app-card-actions" @click.stop>
          <a-dropdown v-if="canManageApps" trigger="click">
            <a-button type="text" size="small">...</a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="edit" @click="handleEdit(app)">编辑</a-menu-item>
                <a-menu-item key="export" @click="handleExport(app)">导出</a-menu-item>
                <a-menu-item v-if="app.status === 'Draft'" key="publish" @click="handlePublish(app.id)">发布</a-menu-item>
                <a-menu-item key="delete" danger @click="handleDelete(app.id)">删除</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
      </div>

      <!-- 新建应用占位卡片 -->
      <div v-if="canManageApps" class="app-card app-card-new" @click="handleCreate">
        <div class="app-card-new-icon">+</div>
        <div class="app-card-new-text">{{ t("lowcodeApp.newCardText") }}</div>
      </div>
    </div>

    <div v-else class="loading-container">
      <a-spin size="large" :tip="t('lowcodeApp.loadingTip')" />
    </div>

    <!-- 新建应用向导 -->
    <AppCreateWizard
      v-model:open="createWizardVisible"
      @created="handleCreated"
    />

    <!-- 编辑应用对话框 -->
    <a-modal
      v-model:open="editVisible"
      :title="t('lowcodeApp.editModalTitle')"
      :ok-text="t('common.confirm')"
      :cancel-text="t('common.cancel')"
      @ok="handleEditSubmit"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('lowcodeApp.appNameLabel')" required>
          <a-input v-model:value="editModel.name" :placeholder="t('lowcodeApp.appNamePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.categoryLabel')">
          <a-select v-model:value="editModel.category" :placeholder="t('lowcodeApp.categoryPlaceholder')" allow-clear>
            <a-select-option value="OA">{{ t("lowcodeApp.categoryOA") }}</a-select-option>
            <a-select-option value="CRM">{{ t("lowcodeApp.categoryCRM") }}</a-select-option>
            <a-select-option value="ERP">{{ t("lowcodeApp.categoryERP") }}</a-select-option>
            <a-select-option value="HR">{{ t("lowcodeApp.categoryHR") }}</a-select-option>
            <a-select-option value="通用">{{ t("lowcodeApp.categoryGeneral") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.descriptionLabel')">
          <a-textarea v-model:value="editModel.description" :rows="3" :placeholder="t('lowcodeApp.descriptionPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.iconLabel')">
          <a-input v-model:value="editModel.icon" :placeholder="t('lowcodeApp.iconPlaceholder')" />
        </a-form-item>
      </a-form>
    </a-modal>

    <input
      ref="importInputRef"
      type="file"
      accept="application/json,.json"
      style="display: none"
      @change="handleImportFileChange"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { TenantAppInstanceListItem } from "@/types/platform-v2";
import { getAuthProfile, hasPermission } from "@/utils/auth";
import {
  getTenantAppInstancesPaged,
  updateTenantAppInstance,
  exportTenantAppInstance,
  importTenantAppInstance,
  publishTenantAppInstance,
  deleteTenantAppInstance
} from "@/services/api-tenant-app-instances";
import {
  parseLowCodeAppExportPackage
} from "@/services/lowcode";
import AppCreateWizard from "@/pages/console/components/AppCreateWizard.vue";

const router = useRouter();
const { t } = useI18n();
const canManageApps = hasPermission(getAuthProfile(), "apps:update");

const keyword = ref("");
const loading = ref(false);
const dataSource = ref<TenantAppInstanceListItem[]>([]);
const importing = ref(false);
const importInputRef = ref<HTMLInputElement | null>(null);

const createWizardVisible = ref(false);
const editVisible = ref(false);
const selectedId = ref<string | null>(null);
const editModel = reactive({
  name: "",
  description: "",
  category: undefined as string | undefined,
  icon: ""
});

const statusColor = (status: string) => {
  const map: Record<string, string> = {
    Draft: "default",
    Published: "green",
    Disabled: "red",
    Archived: "gray"
  };
  return map[status] ?? "default";
};

const statusLabel = (status: string) => {
  const map: Record<string, string> = {
    Draft: t("lowcodeApp.statusDraft"),
    Published: t("lowcodeApp.statusPublished"),
    Disabled: t("lowcodeApp.statusDisabled"),
    Archived: t("lowcodeApp.statusArchived")
  };
  return map[status] ?? status;
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result  = await getTenantAppInstancesPaged({
      pageIndex: 1,
      pageSize: 100,
      keyword: keyword.value || undefined
    });

    if (!isMounted.value) return;
    dataSource.value = result.items;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => {
  fetchData();
};

const handleCreate = () => {
  createWizardVisible.value = true;
};

const handleCreateFromTemplate = () => {
  router.push("/lowcode/templates");
};

const handleCreated = (_appId: string) => {
  fetchData();
};

const handleEdit = (app: TenantAppInstanceListItem) => {
  selectedId.value = app.id;
  editModel.name = app.name;
  editModel.description = app.description ?? "";
  editModel.category = app.category;
  editModel.icon = app.icon ?? "";
  editVisible.value = true;
};

const handleEditSubmit = async () => {
  if (!editModel.name.trim()) {
    message.warning(t("lowcodeApp.warnNameRequired"));
    return;
  }
  if (!selectedId.value) return;
  try {
    await updateTenantAppInstance(selectedId.value, {
      name: editModel.name,
      description: editModel.description || undefined,
      category: editModel.category,
      icon: editModel.icon || undefined
    });

    if (!isMounted.value) return;
    message.success(t("lowcodeApp.updateSuccess"));
    editVisible.value = false;
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.operationFailed"));
  }
};

const handleOpenApp = (id: string) => {
  if (!canManageApps) {
    message.warning("当前账号无应用编辑权限");
    return;
  }
  router.push(`/apps/${id}`);
};

const handlePublish = async (id: string) => {
  try {
    await publishTenantAppInstance(id);

    if (!isMounted.value) return;
    message.success(t("lowcodeApp.publishSuccess"));
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.publishFailed"));
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteTenantAppInstance(id);

    if (!isMounted.value) return;
    message.success(t("lowcodeApp.deleteSuccess"));
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.deleteFailed"));
  }
};

const handleExport = async (app: TenantAppInstanceListItem) => {
  try {
    const blob  = await exportTenantAppInstance(app.id);

    if (!isMounted.value) return;
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${app.appKey}-export.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    message.success("导出成功");
  } catch (error) {
    message.error((error as Error).message || "导出失败");
  }
};

const handleImportClick = () => {
  if (importing.value) {
    return;
  }

  importInputRef.value?.click();
};

const handleImportFileChange = async (event: Event) => {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) {
    return;
  }

  importing.value = true;
  try {
    const rawText  = await file.text();

    if (!isMounted.value) return;
    const pkg = parseLowCodeAppExportPackage(rawText);
    const result  = await importTenantAppInstance({
      package: pkg,
      conflictStrategy: "Rename"
    });

    if (!isMounted.value) return;
    if (result.skipped) {
      message.info("目标应用已存在，已按策略跳过导入");
    } else {
      message.success(`导入成功：${result.appKey}`);
    }
    await fetchData();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || "导入失败");
  } finally {
    importing.value = false;
    input.value = "";
  }
};

onMounted(() => {
  fetchData();
});
</script>

<style scoped>
.app-list-page {
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.page-header-left h2 {
  margin: 0;
  font-size: 20px;
}

.app-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.app-card {
  position: relative;
  background: #fff;
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  padding: 20px;
  cursor: pointer;
  transition: all 0.2s;
}

.app-card:hover {
  border-color: #1890ff;
  box-shadow: 0 2px 8px rgba(24, 144, 255, 0.15);
}

.app-card-icon {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  background: linear-gradient(135deg, #1890ff, #36cfc9);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  color: #fff;
  margin-bottom: 12px;
}

.app-card-content {
  margin-bottom: 12px;
}

.app-card-name {
  font-size: 16px;
  font-weight: 500;
  margin-bottom: 4px;
}

.app-card-key {
  font-size: 12px;
  color: #999;
  margin-bottom: 4px;
}

.app-card-desc {
  font-size: 13px;
  color: #666;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.app-card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.app-card-version {
  font-size: 12px;
  color: #999;
}

.app-card-actions {
  position: absolute;
  top: 12px;
  right: 12px;
}

.app-card-new {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-style: dashed;
  min-height: 200px;
}

.app-card-new-icon {
  font-size: 36px;
  color: #bbb;
  margin-bottom: 8px;
}

.app-card-new-text {
  font-size: 14px;
  color: #999;
}

.loading-container {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 400px;
}
</style>
