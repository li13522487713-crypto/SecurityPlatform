<template>
  <div class="license-page">
    <a-page-header :title="t('licensePage.pageTitle')" :sub-title="t('licensePage.pageSubtitle')" />

    <div class="license-content">
      <!-- 授权状态卡片 -->
      <a-row :gutter="[16, 16]">
        <a-col :xs="24" :md="12">
          <a-card :title="t('licensePage.statusCardTitle')" :loading="loading">
            <template #extra>
              <a-tag :color="statusColor">{{ statusLabel }}</a-tag>
            </template>

            <a-descriptions :column="1" bordered size="small">
              <a-descriptions-item :label="t('licensePage.labelLicenseEdition')">
                <a-tag :color="editionColor">{{ editionLabel }}</a-tag>
              </a-descriptions-item>
              <a-descriptions-item :label="t('licensePage.labelValidTo')">
                <template v-if="licenseStatus?.isPermanent">
                  <a-tag color="green">{{ t("licensePage.permanentTag") }}</a-tag>
                </template>
                <template v-else-if="licenseStatus?.expiresAt">
                  {{ formatDate(licenseStatus.expiresAt) }}
                  <a-tag v-if="licenseStatus.remainingDays !== null" :color="remainingDaysColor" style="margin-left: 8px">
                    {{ t("licensePage.remainingDaysTag", { days: licenseStatus.remainingDays }) }}
                  </a-tag>
                </template>
                <template v-else>{{ t("licensePage.dash") }}</template>
              </a-descriptions-item>
              <a-descriptions-item :label="t('licensePage.labelIssuedAt')">
                {{ licenseStatus?.issuedAt ? formatDate(licenseStatus.issuedAt) : t("licensePage.dash") }}
              </a-descriptions-item>
              <a-descriptions-item :label="t('licensePage.labelMachineBinding')">
                <template v-if="!licenseStatus?.machineBound">
                  <a-tag color="default">{{ t("licensePage.machineUnbound") }}</a-tag>
                </template>
                <template v-else-if="licenseStatus?.machineMatched">
                  <a-tag color="green">{{ t("licensePage.machineBoundMatch") }}</a-tag>
                </template>
                <template v-else>
                  <a-tag color="red">{{ t("licensePage.machineBoundMismatch") }}</a-tag>
                </template>
              </a-descriptions-item>
            </a-descriptions>
          </a-card>
        </a-col>

        <!-- 机器码 -->
        <a-col :xs="24" :md="12">
          <a-card :title="t('licensePage.fingerprintCardTitle')" :loading="fingerprintLoading">
            <template #extra>
              <a-tooltip :title="t('licensePage.fingerprintTooltip')">
                <QuestionCircleOutlined />
              </a-tooltip>
            </template>
            <p style="font-size: 12px; color: #666; margin-bottom: 12px">
              {{ t("licensePage.fingerprintHint") }}
            </p>
            <a-input-group compact>
              <a-input
                :value="fingerprint"
                readonly
                style="flex: 1; font-family: monospace; font-size: 12px"
              />
              <a-button @click="copyFingerprint">
                <template #icon><CopyOutlined /></template>
                {{ t("licensePage.copy") }}
              </a-button>
            </a-input-group>
          </a-card>
        </a-col>
      </a-row>

      <!-- 功能限额 -->
      <a-row :gutter="[16, 16]" style="margin-top: 16px">
        <a-col :xs="24" :md="12">
          <a-card :title="t('licensePage.featureListTitle')">
            <a-list :data-source="featureList" size="small">
              <template #renderItem="{ item }">
                <a-list-item>
                  <span>{{ item.label }}</span>
                  <template #actions>
                    <a-tag :color="item.enabled ? 'green' : 'default'">
                      {{ item.enabled ? t("licensePage.featureOn") : t("licensePage.featureOff") }}
                    </a-tag>
                  </template>
                </a-list-item>
              </template>
            </a-list>
          </a-card>
        </a-col>

        <a-col :xs="24" :md="12">
          <a-card :title="t('licensePage.limitListTitle')">
            <a-list :data-source="limitList" size="small">
              <template #renderItem="{ item }">
                <a-list-item>
                  <span>{{ item.label }}</span>
                  <template #actions>
                    <span style="font-weight: 600">
                      {{ item.value === -1 ? t("licensePage.unlimited") : item.value }}
                    </span>
                  </template>
                </a-list-item>
              </template>
            </a-list>
          </a-card>
        </a-col>
      </a-row>

      <!-- 激活/续签证书 -->
      <a-card :title="t('licensePage.activateCardTitle')" style="margin-top: 16px">
        <a-alert
          v-if="activateResult"
          :type="activateResult.success ? 'success' : 'error'"
          :message="activateResult.message"
          closable
          style="margin-bottom: 16px"
          @close="activateResult = null"
        />
        <p style="color: #666; margin-bottom: 12px">
          {{ t("licensePage.activateHint") }}
        </p>
        <a-upload
          :before-upload="handleFileSelect"
          :show-upload-list="false"
          accept=".atlaslicense,.lic,.txt"
        >
          <a-button :loading="activating">
            <template #icon><UploadOutlined /></template>
            {{ t("licensePage.selectLicenseFile") }}
          </a-button>
        </a-upload>
      </a-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";

import { message } from "ant-design-vue";
import {
  QuestionCircleOutlined,
  CopyOutlined,
  UploadOutlined
} from "@ant-design/icons-vue";
import type { LicenseStatus } from "@atlas/shared-core";
import { getLicenseStatus, getMachineFingerprint, activateLicense } from "@/services/api-license";

interface LicenseApiError extends Error {
  payload?: {
    message?: string
  } | null
}

const { t, locale } = useI18n();

const loading = ref(false);
const fingerprintLoading = ref(false);
const activating = ref(false);
const licenseStatus = ref<LicenseStatus | null>(null);
const fingerprint = ref("");
const activateResult = ref<{ success: boolean; message: string } | null>(null);

const statusColor = computed(() => {
  switch (licenseStatus.value?.status) {
    case "Active": return "green";
    case "Expired": return "red";
    case "Invalid": return "red";
    default: return "default";
  }
});

const statusLabel = computed(() => {
  switch (licenseStatus.value?.status) {
    case "Active": return t("licensePage.statusActive");
    case "Expired": return t("licensePage.statusExpired");
    case "Invalid": return t("licensePage.statusInvalid");
    default: return t("licensePage.statusInactive");
  }
});

const editionColor = computed(() => {
  switch (licenseStatus.value?.edition) {
    case "Enterprise": return "purple";
    case "Pro": return "blue";
    default: return "orange";
  }
});

const editionLabel = computed(() => {
  switch (licenseStatus.value?.edition) {
    case "Enterprise": return t("licensePage.editionEnterprise");
    case "Pro": return t("licensePage.editionPro");
    default: return t("licensePage.editionTrial");
  }
});

const remainingDaysColor = computed(() => {
  const days = licenseStatus.value?.remainingDays;
  if (days === null || days === undefined) return "default";
  if (days <= 7) return "red";
  if (days <= 30) return "orange";
  return "green";
});

const featureDefs = [
  { key: "lowCode", msgKey: "licensePage.featureLowCode" },
  { key: "workflow", msgKey: "licensePage.featureWorkflow" },
  { key: "approval", msgKey: "licensePage.featureApproval" },
  { key: "alert", msgKey: "licensePage.featureAlert" },
  { key: "offlineDeploy", msgKey: "licensePage.featureOfflineDeploy" },
  { key: "multiTenant", msgKey: "licensePage.featureMultiTenant" },
  { key: "audit", msgKey: "licensePage.featureAudit" }
] as const;

const limitDefs = [
  { key: "maxApps", msgKey: "licensePage.limitMaxApps" },
  { key: "maxUsers", msgKey: "licensePage.limitMaxUsers" },
  { key: "maxTenants", msgKey: "licensePage.limitMaxTenants" },
  { key: "maxDataSources", msgKey: "licensePage.limitMaxDataSources" },
  { key: "auditRetentionDays", msgKey: "licensePage.limitAuditRetentionDays" }
] as const;

const featureList = computed(() => {
  const features = licenseStatus.value?.features ?? {};
  return featureDefs.map(({ key, msgKey }) => ({
    key,
    label: t(msgKey),
    enabled: features[key as keyof typeof features] === true
  }));
});

const limitList = computed(() => {
  const limits = licenseStatus.value?.limits ?? {};
  return limitDefs.map(({ key, msgKey }) => ({
    key,
    label: t(msgKey),
    value: limits[key as keyof typeof limits] ?? 0
  }));
});

function formatDate(dateStr: string): string {
  const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
  try {
    return new Date(dateStr).toLocaleDateString(loc, {
      year: "numeric",
      month: "2-digit",
      day: "2-digit"
    });
  } catch {
    return dateStr;
  }
}

async function loadStatus() {
  loading.value = true;
  try {
    licenseStatus.value = await getLicenseStatus();
  } catch (error) {
    const requestError = error as LicenseApiError;
    message.error(requestError?.payload?.message ?? requestError?.message ?? t("licensePage.loadStatusFailed"));
  } finally {
    loading.value = false;
  }
}

async function loadFingerprint() {
  fingerprintLoading.value = true;
  try {
    fingerprint.value = await getMachineFingerprint();
  } catch {
    // 忽略错误，机器码不影响主功能
  } finally {
    fingerprintLoading.value = false;
  }
}

async function copyFingerprint() {
  if (!fingerprint.value) return;
  try {
    await navigator.clipboard.writeText(fingerprint.value);
    message.success(t("licensePage.fingerprintCopied"));
  } catch {
    message.error(t("licensePage.copyFailed"));
  }
}

async function handleFileSelect(file: File): Promise<false> {
  activating.value = true;
  activateResult.value = null;

  let content = "";
  try {
    content = await readFileAsText(file);
  } catch (error) {
    activateResult.value = {
      success: false,
      message: error instanceof Error ? error.message : t("licensePage.fileReadFailed")
    };
    activating.value = false;
    return false;
  }

  try {
    const resp = await activateLicense(content);
    if (resp.success) {
      activateResult.value = {
        success: true,
        message: resp.data?.message ?? resp.message ?? t("licensePage.activateSuccessDefault")
      };
      await loadStatus();
    } else {
      activateResult.value = { success: false, message: resp.message || t("licensePage.activateFailedDefault") };
    }
  } catch (error) {
    const requestError = error as LicenseApiError;
    const detailMessage =
      requestError?.payload?.message ??
      (error instanceof Error ? error.message : "");
    activateResult.value = {
      success: false,
      message: detailMessage || t("licensePage.activateFailedRetry")
    };
  } finally {
    activating.value = false;
  }
  return false;
}

function readFileAsText(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (e) => resolve((e.target?.result as string) ?? "");
    reader.onerror = () => reject(new Error(t("licensePage.fileReadFailed")));
    reader.readAsText(file);
  });
}

onMounted(async () => {
  await Promise.all([loadStatus(), loadFingerprint()]);
});
</script>

<style scoped>
.license-page {
  padding: 24px;
  min-height: 100%;
}

.license-content {
  max-width: 1200px;
}
</style>
