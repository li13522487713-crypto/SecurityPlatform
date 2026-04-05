<template>
  <div class="webhooks-page">
    <a-page-header :title="t('systemWebhooks.pageTitle')" :sub-title="t('systemWebhooks.pageSubtitle')" />

    <a-card :bordered="false">
      <template #extra>
        <a-button type="primary" @click="openCreate">
          <PlusOutlined />{{ t("systemWebhooks.createWebhook") }}
        </a-button>
      </template>

      <a-table
        :columns="columns"
        :data-source="webhooks"
        :loading="loading"
        row-key="id"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'isActive'">
            <a-badge
              :status="record.isActive ? 'success' : 'default'"
              :text="record.isActive ? t('systemWebhooks.enabled') : t('systemWebhooks.disabled')"
            />
          </template>
          <template v-if="column.key === 'eventTypes'">
            <a-tag v-for="eventType in record.eventTypes" :key="eventType">{{ eventType }}</a-tag>
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button size="small" type="link" @click="openDeliveries(record)">{{ t("systemWebhooks.deliveriesLog") }}</a-button>
              <a-button size="small" type="link" @click="handleTest(record.id)">{{ t("systemWebhooks.test") }}</a-button>
              <a-button size="small" type="link" @click="openEdit(record)">{{ t("systemWebhooks.edit") }}</a-button>
              <a-popconfirm :title="t('systemWebhooks.deleteConfirm')" @confirm="handleDelete(record.id)">
                <a-button size="small" type="link" danger>{{ t("common.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="modalOpen"
      :title="editItem ? t('systemWebhooks.modalEditTitle') : t('systemWebhooks.modalCreateTitle')"
      :confirm-loading="saving"
      @ok="handleSave"
    >
      <a-form :model="form" layout="vertical">
        <a-form-item :label="t('systemWebhooks.nameLabel')" required>
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item :label="t('systemWebhooks.urlLabel')" required>
          <a-input v-model:value="form.targetUrl" :placeholder="t('systemWebhooks.urlPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemWebhooks.secretLabel')">
          <a-input v-model:value="form.secret" :placeholder="t('systemWebhooks.secretPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemWebhooks.eventsLabel')">
          <a-select
            v-model:value="form.eventTypes"
            mode="tags"
            :placeholder="t('systemWebhooks.eventsPlaceholder')"
          >
            <a-select-option value="approval.completed">approval.completed</a-select-option>
            <a-select-option value="approval.rejected">approval.rejected</a-select-option>
            <a-select-option value="approval.started">approval.started</a-select-option>
            <a-select-option value="*">{{ t("systemWebhooks.eventAllOption") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item v-if="editItem" :label="t('systemWebhooks.statusLabel')">
          <a-switch
            v-model:checked="form.isActive"
            :checked-children="t('common.statusEnabled')"
            :un-checked-children="t('common.statusDisabled')"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-drawer
      v-if="deliveriesWebhook"
      :title="t('systemWebhooks.deliveriesDrawerTitle', { name: deliveriesWebhook.name })"
      width="640"
      :open="true"
      @close="deliveriesWebhook = null"
    >
      <a-list
        :data-source="deliveries"
        :loading="deliveriesLoading"
        item-layout="horizontal"
      >
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta>
              <template #title>
                <span>
                  <a-tag :color="item.success ? 'green' : 'red'">{{ item.responseCode ?? "ERR" }}</a-tag>
                  {{ item.eventType }}
                  <span class="log-duration">{{ item.durationMs }}ms</span>
                </span>
              </template>
              <template #description>
                <span>{{ formatDate(item.createdAt) }}</span>
                <span v-if="item.errorMessage" class="log-error"> — {{ item.errorMessage }}</span>
              </template>
            </a-list-item-meta>
          </a-list-item>
        </template>
      </a-list>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import { PlusOutlined } from "@ant-design/icons-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import {
  createWebhook,
  deleteWebhook,
  getWebhookDeliveries,
  getWebhooks,
  testWebhookDelivery,
  updateWebhook
} from "@/services/api-webhook";
import type { WebhookDeliveryLog, WebhookSubscription } from "@/services/api-webhook";

const { t, locale } = useI18n();
const isMounted = ref(false);
const loading = ref(false);
const webhooks = ref<WebhookSubscription[]>([]);
const modalOpen = ref(false);
const saving = ref(false);
const editItem = ref<WebhookSubscription | null>(null);
const deliveriesWebhook = ref<WebhookSubscription | null>(null);
const deliveries = ref<WebhookDeliveryLog[]>([]);
const deliveriesLoading = ref(false);

const form = reactive({
  name: "",
  targetUrl: "",
  secret: "",
  eventTypes: [] as string[],
  isActive: true
});

function formatDate(value: string) {
  const currentLocale = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(value).toLocaleString(currentLocale);
}

const columns = computed(() => [
  { title: t("systemWebhooks.colName"), dataIndex: "name", key: "name" },
  { title: t("systemWebhooks.colCallbackUrl"), dataIndex: "targetUrl", key: "targetUrl", ellipsis: true },
  { title: t("systemWebhooks.colSubscribedEvents"), key: "eventTypes" },
  { title: t("systemWebhooks.colStatus"), key: "isActive", width: 80 },
  {
    title: t("systemWebhooks.colLastTriggered"),
    dataIndex: "lastTriggeredAt",
    key: "lastTriggeredAt",
    customRender: ({ value }: { value: string }) => (value ? formatDate(value) : t("systemWebhooks.dash"))
  },
  { title: t("systemWebhooks.colActions"), key: "actions", width: 200 }
]);

async function fetchWebhooks() {
  loading.value = true;
  try {
    const response = await getWebhooks();
    if (!isMounted.value) return;
    if (response.success) {
      webhooks.value = response.data ?? [];
    }
  } catch (error: unknown) {
    if (!isMounted.value) return;
    webhooks.value = [];
    const status = (error as { status?: number }).status;
    if (status !== 401) {
      message.error(t("systemWebhooks.loadListFailed"));
    }
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editItem.value = null;
  Object.assign(form, { name: "", targetUrl: "", secret: "", eventTypes: [], isActive: true });
  modalOpen.value = true;
}

function openEdit(item: WebhookSubscription) {
  editItem.value = item;
  Object.assign(form, {
    name: item.name,
    targetUrl: item.targetUrl,
    secret: item.secret,
    eventTypes: [...item.eventTypes],
    isActive: item.isActive
  });
  modalOpen.value = true;
}

async function handleSave() {
  saving.value = true;
  try {
    if (editItem.value) {
      await updateWebhook(editItem.value.id, {
        name: form.name,
        targetUrl: form.targetUrl,
        eventTypes: form.eventTypes,
        isActive: form.isActive
      });
    } else {
      await createWebhook({
        name: form.name,
        targetUrl: form.targetUrl,
        secret: form.secret,
        eventTypes: form.eventTypes
      });
    }

    if (!isMounted.value) return;
    message.success(t("systemWebhooks.saveSuccess"));
    modalOpen.value = false;
    void fetchWebhooks();
  } finally {
    saving.value = false;
  }
}

async function handleDelete(id: number) {
  await deleteWebhook(id);
  if (!isMounted.value) return;
  message.success(t("systemWebhooks.deletedSuccess"));
  void fetchWebhooks();
}

async function handleTest(id: number) {
  await testWebhookDelivery(id);
  if (!isMounted.value) return;
  message.success(t("systemWebhooks.testSent"));
}

async function openDeliveries(webhook: WebhookSubscription) {
  deliveriesWebhook.value = webhook;
  deliveriesLoading.value = true;
  try {
    const response = await getWebhookDeliveries(webhook.id);
    if (!isMounted.value) return;
    if (response.success) {
      deliveries.value = response.data ?? [];
    }
  } finally {
    deliveriesLoading.value = false;
  }
}

onMounted(() => {
  isMounted.value = true;
  void fetchWebhooks();
});

onUnmounted(() => {
  isMounted.value = false;
});
</script>

<style scoped>
.webhooks-page {
  padding: 0 24px 24px;
}

.log-duration {
  margin-left: 8px;
  color: #8c8c8c;
  font-size: 12px;
}

.log-error {
  color: #ff4d4f;
}
</style>
