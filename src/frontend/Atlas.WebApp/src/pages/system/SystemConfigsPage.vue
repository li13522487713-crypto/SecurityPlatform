<template>
  <div class="system-config-page">
    <div class="toolbar">
      <a-space>
        <a-button type="primary" @click="openCreate">{{ t("systemConfig.create") }}</a-button>
        <a-button :loading="loading" @click="loadConfigs">{{ t("common.refresh") }}</a-button>
      </a-space>
    </div>

    <a-spin :spinning="loading">
      <a-tabs v-model:activeKey="activeGroup" class="group-tabs">
        <a-tab-pane v-for="tab in groupTabs" :key="tab.key">
          <template #tab>
            {{ tab.title }} ({{ groupedConfigs[tab.key]?.length ?? 0 }})
          </template>

          <a-empty v-if="(groupedConfigs[tab.key]?.length ?? 0) === 0" :description="t('systemConfig.empty')" />

          <div v-else class="group-panel">
            <a-form layout="vertical">
              <div
                v-for="item in groupedConfigs[tab.key]"
                :key="item.id"
                class="config-item"
              >
                <div class="config-item-header">
                  <div class="config-item-title">{{ item.configName }}</div>
                  <a-space size="small">
                    <a-tag>{{ item.configKey }}</a-tag>
                    <a-tag v-if="item.isBuiltIn" color="gold">{{ t("systemConfig.builtIn") }}</a-tag>
                    <a-tag v-if="item.isEncrypted" color="magenta">{{ t("systemConfig.secret") }}</a-tag>
                  </a-space>
                </div>

                <a-form-item :label="t('systemConfig.value')">
                  <a-switch
                    v-if="isBooleanType(item.configType)"
                    :checked="toBoolean(item.draftValue)"
                    @change="item.draftValue = $event ? 'true' : 'false'"
                  />
                  <a-input-number
                    v-else-if="item.configType === 'Number'"
                    :value="toNumber(item.draftValue)"
                    style="width: 280px"
                    @change="item.draftValue = $event == null ? '' : String($event)"
                  />
                  <a-input-password
                    v-else-if="item.configType === 'Secret' || item.isEncrypted"
                    v-model:value="item.draftValue"
                    :placeholder="t('systemConfig.valuePlaceholder')"
                  />
                  <a-textarea
                    v-else-if="item.configType === 'Json'"
                    v-model:value="item.draftValue"
                    :rows="4"
                    :placeholder="t('systemConfig.jsonPlaceholder')"
                  />
                  <a-input
                    v-else
                    v-model:value="item.draftValue"
                    :placeholder="t('systemConfig.valuePlaceholder')"
                  />
                </a-form-item>

                <a-form-item :label="t('systemConfig.remark')">
                  <a-input v-model:value="item.draftRemark" :placeholder="t('systemConfig.remarkPlaceholder')" />
                </a-form-item>
              </div>
            </a-form>

            <a-space>
              <a-button type="primary" :loading="savingGroup === tab.key" @click="saveGroup(tab.key)">
                {{ t("common.save") }}
              </a-button>
            </a-space>
          </div>
        </a-tab-pane>
      </a-tabs>
    </a-spin>

    <a-modal
      v-model:open="createModalVisible"
      :title="t('systemConfig.create')"
      :confirm-loading="createLoading"
      @ok="submitCreate"
      @cancel="createModalVisible = false"
    >
      <a-form ref="createFormRef" :model="createForm" layout="vertical" :rules="createRules">
        <a-form-item :label="t('systemConfig.key')" name="configKey">
          <a-input v-model:value="createForm.configKey" :placeholder="t('systemConfig.keyPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemConfig.name')" name="configName">
          <a-input v-model:value="createForm.configName" :placeholder="t('systemConfig.namePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemConfig.value')" name="configValue">
          <a-input v-model:value="createForm.configValue" :placeholder="t('systemConfig.valuePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemConfig.type')" name="configType">
          <a-select v-model:value="createForm.configType" :options="typeOptions" />
        </a-form-item>
        <a-form-item :label="t('systemConfig.group')" name="groupName">
          <a-input v-model:value="createForm.groupName" :placeholder="t('systemConfig.groupPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemConfig.remark')" name="remark">
          <a-input v-model:value="createForm.remark" :placeholder="t('systemConfig.remarkPlaceholder')" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import {
  batchUpsertSystemConfigs,
  createSystemConfig,
  querySystemConfigs,
  type SystemConfigBatchUpsertItem,
  type SystemConfigDto,
  type SystemConfigType
} from "@/services/system-config";

interface EditableSystemConfig extends SystemConfigDto {
  draftValue: string;
  draftRemark: string;
}

const { t } = useI18n();
const loading = ref(false);
const savingGroup = ref("");
const groupedConfigs = reactive<Record<string, EditableSystemConfig[]>>({});
const activeGroup = ref("FileStorage");

const fixedGroups = ["FileStorage", "Security", "AiPlatform", "SystemSwitch", "Custom"] as const;
const groupI18nKeyMap: Record<string, string> = {
  FileStorage: "systemConfig.groupFileStorage",
  Security: "systemConfig.groupSecurity",
  AiPlatform: "systemConfig.groupAiPlatform",
  SystemSwitch: "systemConfig.groupSystemSwitch",
  Custom: "systemConfig.groupCustom"
};

const groupTabs = computed(() => {
  const keys = new Set(Object.keys(groupedConfigs));
  fixedGroups.forEach(key => keys.add(key));
  return Array.from(keys).map(key => ({
    key,
    title: t(groupI18nKeyMap[key] ?? "systemConfig.groupCustom")
  }));
});

function toEditable(item: SystemConfigDto): EditableSystemConfig {
  return {
    ...item,
    draftValue: item.configValue ?? "",
    draftRemark: item.remark ?? ""
  };
}

function normalizeGroup(groupName?: string | null): string {
  if (!groupName || groupName.trim().length === 0) {
    return "Custom";
  }

  return groupName.trim();
}

async function loadConfigs() {
  loading.value = true;
  try {
    const configs = await querySystemConfigs({});
    Object.keys(groupedConfigs).forEach(key => {
      delete groupedConfigs[key];
    });

    configs.forEach(item => {
      const group = normalizeGroup(item.groupName);
      if (!groupedConfigs[group]) {
        groupedConfigs[group] = [];
      }

      groupedConfigs[group].push(toEditable(item));
    });

    fixedGroups.forEach(group => {
      if (!groupedConfigs[group]) {
        groupedConfigs[group] = [];
      }
    });
  } catch (error) {
    message.error((error as Error).message || t("systemConfig.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function isBooleanType(type: SystemConfigType): boolean {
  return type === "Boolean" || type === "FeatureFlag";
}

function toBoolean(value: string): boolean {
  return value === "true" || value === "1";
}

function toNumber(value: string): number | undefined {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

async function saveGroup(groupKey: string) {
  const items = groupedConfigs[groupKey] ?? [];
  if (items.length === 0) {
    return;
  }

  savingGroup.value = groupKey;
  try {
    const payloadItems: SystemConfigBatchUpsertItem[] = items.map(item => ({
      configKey: item.configKey,
      configValue: item.draftValue,
      configName: item.configName,
      remark: item.draftRemark || undefined,
      configType: item.configType,
      targetJson: item.targetJson ?? undefined,
      appId: item.appId ?? undefined,
      groupName: groupKey === "Custom" ? item.groupName ?? undefined : groupKey,
      isEncrypted: item.isEncrypted,
      version: item.version
    }));

    await batchUpsertSystemConfigs({
      groupName: groupKey === "Custom" ? undefined : groupKey,
      items: payloadItems
    });

    message.success(t("systemConfig.batchSaveSuccess"));
    await loadConfigs();
  } catch (error) {
    message.error((error as Error).message || t("systemConfig.operationFailed"));
  } finally {
    savingGroup.value = "";
  }
}

const createModalVisible = ref(false);
const createLoading = ref(false);
const createFormRef = ref();
const createForm = reactive({
  configKey: "",
  configName: "",
  configValue: "",
  configType: "Text" as SystemConfigType,
  groupName: "Custom",
  remark: ""
});

const createRules = {
  configKey: [
    { required: true, message: t("systemConfig.keyRequired") },
    { pattern: /^[a-zA-Z][a-zA-Z0-9_.:\-]{0,127}$/, message: t("systemConfig.keyPattern") }
  ],
  configName: [{ required: true, message: t("systemConfig.nameRequired") }],
  configValue: [{ required: true, message: t("systemConfig.valueRequired") }]
};

const typeOptions = computed(() => [
  { label: "Text", value: "Text" },
  { label: "Number", value: "Number" },
  { label: "Boolean", value: "Boolean" },
  { label: "Json", value: "Json" },
  { label: "Secret", value: "Secret" },
  { label: "FeatureFlag", value: "FeatureFlag" }
]);

function openCreate() {
  Object.assign(createForm, {
    configKey: "",
    configName: "",
    configValue: "",
    configType: "Text",
    groupName: "Custom",
    remark: ""
  });
  createModalVisible.value = true;
}

async function submitCreate() {
  try {
    await createFormRef.value?.validate();
  } catch {
    return;
  }

  createLoading.value = true;
  try {
    await createSystemConfig({
      configKey: createForm.configKey,
      configName: createForm.configName,
      configValue: createForm.configValue,
      configType: createForm.configType,
      groupName: createForm.groupName,
      remark: createForm.remark || undefined,
      isEncrypted: createForm.configType === "Secret"
    });
    message.success(t("systemConfig.createSuccess"));
    createModalVisible.value = false;
    await loadConfigs();
  } catch (error) {
    message.error((error as Error).message || t("systemConfig.operationFailed"));
  } finally {
    createLoading.value = false;
  }
}

onMounted(() => {
  loadConfigs();
});
</script>

<style scoped>
.system-config-page {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.group-panel {
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px;
}

.config-item {
  border-bottom: 1px dashed #f0f0f0;
  padding-bottom: 12px;
  margin-bottom: 12px;
}

.config-item:last-child {
  border-bottom: none;
  margin-bottom: 0;
  padding-bottom: 0;
}

.config-item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.config-item-title {
  font-weight: 600;
}
</style>
