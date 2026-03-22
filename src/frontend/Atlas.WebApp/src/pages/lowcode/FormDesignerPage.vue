<template>
  <div class="form-designer-page">
    <div class="designer-toolbar">
      <div class="toolbar-left">
        <a-button @click="goBack">{{ t("lowcode.formDesigner.back") }}</a-button>
        <a-divider type="vertical" />
        <span class="form-name">{{ formName }}</span>
        <a-tag v-if="formStatus" :color="statusColor(formStatus)">
          {{ statusLabel(formStatus) }}
        </a-tag>
        <span class="version-label">v{{ formVersion }}</span>
      </div>
      <div class="toolbar-center">
        <a-tooltip :title="t('lowcode.builderExtra.undo')">
          <a-button size="small" :disabled="!schemaHistory.canUndo" @click="() => { const s = schemaHistory.undo(); if (s) schema = s; }">
            ↩
          </a-button>
        </a-tooltip>
        <a-tooltip :title="t('lowcode.builderExtra.redo')">
          <a-button size="small" :disabled="!schemaHistory.canRedo" @click="() => { const s = schemaHistory.redo(); if (s) schema = s; }">
            ↪
          </a-button>
        </a-tooltip>
        <a-divider type="vertical" />
        <a-radio-group v-model:value="viewMode" button-style="solid" size="small">
          <a-radio-button value="edit">{{ t("lowcode.formDesigner.modeEdit") }}</a-radio-button>
          <a-radio-button value="preview">{{ t("lowcode.formDesigner.modePreview") }}</a-radio-button>
        </a-radio-group>
        <a-radio-group v-model:value="deviceMode" button-style="solid" size="small">
          <a-radio-button value="pc">{{ t("lowcode.formDesigner.devicePc") }}</a-radio-button>
          <a-radio-button value="mobile">{{ t("lowcode.formDesigner.deviceMobile") }}</a-radio-button>
        </a-radio-group>
      </div>
      <div class="toolbar-right">
        <a-button @click="handleSave" :loading="saving">{{ t("lowcode.formDesigner.save") }}</a-button>
        <a-button type="primary" @click="handlePublish" :loading="publishing">{{ t("lowcode.formDesigner.publish") }}</a-button>
        <a-dropdown>
          <a-button>{{ t("lowcode.formDesigner.more") }}</a-button>
          <template #overlay>
            <a-menu>
              <a-menu-item key="versions" @click="openVersionHistory">
                <template #icon><HistoryOutlined /></template>
                {{ t("lowcode.formDesigner.versionHistory") }}
              </a-menu-item>
              <a-menu-divider />
              <a-menu-item key="guide" @click="showGuide">
                <template #icon><QuestionCircleOutlined /></template>
                {{ t("lowcode.formDesigner.showGuide") }}
              </a-menu-item>
              <a-menu-divider />
              <a-menu-item key="import" @click="handleImport">{{ t("lowcode.formDesigner.importJson") }}</a-menu-item>
              <a-menu-item key="export" @click="handleExport">{{ t("lowcode.formDesigner.exportJson") }}</a-menu-item>
              <a-menu-item key="settings" @click="settingsVisible = true">{{ t("lowcode.formDesigner.formSettings") }}</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </div>
    </div>

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
        <a-spin size="large" :tip="t('lowcode.formDesigner.spinLoad')" />
      </div>
    </div>

    <a-drawer
      v-model:open="settingsVisible"
      :title="t('lowcode.formDesigner.drawerSettings')"
      :width="480"
      placement="right"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('lowcode.formDesigner.labelFormName')">
          <a-input v-model:value="formName" />
        </a-form-item>
        <a-form-item :label="t('lowcode.formDesigner.labelCategory')">
          <a-select v-model:value="formCategory" :placeholder="t('lowcode.formList.phSelectCat')" allow-clear>
            <a-select-option value="人事类">{{ t("lowcode.formList.catHr") }}</a-select-option>
            <a-select-option value="财务类">{{ t("lowcode.formList.catFinance") }}</a-select-option>
            <a-select-option value="采购类">{{ t("lowcode.formList.catPurchase") }}</a-select-option>
            <a-select-option value="通用">{{ t("lowcode.formList.catGeneral") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcode.formDesigner.labelDescription')">
          <a-textarea v-model:value="formDescription" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('lowcode.formDesigner.labelDataTable')">
          <a-input v-model:value="formDataTableKey" :placeholder="t('lowcode.formDesigner.phDataTable')" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="settingsVisible = false">{{ t("lowcode.formDesigner.cancel") }}</a-button>
          <a-button type="primary" @click="handleSaveSettings">{{ t("lowcode.formDesigner.saveSettings") }}</a-button>
        </a-space>
      </template>
    </a-drawer>

    <a-modal
      v-model:open="importVisible"
      :title="t('lowcode.formDesigner.modalImportTitle')"
      :ok-text="t('lowcode.formDesigner.okImport')"
      :cancel-text="t('common.cancel')"
      width="680px"
      @ok="handleImportConfirm"
    >
      <a-textarea
        v-model:value="importJson"
        :rows="16"
        :placeholder="t('lowcode.formDesigner.phImport')"
        style="font-family: monospace; font-size: 13px"
      />
    </a-modal>

    <a-drawer
      v-model:open="versionHistoryVisible"
      :title="t('lowcode.formDesigner.drawerVersions')"
      :width="480"
      placement="right"
    >
      <div v-if="loadingVersions" class="version-loading">
        <a-spin :tip="t('lowcode.formDesigner.spinVersions')" />
      </div>
      <a-empty v-else-if="versionList.length === 0" :description="t('lowcode.formDesigner.emptyVersions')" />
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
                    {{ new Date(item.createdAt).toLocaleString(locale === 'en-US' ? 'en-US' : 'zh-CN') }}
                  </span>
                  <span v-if="item.category" style="color: #999; font-size: 12px">
                    {{ t("lowcode.formDesigner.categoryPrefix") }}: {{ item.category }}
                  </span>
                </a-space>
              </template>
            </a-list-item-meta>
            <template #actions>
              <a-popconfirm
                :title="t('lowcode.formDesigner.rollbackConfirm', { version: item.snapshotVersion })"
                :ok-text="t('lowcode.formDesigner.rollback')"
                :cancel-text="t('common.cancel')"
                @confirm="handleRollback(item.id)"
              >
                <a-button type="link" size="small" :loading="rollingBack === item.id">{{ t("lowcode.formDesigner.rollback") }}</a-button>
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
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { QuestionCircleOutlined, HistoryOutlined } from "@ant-design/icons-vue";
import AmisEditor from "@/components/amis/AmisEditor.vue";
import { useSchemaHistoryStore } from "@/stores/schemaHistory";
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

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const formId = route.params.id as string;

const schemaHistory = useSchemaHistoryStore();
const isMounted = ref(false);
onUnmounted(() => {
  isMounted.value = false;
  document.removeEventListener("keydown", handleFormKeyDown);
});

function handleFormKeyDown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key === "z" && !e.shiftKey) {
    e.preventDefault();
    const prev = schemaHistory.undo();
    if (prev) schema.value = prev;
  }
  if ((e.ctrlKey || e.metaKey) && ((e.key === "z" && e.shiftKey) || e.key === "y")) {
    e.preventDefault();
    const next = schemaHistory.redo();
    if (next) schema.value = next;
  }
}

const editorRef = ref<InstanceType<typeof AmisEditor> | null>(null);
const loadingSchema = ref(true);
const saving = ref(false);
const publishing = ref(false);
const settingsVisible = ref(false);
const importVisible = ref(false);
const importJson = ref("");

const schema = ref<Record<string, unknown>>({
  type: "page",
  title: t("lowcode.formDesigner.newFormDefault"),
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
  const map: Record<string, string> = {
    Draft: t("lowcode.formList.stDraft"),
    Published: t("lowcode.formList.stPublished"),
    Disabled: t("lowcode.formList.stDisabled")
  };
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
    schemaHistory.init(schema.value);
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formDesigner.loadFailed"));
  } finally {
    loadingSchema.value = false;
  }
};

const handleSchemaChange = (newSchema: Record<string, unknown>) => {
  schema.value = newSchema;
  schemaHistory.pushState(newSchema);
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
    message.success(t("lowcode.formDesigner.saveOk"));
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formDesigner.saveFailed"));
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
    message.success(t("lowcode.formDesigner.publishOk"));
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formDesigner.publishFailed"));
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
    message.success(t("lowcode.formDesigner.settingsOk"));
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formDesigner.settingsFailed"));
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
    message.success(t("lowcode.formDesigner.importOk"));
  } catch {
    message.error(t("lowcode.formDesigner.jsonInvalid"));
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
    message.error((error as Error).message || t("lowcode.formDesigner.loadVersionsFailed"));
  } finally {
    loadingVersions.value = false;
  }
};

const handleRollback = async (versionId: string) => {
  if (!formId) return;
  rollingBack.value = versionId;
  try {
    const versionDetail = await getFormDefinitionVersionDetail(formId, versionId);

    if (!isMounted.value) return;
    await rollbackFormDefinitionVersion(formId, versionId);

    if (!isMounted.value) return;

    // Apply rolled-back schema to editor
    try {
      schema.value = JSON.parse(versionDetail.schemaJson) as Record<string, unknown>;
    } catch {
      /* keep current schema */
    }
    formStatus.value = "Published";
    formVersion.value += 1;
    versionHistoryVisible.value = false;
    message.success(t("lowcode.formDesigner.rollbackOk", { version: versionDetail.snapshotVersion }));
    // Reload version list after rollback
    versionList.value = await getFormDefinitionVersions(formId);

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("lowcode.formDesigner.rollbackFailed"));
  } finally {
    rollingBack.value = null;
  }
};

const tourSteps: DriveStep[] = [
  {
    element: ".toolbar-left",
    popover: {
      title: t("lowcode.formDesigner.tourWelcomeTitle"),
      description: t("lowcode.formDesigner.tourWelcomeDesc"),
      side: "bottom",
      align: "start"
    }
  },
  {
    element: ".toolbar-center",
    popover: {
      title: t("lowcode.formDesigner.tourViewTitle"),
      description: t("lowcode.formDesigner.tourViewDesc"),
      side: "bottom",
      align: "center"
    }
  },
  {
    element: ".toolbar-right .ant-btn-primary",
    popover: {
      title: t("lowcode.formDesigner.tourSaveTitle"),
      description: t("lowcode.formDesigner.tourSaveDesc"),
      side: "bottom",
      align: "end"
    }
  },
  {
    element: ".toolbar-right .ant-dropdown-trigger",
    popover: {
      title: t("lowcode.formDesigner.tourMoreTitle"),
      description: t("lowcode.formDesigner.tourMoreDesc"),
      side: "bottom",
      align: "end"
    }
  },
  {
    element: ".designer-body",
    popover: {
      title: t("lowcode.formDesigner.tourCanvasTitle"),
      description: t("lowcode.formDesigner.tourCanvasDesc"),
      side: "top",
      align: "center"
    }
  },
  {
    popover: {
      title: t("lowcode.formDesigner.tourDoneTitle"),
      description: t("lowcode.formDesigner.tourDoneDesc")
    }
  }
];

const { startTour } = useOnboarding({
  storageKey: "atlas-form-designer-tour-viewed",
  steps: tourSteps,
  showOnFirstVisit: true,
  onComplete: () => {
    message.success(t("lowcode.formDesigner.tourComplete"));
  }
});

const showGuide = () => {
  startTour();
};

onMounted(() => {
  isMounted.value = true;
  document.addEventListener("keydown", handleFormKeyDown);
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
