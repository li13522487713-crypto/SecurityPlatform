<template>
  <div class="app-list-page">
    <div class="page-header">
      <div class="page-header-left">
        <h2>应用管理</h2>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索应用名称或标识"
          allow-clear
          style="width: 260px"
          @search="handleSearch"
        />
      </div>
      <div class="page-header-right">
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
          <div class="app-card-desc">{{ app.description || "暂无描述" }}</div>
        </div>
        <div class="app-card-footer">
          <a-tag :color="statusColor(app.status)">
            {{ statusLabel(app.status) }}
          </a-tag>
          <span class="app-card-version">v{{ app.version }}</span>
        </div>
        <div class="app-card-actions" @click.stop>
          <a-dropdown v-if="canManageApps" trigger="click">
            <a-button type="text" size="small">...</a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="edit" @click="handleEdit(app)">编辑</a-menu-item>
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
        <div class="app-card-new-text">新建应用</div>
      </div>
    </div>

    <div v-else class="loading-container">
      <a-spin size="large" tip="加载应用列表..." />
    </div>

    <!-- 新建/编辑应用对话框 -->
    <a-modal
      v-model:open="formVisible"
      :title="formMode === 'create' ? '新建应用' : '编辑应用'"
      ok-text="确定"
      cancel-text="取消"
      @ok="handleFormSubmit"
    >
      <a-form layout="vertical">
        <a-form-item v-if="formMode === 'create'" label="应用标识" required>
          <a-input
            v-model:value="formModel.appKey"
            placeholder="如 crm, oa, erp（字母开头，只能包含字母数字下划线连字符）"
          />
        </a-form-item>
        <a-form-item label="应用名称" required>
          <a-input v-model:value="formModel.name" placeholder="请输入应用名称" />
        </a-form-item>
        <a-form-item label="分类">
          <a-select v-model:value="formModel.category" placeholder="选择分类" allow-clear>
            <a-select-option value="OA">OA 办公</a-select-option>
            <a-select-option value="CRM">客户管理</a-select-option>
            <a-select-option value="ERP">资源管理</a-select-option>
            <a-select-option value="HR">人事管理</a-select-option>
            <a-select-option value="通用">通用</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="formModel.description" :rows="3" placeholder="请输入描述" />
        </a-form-item>
        <a-form-item label="图标">
          <a-input v-model:value="formModel.icon" placeholder="输入 emoji 或图标名称" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { LowCodeAppListItem } from "@/types/lowcode";
import { getAuthProfile, hasPermission } from "@/utils/auth";
import {
  getLowCodeAppsPaged,
  createLowCodeApp,
  updateLowCodeApp,
  publishLowCodeApp,
  deleteLowCodeApp
} from "@/services/lowcode";

const router = useRouter();
const canManageApps = hasPermission(getAuthProfile(), "apps:update");

const keyword = ref("");
const loading = ref(false);
const dataSource = ref<LowCodeAppListItem[]>([]);

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
    Draft: "草稿",
    Published: "已发布",
    Disabled: "已停用",
    Archived: "已归档"
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
    message.error((error as Error).message || "查询失败");
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
    message.warning("请输入应用名称");
    return;
  }

  try {
    if (formMode.value === "create") {
      if (!formModel.appKey.trim()) {
        message.warning("请输入应用标识");
        return;
      }
      await createLowCodeApp({
        appKey: formModel.appKey,
        name: formModel.name,
        description: formModel.description || undefined,
        category: formModel.category,
        icon: formModel.icon || undefined
      });
      message.success("创建成功");
    } else if (selectedId.value) {
      await updateLowCodeApp(selectedId.value, {
        name: formModel.name,
        description: formModel.description || undefined,
        category: formModel.category,
        icon: formModel.icon || undefined
      });
      message.success("更新成功");
    }
    formVisible.value = false;
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "操作失败");
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
    message.success("发布成功");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "发布失败");
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteLowCodeApp(id);
    message.success("已删除");
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "删除失败");
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
