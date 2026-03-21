<template>
  <a-modal
    v-model:open="visible"
    :title="t('lowcodeApp.createModalTitle')"
    :footer="null"
    width="600px"
    @cancel="handleCancel"
  >
    <a-steps :current="currentStep" size="small" style="margin-bottom: 24px">
      <a-step :title="t('lowcodeApp.wizard.step1Title')" />
      <a-step :title="t('lowcodeApp.wizard.step2Title')" />
      <a-step :title="t('lowcodeApp.wizard.step3Title')" />
    </a-steps>

    <!-- Step 1: 基本信息 -->
    <div v-if="currentStep === 0">
      <a-form layout="vertical" :model="basicForm">
        <a-form-item :label="t('lowcodeApp.appKeyLabel')" required>
          <a-input
            v-model:value="basicForm.appKey"
            :placeholder="t('lowcodeApp.appKeyPlaceholder')"
          />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.appNameLabel')" required>
          <a-input v-model:value="basicForm.name" :placeholder="t('lowcodeApp.appNamePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.categoryLabel')">
          <a-select v-model:value="basicForm.category" :placeholder="t('lowcodeApp.categoryPlaceholder')" allow-clear>
            <a-select-option value="OA">{{ t("lowcodeApp.categoryOA") }}</a-select-option>
            <a-select-option value="CRM">{{ t("lowcodeApp.categoryCRM") }}</a-select-option>
            <a-select-option value="ERP">{{ t("lowcodeApp.categoryERP") }}</a-select-option>
            <a-select-option value="HR">{{ t("lowcodeApp.categoryHR") }}</a-select-option>
            <a-select-option value="通用">{{ t("lowcodeApp.categoryGeneral") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.descriptionLabel')">
          <a-textarea v-model:value="basicForm.description" :rows="3" :placeholder="t('lowcodeApp.descriptionPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.iconLabel')">
          <a-input v-model:value="basicForm.icon" :placeholder="t('lowcodeApp.iconPlaceholder')" />
        </a-form-item>
      </a-form>
    </div>

    <!-- Step 2: 数据源绑定 -->
    <div v-if="currentStep === 1">
      <a-alert
        type="info"
        :message="t('lowcodeApp.wizard.datasourceNote')"
        show-icon
        style="margin-bottom: 16px"
      />
      <a-form layout="vertical">
        <a-form-item :label="t('lowcodeApp.wizard.datasourceLabel')">
          <a-select
            v-model:value="datasourceForm.dataSourceId"
            :placeholder="t('lowcodeApp.wizard.datasourcePlaceholder')"
            allow-clear
            :loading="loadingDatasources"
            show-search
            option-filter-prop="children"
            style="width: 100%"
          >
            <a-select-option
              v-for="ds in datasources"
              :key="ds.id"
              :value="ds.id"
            >
              <a-badge :status="ds.isActive ? 'success' : 'default'" />
              {{ ds.name }}
              <a-tag size="small" style="margin-left: 8px">{{ ds.dbType }}</a-tag>
            </a-select-option>
          </a-select>
        </a-form-item>
        <div v-if="!datasourceForm.dataSourceId" class="datasource-skip-hint">
          {{ t('lowcodeApp.wizard.datasourceSkipHint') }}
        </div>
      </a-form>
    </div>

    <!-- Step 3: 共享策略 -->
    <div v-if="currentStep === 2">
      <a-alert
        type="info"
        :message="t('lowcodeApp.wizard.sharingNote')"
        show-icon
        style="margin-bottom: 16px"
      />
      <a-alert
        v-if="hasIsolatedPolicy && !datasourceForm.dataSourceId"
        type="warning"
        show-icon
        :message="t('lowcode.createWizard.alertIsolatedTitle')"
        :description="t('lowcode.createWizard.alertIsolatedDesc')"
        style="margin-bottom: 16px"
      />
      <a-form layout="vertical">
        <a-form-item class="policy-form-item">
          <div class="policy-row">
            <div class="policy-title">{{ t("lowcode.createWizard.policyUsers") }}</div>
            <a-switch
              v-model:checked="sharingForm.useSharedUsers"
              :checked-children="t('lowcode.createWizard.inheritPlatform')"
              :un-checked-children="t('lowcode.createWizard.appStandalone')"
            />
            <a-tag :color="sharingForm.useSharedUsers ? 'processing' : 'warning'">
              {{ sharingForm.useSharedUsers ? t("lowcode.createWizard.modeShared") : t("lowcode.createWizard.modeIsolated") }}
            </a-tag>
          </div>
          <div class="sharing-hint">{{ t('lowcodeApp.wizard.useSharedUsersHint') }}</div>
        </a-form-item>
        <a-form-item class="policy-form-item">
          <div class="policy-row">
            <div class="policy-title">{{ t("lowcode.createWizard.policyRoles") }}</div>
            <a-switch
              v-model:checked="sharingForm.useSharedRoles"
              :checked-children="t('lowcode.createWizard.inheritPlatform')"
              :un-checked-children="t('lowcode.createWizard.appStandalone')"
            />
            <a-tag :color="sharingForm.useSharedRoles ? 'processing' : 'warning'">
              {{ sharingForm.useSharedRoles ? t("lowcode.createWizard.modeShared") : t("lowcode.createWizard.modeIsolated") }}
            </a-tag>
          </div>
          <div class="sharing-hint">{{ t('lowcodeApp.wizard.useSharedRolesHint') }}</div>
        </a-form-item>
        <a-form-item class="policy-form-item">
          <div class="policy-row">
            <div class="policy-title">{{ t("lowcode.createWizard.policyDepts") }}</div>
            <a-switch
              v-model:checked="sharingForm.useSharedDepartments"
              :checked-children="t('lowcode.createWizard.inheritPlatform')"
              :un-checked-children="t('lowcode.createWizard.appStandalone')"
            />
            <a-tag :color="sharingForm.useSharedDepartments ? 'processing' : 'warning'">
              {{ sharingForm.useSharedDepartments ? t("lowcode.createWizard.modeShared") : t("lowcode.createWizard.modeIsolated") }}
            </a-tag>
          </div>
          <div class="sharing-hint">{{ t('lowcodeApp.wizard.useSharedDepartmentsHint') }}</div>
        </a-form-item>
      </a-form>
    </div>

    <!-- 底部按钮 -->
    <div class="wizard-footer">
      <a-button v-if="currentStep > 0" @click="currentStep--">{{ t('common.previous') }}</a-button>
      <a-space>
        <a-button @click="handleCancel">{{ t('common.cancel') }}</a-button>
        <a-button v-if="currentStep < 2" type="primary" @click="handleNext">
          {{ t('common.next') }}
        </a-button>
        <a-button v-else type="primary" :loading="submitting" @click="handleSubmit">
          {{ t('common.confirm') }}
        </a-button>
      </a-space>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, ref, reactive, watch } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { TenantDataSourceDto } from "@/types/api";
import { getTenantDataSources } from "@/services/api-system";
import { createTenantAppInstance } from "@/services/api-tenant-app-instances";

const { t } = useI18n();

interface Props {
  open: boolean;
}
const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "update:open", val: boolean): void;
  (e: "created", appId: string): void;
}>();

const visible = ref(false);
const currentStep = ref(0);
const submitting = ref(false);
const loadingDatasources = ref(false);
const datasources = ref<TenantDataSourceDto[]>([]);

const basicForm = reactive({
  appKey: "",
  name: "",
  description: "",
  category: undefined as string | undefined,
  icon: ""
});

const datasourceForm = reactive({
  dataSourceId: undefined as string | undefined
});

const sharingForm = reactive({
  useSharedUsers: true,
  useSharedRoles: true,
  useSharedDepartments: true
});
const hasIsolatedPolicy = computed(
  () => !sharingForm.useSharedUsers || !sharingForm.useSharedRoles || !sharingForm.useSharedDepartments
);

watch(
  () => props.open,
  (val) => {
    visible.value = val;
    if (val) {
      resetForm();
      loadDatasources();
    }
  }
);

watch(visible, (val) => {
  if (!val) emit("update:open", false);
});

const resetForm = () => {
  currentStep.value = 0;
  basicForm.appKey = "";
  basicForm.name = "";
  basicForm.description = "";
  basicForm.category = undefined;
  basicForm.icon = "";
  datasourceForm.dataSourceId = undefined;
  sharingForm.useSharedUsers = true;
  sharingForm.useSharedRoles = true;
  sharingForm.useSharedDepartments = true;
};

const loadDatasources = async () => {
  loadingDatasources.value = true;
  try {
    datasources.value = await getTenantDataSources();
  } catch {
    datasources.value = [];
  } finally {
    loadingDatasources.value = false;
  }
};

const handleNext = () => {
  if (currentStep.value === 0) {
    if (!basicForm.appKey.trim()) {
      message.warning(t("lowcodeApp.warnKeyRequired"));
      return;
    }
    if (!basicForm.name.trim()) {
      message.warning(t("lowcodeApp.warnNameRequired"));
      return;
    }
  }
  currentStep.value++;
};

const handleCancel = () => {
  visible.value = false;
};

const handleSubmit = async () => {
  if (hasIsolatedPolicy.value && !datasourceForm.dataSourceId) {
    message.warning(t("lowcode.createWizard.warnIsolatedBindDs"));
    return;
  }

  submitting.value = true;
  try {
    const result = await createTenantAppInstance({
      appKey: basicForm.appKey,
      name: basicForm.name,
      description: basicForm.description || undefined,
      category: basicForm.category,
      icon: basicForm.icon || undefined,
      dataSourceId: datasourceForm.dataSourceId ? Number(datasourceForm.dataSourceId) : undefined,
      useSharedUsers: sharingForm.useSharedUsers,
      useSharedRoles: sharingForm.useSharedRoles,
      useSharedDepartments: sharingForm.useSharedDepartments
    });
    message.success(t("lowcodeApp.createSuccess"));
    visible.value = false;
    emit("created", result.id);
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.operationFailed"));
  } finally {
    submitting.value = false;
  }
};
</script>

<style scoped>
.wizard-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid #f0f0f0;
}

.datasource-skip-hint {
  color: #999;
  font-size: 12px;
  margin-top: 4px;
}

.sharing-hint {
  color: #999;
  font-size: 12px;
  margin-top: 2px;
  margin-left: 12px;
}

.policy-form-item {
  margin-bottom: 12px;
}

.policy-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.policy-title {
  min-width: 120px;
  font-weight: 500;
}
</style>
