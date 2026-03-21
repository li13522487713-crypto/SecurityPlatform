<template>
  <div class="form-designer-page">
    <!-- 顶部工具栏 -->
    <div class="designer-toolbar">
      <div class="toolbar-left">
        <a-button @click="goBack">返回</a-button>
        <a-divider type="vertical" />
        <span class="form-name">{{ formName }}</span>
        <a-tag v-if="formStatus" :color="statusColor(formStatus)">
          {{ statusLabel(formStatus) }}
        </a-tag>
        <span class="version-label">v{{ formVersion }}</span>
      </div>
      <div class="toolbar-center">
        <a-radio-group v-model:value="viewMode" button-style="solid" size="small">
          <a-radio-button value="edit">编辑</a-radio-button>
          <a-radio-button value="preview">预览</a-radio-button>
        </a-radio-group>
        <a-radio-group v-model:value="deviceMode" button-style="solid" size="small">
          <a-radio-button value="pc">PC</a-radio-button>
          <a-radio-button value="mobile">移动端</a-radio-button>
        </a-radio-group>
      </div>
      <div class="toolbar-right">
        <a-button @click="handleSave" :loading="saving">保存</a-button>
        <a-button type="primary" @click="handlePublish" :loading="publishing">发布</a-button>
        <a-dropdown>
          <a-button>更多</a-button>
          <template #overlay>
            <a-menu>
              <a-menu-item key="versions" @click="openVersionHistory">
                <template #icon><HistoryOutlined /></template>
                版本历史
              </a-menu-item>
              <a-menu-divider />
              <a-menu-item key="guide" @click="showGuide">
                <template #icon><QuestionCircleOutlined /></template>
                显示引导
              </a-menu-item>
              <a-menu-divider />
              <a-menu-item key="import" @click="handleImport">导入 JSON</a-menu-item>
              <a-menu-item key="export" @click="handleExport">导出 JSON</a-menu-item>
              <a-menu-item key="settings" @click="settingsVisible = true">表单设置</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </div>
    </div>

    <!-- 设计器主体 -->
    <div class="designer-body">
      <AmisEditor
        v-if="!loadingSchema"
        ref="editorRef"
        :schema="schema"
        :preview="viewMode === 'preview'"
        :is-mobile="deviceMode === 'mobile'"
        height="calc(100vh - 56px)"
        @change="handleSchemaChange"
        @save="handleSave"
      />
      <div v-else class="loading-container">
        <a-spin size="large" tip="加载表单定义..." />
      </div>
    </div>

    <!-- 表单设置抽屉 -->
    <a-drawer
      v-model:open="settingsVisible"
      title="表单设置"
      :width="480"
      placement="right"
    >
      <a-form layout="vertical">
        <a-form-item label="表单名称">
          <a-input v-model:value="formName" />
        </a-form-item>
        <a-form-item label="分类">
          <a-select v-model:value="formCategory" placeholder="选择分类" allow-clear>
            <a-select-option value="人事类">人事类</a-select-option>
            <a-select-option value="财务类">财务类</a-select-option>
            <a-select-option value="采购类">采购类</a-select-option>
            <a-select-option value="通用">通用</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="formDescription" :rows="3" />
        </a-form-item>
        <a-form-item label="数据表绑定">
          <a-input v-model:value="formDataTableKey" placeholder="关联的动态表 Key" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="settingsVisible = false">取消</a-button>
          <a-button type="primary" @click="handleSaveSettings">保存设置</a-button>
        </a-space>
      </template>
    </a-drawer>

    <!-- 导入 JSON 对话框 -->
    <a-modal
      v-model:open="importVisible"
      title="导入 JSON Schema"
      ok-text="导入"
      cancel-text="取消"
      width="680px"
      @ok="handleImportConfirm"
    >
      <a-textarea
        v-model:value="importJson"
        :rows="16"
        placeholder="粘贴 amis JSON Schema..."
        style="font-family: monospace; font-size: 13px"
      />
    </a-modal>

    <!-- 版本历史抽屉 -->
    <a-drawer
      v-model:open="versionHistoryVisible"
      title="版本历史"
      :width="480"
      placement="right"
    >
      <div v-if="loadingVersions" class="version-loading">
        <a-spin tip="加载版本历史..." />
      </div>
      <a-empty v-else-if="versionList.length === 0" description="暂无版本历史，发布后可在此查看" />
      <a-list
        v-else
        :data-source="versionList"
        item-layout="horizontal"
      >
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta>
              <template #title>
                <span>v{{ item.snapshotVersion }} — {{ item.name }}</span>
              </template>
              <template #description>
                <a-space direction="vertical" :size="2">
                  <span style="color: #666; font-size: 12px">
                    {{ new Date(item.createdAt).toLocaleString('zh-CN') }}
                  </span>
                  <span v-if="item.category" style="color: #999; font-size: 12px">
                    分类：{{ item.category }}
                  </span>
                </a-space>
              </template>
            </a-list-item-meta>
            <template #actions>
              <a-popconfirm
                :title="`确定回滚到 v${item.snapshotVersion}？当前未保存内容将丢失。`"
                ok-text="回滚"
                cancel-text="取消"
                @confirm="handleRollback(item.id)"
              >
                <a-button type="link" size="small" :loading="rollingBack === item.id">回滚</a-button>
              </a-popconfirm>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { QuestionCircleOutlined, HistoryOutlined } from "@ant-design/icons-vue";
import AmisEditor from "@/components/amis/AmisEditor.vue";
import {
  getFormDefinitionDetail,
  updateFormDefinition,
  updateFormDefinitionSchema,
  publishFormDefinition,
  getFormDefinitionVersions,
  getFormDefinitionVersionDetail,
  rollbackFormDefinitionVersion
} from "@/services/lowcode";
import type { FormDefinitionVersionListItem } from "@/types/lowcode";
import { useOnboarding } from "@/composables/useOnboarding";
import type { DriveStep } from "driver.js";

const route = useRoute();
const router = useRouter();
const formId = route.params.id as string;

const editorRef = ref<InstanceType<typeof AmisEditor> | null>(null);
const loadingSchema = ref(true);
const saving = ref(false);
const publishing = ref(false);
const settingsVisible = ref(false);
const importVisible = ref(false);
const importJson = ref("");

const schema = ref<Record<string, unknown>>({
  type: "page",
  title: "新表单",
  body: [{ type: "form", title: "", body: [] }]
});

const formName = ref("");
const formDescription = ref("");
const formCategory = ref<string | undefined>(undefined);
const formDataTableKey = ref("");
const formStatus = ref("");
const formVersion = ref(1);

const viewMode = ref<"edit" | "preview">("edit");
const deviceMode = ref<"pc" | "mobile">("pc");

const versionHistoryVisible = ref(false);
const loadingVersions = ref(false);
const versionList = ref<FormDefinitionVersionListItem[]>([]);
const rollingBack = ref<string | null>(null);

const statusColor = (status: string) => {
  const map: Record<string, string> = { Draft: "default", Published: "green", Disabled: "red" };
  return map[status] ?? "default";
};

const statusLabel = (status: string) => {
  const map: Record<string, string> = { Draft: "草稿", Published: "已发布", Disabled: "已停用" };
  return map[status] ?? status;
};

const loadFormDefinition = async () => {
  if (!formId) {
    loadingSchema.value = false;
    return;
  }

  try {
    const detail  = await getFormDefinitionDetail(formId);

    if (!isMounted.value) return;
    formName.value = detail.name;
    formDescription.value = detail.description ?? "";
    formCategory.value = detail.category;
    formDataTableKey.value = detail.dataTableKey ?? "";
    formStatus.value = detail.status;
    formVersion.value = detail.version;

    try {
      schema.value = JSON.parse(detail.schemaJson) as Record<string, unknown>;
    } catch {
      schema.value = { type: "page", title: detail.name, body: [] };
    }
  } catch (error) {
    message.error((error as Error).message || "加载表单定义失败");
  } finally {
    loadingSchema.value = false;
  }
};

const handleSchemaChange = (newSchema: Record<string, unknown>) => {
  schema.value = newSchema;
};

const handleSave = async () => {
  if (!formId) return;

  saving.value = true;
  try {
    const currentSchema = editorRef.value?.getSchema() ?? schema.value;
    const schemaJson = JSON.stringify(currentSchema);

    await updateFormDefinitionSchema(formId, schemaJson);


    if (!isMounted.value) return;
    formVersion.value += 1;
    message.success("保存成功");
  } catch (error) {
    message.error((error as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
};

const handlePublish = async () => {
  if (!formId) return;

  // Save first, then publish
  publishing.value = true;
  try {
    const currentSchema = editorRef.value?.getSchema() ?? schema.value;
    const schemaJson = JSON.stringify(currentSchema);

    await updateFormDefinitionSchema(formId, schemaJson);


    if (!isMounted.value) return;
    await publishFormDefinition(formId);

    if (!isMounted.value) return;
    formStatus.value = "Published";
    message.success("发布成功");
  } catch (error) {
    message.error((error as Error).message || "发布失败");
  } finally {
    publishing.value = false;
  }
};

const handleSaveSettings = async () => {
  if (!formId) return;

  try {
    const currentSchema = editorRef.value?.getSchema() ?? schema.value;
    await updateFormDefinition(formId, {
      name: formName.value,
      description: formDescription.value || undefined,
      category: formCategory.value,
      schemaJson: JSON.stringify(currentSchema),
      dataTableKey: formDataTableKey.value || undefined
    });

    if (!isMounted.value) return;
    settingsVisible.value = false;
    message.success("设置已保存");
  } catch (error) {
    message.error((error as Error).message || "保存设置失败");
  }
};

const handleImport = () => {
  importJson.value = "";
  importVisible.value = true;
};

const handleImportConfirm = () => {
  try {
    const parsed = JSON.parse(importJson.value) as Record<string, unknown>;
    schema.value = parsed;
    importVisible.value = false;
    message.success("导入成功");
  } catch {
    message.error("JSON 格式不正确");
  }
};

const handleExport = () => {
  const currentSchema = editorRef.value?.getSchema() ?? schema.value;
  const blob = new Blob([JSON.stringify(currentSchema, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `${formName.value || "form"}.json`;
  a.click();
  URL.revokeObjectURL(url);
};

const goBack = () => {
  router.push({ name: "apps-forms" });
};

const openVersionHistory = async () => {
  versionHistoryVisible.value = true;
  if (!formId) return;
  loadingVersions.value = true;
  try {
    versionList.value = await getFormDefinitionVersions(formId);

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || "加载版本历史失败");
  } finally {
    loadingVersions.value = false;
  }
};

const handleRollback = async (versionId: string) => {
  if (!formId) return;
  rollingBack.value = versionId;
  try {
    // 获取版本详情并恢复 Schema
    const versionDetail  = await getFormDefinitionVersionDetail(formId, versionId);

    if (!isMounted.value) return;
    await rollbackFormDefinitionVersion(formId, versionId);

    if (!isMounted.value) return;

    // 刷新当前页面 Schema
    try {
      schema.value = JSON.parse(versionDetail.schemaJson) as Record<string, unknown>;
    } catch {
      // 保持现有 schema
    }
    formStatus.value = "Published";
    formVersion.value += 1;
    versionHistoryVisible.value = false;
    message.success(`已回滚到 v${versionDetail.snapshotVersion}`);
    // 刷新版本列表
    versionList.value = await getFormDefinitionVersions(formId);

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || "回滚失败");
  } finally {
    rollingBack.value = null;
  }
};

// 新手引导配置
const tourSteps: DriveStep[] = [
  {
    element: ".toolbar-left",
    popover: {
      title: "欢迎使用表单设计器",
      description:
        "这是Atlas低代码平台的表单设计器。您可以使用可视化方式设计表单，无需编写代码。让我们快速了解一下各个功能区域。",
      side: "bottom",
      align: "start"
    }
  },
  {
    element: ".toolbar-center",
    popover: {
      title: "视图模式切换",
      description:
        "您可以在【编辑】和【预览】模式之间切换，实时查看表单效果。同时支持PC端和移动端预览。",
      side: "bottom",
      align: "center"
    }
  },
  {
    element: ".toolbar-right .ant-btn-primary",
    popover: {
      title: "保存与发布",
      description:
        "点击【保存】保存当前设计。点击【发布】使表单对外可用。只有已发布的表单才能被用户填写。",
      side: "bottom",
      align: "end"
    }
  },
  {
    element: ".toolbar-right .ant-dropdown-trigger",
    popover: {
      title: "更多操作",
      description:
        "在这里可以导入/导出JSON Schema，配置表单设置（如绑定数据表），以及重新显示此引导。",
      side: "bottom",
      align: "end"
    }
  },
  {
    element: ".designer-body",
    popover: {
      title: "设计器画布",
      description:
        "这是主要的设计区域。您可以通过拖拽组件、配置属性来设计表单。AMIS编辑器提供了丰富的表单组件供您使用。",
      side: "top",
      align: "center"
    }
  },
  {
    popover: {
      title: "开始设计吧！",
      description:
        "现在您已经了解了表单设计器的基本功能。尝试添加一些表单字段，然后保存和预览您的设计吧！如需再次查看引导，请点击右上角【更多】→【显示引导】。"
    }
  }
];

const { startTour } = useOnboarding({
  storageKey: "atlas-form-designer-tour-viewed",
  steps: tourSteps,
  showOnFirstVisit: true,
  onComplete: () => {
    message.success("引导完成！开始设计您的表单吧");
  }
});

const showGuide = () => {
  startTour();
};

onMounted(() => {
  loadFormDefinition();
});
</script>

<style scoped>
.form-designer-page {
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
}

.designer-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 56px;
  padding: 0 16px;
  background: #fff;
  border-bottom: 1px solid #e8e8e8;
  flex-shrink: 0;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-center {
  display: flex;
  align-items: center;
  gap: 12px;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.form-name {
  font-size: 16px;
  font-weight: 500;
}

.version-label {
  font-size: 12px;
  color: #999;
}

.designer-body {
  flex: 1;
  overflow: hidden;
}

.loading-container {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.version-loading {
  display: flex;
  justify-content: center;
  padding: 40px 0;
}
</style>
