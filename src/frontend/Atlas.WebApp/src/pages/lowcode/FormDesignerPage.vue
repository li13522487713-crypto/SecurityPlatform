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
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import AmisEditor from "@/components/amis/AmisEditor.vue";
import {
  getFormDefinitionDetail,
  updateFormDefinition,
  updateFormDefinitionSchema,
  publishFormDefinition
} from "@/services/lowcode";

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
    const detail = await getFormDefinitionDetail(formId);
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
    await publishFormDefinition(formId);
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
  router.push({ name: "form-list" });
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
</style>
