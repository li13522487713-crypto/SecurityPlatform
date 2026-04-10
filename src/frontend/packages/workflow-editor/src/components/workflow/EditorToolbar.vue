<template>
  <div class="editor-header">
    <div class="header-left">
      <a-button type="text" style="color: #fff" @click="$emit('back')">
        <LeftOutlined />
      </a-button>
      <a-input
        :value="name"
        class="name-input"
        @update:value="$emit('update:name', $event)"
        @blur="$emit('name-blur')"
        @press-enter="$emit('name-blur')"
      />
      <a-tag v-if="isDirty" color="orange">{{ t("workflow.editorUnsaved") }}</a-tag>
      <a-tag v-else color="green">{{ autoSaveLabel }}</a-tag>
    </div>

    <div class="header-right">
      <a-space>
        <a-button :loading="saving" @click="$emit('save-draft')">{{ t("workflow.saveDraft") }}</a-button>
        <a-button type="primary" @click="$emit('publish')">{{ t("workflow.publish") }}</a-button>
        <a-button @click="$emit('open-version-history')">{{ t("workflow.colLatestVersion") }}</a-button>
        <a-button :type="showTestPanel ? 'primary' : 'default'" @click="$emit('toggle-test-panel')">
          <PlayCircleOutlined />
          {{ t("workflow.testRunToolbar") }}
        </a-button>
        <a-dropdown>
          <a-button>
            {{ t("workflow.moreActions") }}
            <DownOutlined />
          </a-button>
          <template #overlay>
            <a-menu @click="handleMenuClick">
              <a-menu-item key="export-json">{{ t("workflow.exportCanvasJson") }}</a-menu-item>
              <a-menu-item key="import-json">{{ t("workflow.importCanvasJson") }}</a-menu-item>
              <a-menu-item key="reset-canvas">{{ t("workflow.resetCanvas") }}</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </a-space>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { LeftOutlined, PlayCircleOutlined, DownOutlined } from "@ant-design/icons-vue";
import type { MenuInfo } from "ant-design-vue/es/menu/src/interface";

const props = defineProps<{
  name: string;
  isDirty: boolean;
  saving: boolean;
  showTestPanel: boolean;
  autoSavedAt?: string;
}>();

const emit = defineEmits<{
  (e: "back"): void;
  (e: "update:name", value: string): void;
  (e: "name-blur"): void;
  (e: "save-draft"): void;
  (e: "publish"): void;
  (e: "toggle-test-panel"): void;
  (e: "open-version-history"): void;
  (e: "menu-action", key: string): void;
}>();

const { t } = useI18n();

const autoSaveLabel = computed(() => {
  if (!props.autoSavedAt) {
    return t("workflow.autosaveNotStarted");
  }
  return t("workflow.autosaveAt", { time: props.autoSavedAt });
});

function handleMenuClick(info: MenuInfo) {
  emit("menu-action", String(info.key));
}
</script>

<style scoped>
.editor-header {
  height: 56px;
  background: #111820;
  border-bottom: 1px solid #2a3440;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.header-right {
  display: flex;
  align-items: center;
}

.name-input {
  width: 260px;
}

:deep(.name-input .ant-input) {
  background: #0d1117;
  border-color: #30363d;
  color: #e6edf3;
  font-weight: 600;
}
</style>
