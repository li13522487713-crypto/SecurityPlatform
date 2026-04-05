<template>
  <div class="app-settings">
    <a-page-header :title="t('settings.pageTitle')" />

    <a-spin :spinning="loading">
      <a-alert
        v-if="loadError"
        type="error"
        show-icon
        :message="loadError"
        closable
        style="margin-bottom: 16px"
      />

      <a-row :gutter="[16, 16]">
        <a-col :span="24" :lg="14">
          <a-card :title="t('settings.basicInfo')">
            <a-form layout="vertical" :model="form">
              <a-form-item :label="t('settings.appName')" required>
                <a-input v-model:value="form.name" :disabled="!canEdit" />
              </a-form-item>
              <a-form-item :label="t('settings.appDescription')">
                <a-textarea v-model:value="form.description" :rows="3" :disabled="!canEdit" />
              </a-form-item>
              <a-form-item :label="t('settings.appCategory')">
                <a-input v-model:value="form.category" :disabled="!canEdit" />
              </a-form-item>
              <a-form-item :label="t('settings.appIcon')">
                <a-input v-model:value="form.icon" :disabled="!canEdit" />
              </a-form-item>
              <a-form-item v-if="canEdit">
                <a-button type="primary" :loading="saving" @click="handleSave">
                  {{ t('settings.save') }}
                </a-button>
              </a-form-item>
            </a-form>
          </a-card>
        </a-col>

        <a-col :span="24" :lg="10">
          <a-card :title="t('settings.statusManagement')">
            <a-descriptions :column="1" bordered size="small">
              <a-descriptions-item :label="t('settings.currentStatus')">
                <a-tag :color="statusColor(detail?.status)">
                  {{ detail?.status ?? '—' }}
                </a-tag>
              </a-descriptions-item>
              <a-descriptions-item :label="t('settings.version')">
                {{ detail?.version ?? '—' }}
              </a-descriptions-item>
              <a-descriptions-item :label="t('settings.runtimeStatus')">
                <a-tag :color="statusColor(detail?.runtimeStatus)">
                  {{ detail?.runtimeStatus ?? '—' }}
                </a-tag>
              </a-descriptions-item>
              <a-descriptions-item :label="t('settings.healthStatus')">
                {{ detail?.healthStatus ?? '—' }}
              </a-descriptions-item>
            </a-descriptions>

            <a-divider />

            <a-space v-if="canEdit" wrap>
              <a-popconfirm :title="t('settings.confirmPublish')" @confirm="handlePublish">
                <a-button :loading="publishing">{{ t('settings.publish') }}</a-button>
              </a-popconfirm>
              <a-popconfirm :title="t('settings.confirmStart')" @confirm="handleStart">
                <a-button type="primary" :loading="starting">{{ t('settings.start') }}</a-button>
              </a-popconfirm>
              <a-popconfirm :title="t('settings.confirmStop')" @confirm="handleStop">
                <a-button danger :loading="stopping">{{ t('settings.stop') }}</a-button>
              </a-popconfirm>
              <a-popconfirm :title="t('settings.confirmRestart')" @confirm="handleRestart">
                <a-button :loading="restarting">{{ t('settings.restart') }}</a-button>
              </a-popconfirm>
            </a-space>
          </a-card>
        </a-col>
      </a-row>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, watch, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { useAppContext } from "@/composables/useAppContext";
import {
  getAppInstanceDetail,
  updateAppInstance,
  publishApp,
  startApp,
  stopApp,
  restartApp
} from "@/services/api-app-instance";
import type { TenantAppInstanceDetail } from "@/services/api-app-instance";
import { useAppUserStore } from "@/stores/user";

const { t } = useI18n();
const { appId } = useAppContext();
const userStore = useAppUserStore();

const loading = ref(false);
const loadError = ref("");
const detail = ref<TenantAppInstanceDetail | null>(null);
const saving = ref(false);
const publishing = ref(false);
const starting = ref(false);
const stopping = ref(false);
const restarting = ref(false);

const form = reactive({
  name: "",
  description: "",
  category: "",
  icon: ""
});

const canEdit = ref(true);

function checkPermission() {
  const perms = userStore.permissions;
  const profile = userStore.profile;
  if (profile?.isPlatformAdmin) { canEdit.value = true; return; }
  if (perms.includes("*:*:*")) { canEdit.value = true; return; }
  if (userStore.roles.some((r) => ["admin", "superadmin"].includes(r.toLowerCase()))) { canEdit.value = true; return; }
  canEdit.value = perms.includes("Permission:apps:update");
}

function statusColor(status?: string | null): string {
  if (!status) return "default";
  const s = status.toLowerCase();
  if (s === "running" || s === "published" || s === "healthy") return "green";
  if (s === "stopped" || s === "disabled" || s === "unhealthy") return "red";
  if (s === "draft" || s === "pending") return "orange";
  return "default";
}

async function loadDetail() {
  const id = appId.value;
  if (!id) return;

  loading.value = true;
  loadError.value = "";
  try {
    detail.value = await getAppInstanceDetail(id);
    form.name = detail.value.name ?? "";
    form.description = detail.value.description ?? "";
    form.category = detail.value.category ?? "";
    form.icon = detail.value.icon ?? "";
  } catch (e) {
    loadError.value = e instanceof Error ? e.message : t("settings.loadFailed");
  } finally {
    loading.value = false;
  }
}

async function handleSave() {
  const id = appId.value;
  if (!id || !form.name.trim()) {
    message.warning(t("settings.nameRequired"));
    return;
  }

  saving.value = true;
  try {
    await updateAppInstance(id, {
      name: form.name,
      description: form.description || undefined,
      category: form.category || undefined,
      icon: form.icon || undefined
    });
    message.success(t("settings.saveSuccess"));
    await loadDetail();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("settings.saveFailed"));
  } finally {
    saving.value = false;
  }
}

async function handlePublish() {
  const id = appId.value;
  if (!id) return;
  publishing.value = true;
  try {
    await publishApp(id);
    message.success(t("settings.publishSuccess"));
    await loadDetail();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("settings.publishFailed"));
  } finally {
    publishing.value = false;
  }
}

async function handleStart() {
  const id = appId.value;
  if (!id) return;
  starting.value = true;
  try {
    await startApp(id);
    message.success(t("settings.startSuccess"));
    await loadDetail();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("settings.startFailed"));
  } finally {
    starting.value = false;
  }
}

async function handleStop() {
  const id = appId.value;
  if (!id) return;
  stopping.value = true;
  try {
    await stopApp(id);
    message.success(t("settings.stopSuccess"));
    await loadDetail();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("settings.stopFailed"));
  } finally {
    stopping.value = false;
  }
}

async function handleRestart() {
  const id = appId.value;
  if (!id) return;
  restarting.value = true;
  try {
    await restartApp(id);
    message.success(t("settings.restartSuccess"));
    await loadDetail();
  } catch (e) {
    message.error(e instanceof Error ? e.message : t("settings.restartFailed"));
  } finally {
    restarting.value = false;
  }
}

watch(appId, (id) => {
  if (id) void loadDetail();
});

onMounted(() => {
  checkPermission();
  if (appId.value) void loadDetail();
});
</script>

<style scoped>
.app-settings {
  max-width: 1200px;
}
</style>
