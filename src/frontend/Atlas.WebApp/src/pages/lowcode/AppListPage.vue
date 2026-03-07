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

    <!-- 新建/编辑应用对话框 -->
    <a-modal
      v-model:open="formVisible"
      :title="formMode === 'create' ? t('lowcodeApp.createModalTitle') : t('lowcodeApp.editModalTitle')"
      :ok-text="t('common.confirm')"
      :cancel-text="t('common.cancel')"
      @ok="handleFormSubmit"
    >
      <a-form layout="vertical">
        <a-form-item v-if="formMode === 'create'" :label="t('lowcodeApp.appKeyLabel')" required>
          <a-input
            v-model:value="formModel.appKey"
            :placeholder="t('lowcodeApp.appKeyPlaceholder')"
          />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.appNameLabel')" required>
          <a-input v-model:value="formModel.name" :placeholder="t('lowcodeApp.appNamePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.categoryLabel')">
          <a-select v-model:value="formModel.category" :placeholder="t('lowcodeApp.categoryPlaceholder')" allow-clear>
            <a-select-option value="OA">{{ t("lowcodeApp.categoryOA") }}</a-select-option>
            <a-select-option value="CRM">{{ t("lowcodeApp.categoryCRM") }}</a-select-option>
            <a-select-option value="ERP">{{ t("lowcodeApp.categoryERP") }}</a-select-option>
            <a-select-option value="HR">{{ t("lowcodeApp.categoryHR") }}</a-select-option>
            <a-select-option value="通用">{{ t("lowcodeApp.categoryGeneral") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.descriptionLabel')">
          <a-textarea v-model:value="formModel.description" :rows="3" :placeholder="t('lowcodeApp.descriptionPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.iconLabel')">
          <a-input v-model:value="formModel.icon" :placeholder="t('lowcodeApp.iconPlaceholder')" />
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
import { onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { LowCodeAppListItem } from "@/types/lowcode";
import { getAuthProfile, hasPermission } from "@/utils/auth";
import {
  getLowCodeAppsPaged,
  createLowCodeApp,
  updateLowCodeApp,
  exportLowCodeApp,
  importLowCodeApp,
  parseLowCodeAppExportPackage,
  publishLowCodeApp,
  deleteLowCodeApp
} from "@/services/lowcode";

const router = useRouter();
const { t } = useI18n();
const canManageApps = hasPermission(getAuthProfile(), "apps:update");

const keyword = ref("");
const loading = ref(false);
const dataSource = ref<LowCodeAppListItem[]>([]);
const importing = ref(false);
const importInputRef = ref<HTMLInputElement | null>(null);

const formVisible = ref(false);
const formMode = ref<"create" | "edit">("create");
const selectedId = ref<string | null>(null);
const formModel = reactive({
  appKey: "",
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
    const result = await getLowCodeAppsPaged({
      pageIndex: 1,
      pageSize: 100,
      keyword: keyword.value || undefined
    });
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
  formMode.value = "create";
  selectedId.value = null;
  formModel.appKey = "";
  formModel.name = "";
  formModel.description = "";
  formModel.category = undefined;
  formModel.icon = "";
  formVisible.value = true;
};

const handleEdit = (app: LowCodeAppListItem) => {
  formMode.value = "edit";
  selectedId.value = app.id;
  formModel.appKey = app.appKey;
  formModel.name = app.name;
  formModel.description = app.description ?? "";
  formModel.category = app.category;
  formModel.icon = app.icon ?? "";
  formVisible.value = true;
};

const handleFormSubmit = async () => {
  if (!formModel.name.trim()) {
    message.warning(t("lowcodeApp.warnNameRequired"));
    return;
  }

  try {
    if (formMode.value === "create") {
      if (!formModel.appKey.trim()) {
        message.warning(t("lowcodeApp.warnKeyRequired"));
        return;
      }
      await createLowCodeApp({
        appKey: formModel.appKey,
        name: formModel.name,
        description: formModel.description || undefined,
        category: formModel.category,
        icon: formModel.icon || undefined
      });
      message.success(t("lowcodeApp.createSuccess"));
    } else if (selectedId.value) {
      await updateLowCodeApp(selectedId.value, {
        name: formModel.name,
        description: formModel.description || undefined,
        category: formModel.category,
        icon: formModel.icon || undefined
      });
      message.success(t("lowcodeApp.updateSuccess"));
    }
    formVisible.value = false;
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
  router.push(`/lowcode/apps/${id}/builder`);
};

const handlePublish = async (id: string) => {
  try {
    await publishLowCodeApp(id);
    message.success(t("lowcodeApp.publishSuccess"));
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.publishFailed"));
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteLowCodeApp(id);
    message.success(t("lowcodeApp.deleteSuccess"));
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.deleteFailed"));
  }
};

const handleExport = async (app: LowCodeAppListItem) => {
  try {
    const blob = await exportLowCodeApp(app.id);
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
    const rawText = await file.text();
    const pkg = parseLowCodeAppExportPackage(rawText);
    const result = await importLowCodeApp({
      package: pkg,
      conflictStrategy: "Rename"
    });
    if (result.skipped) {
      message.info("目标应用已存在，已按策略跳过导入");
    } else {
      message.success(`导入成功：${result.appKey}`);
    }
    await fetchData();
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
