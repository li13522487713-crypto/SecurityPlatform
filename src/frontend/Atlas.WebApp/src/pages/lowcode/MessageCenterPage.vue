<template>
  <div class="message-center-page">
    <a-tabs v-model:active-key="activeTab">
      <a-tab-pane key="templates" :tab="t('lowcode.messageCenter.tabTemplates')">
        <div class="tab-header">
          <a-input-search
            v-model:value="templateKeyword"
            :placeholder="t('lowcode.messageCenter.phSearchTpl')"
            allow-clear
            style="width: 260px"
            @search="fetchTemplates"
          />
          <a-button type="primary" @click="handleCreateTemplate">{{ t("lowcode.messageCenter.newTemplate") }}</a-button>
        </div>
        <a-table
          :columns="templateColumns"
          :data-source="templates"
          :pagination="templatePagination"
          :loading="templateLoading"
          row-key="id"
          @change="onTemplatePagChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'channel'">
              <a-tag>{{ channelLabel(record.channel) }}</a-tag>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-space>
                <a-button type="link" @click="handleEditTemplate(record)">{{ t("lowcode.messageCenter.edit") }}</a-button>
                <a-popconfirm :title="t('lowcode.messageCenter.confirmDelete')" @confirm="deleteTemplate(record.id)">
                  <a-button type="link" danger>{{ t("lowcode.messageCenter.delete") }}</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="records" :tab="t('lowcode.messageCenter.tabRecords')">
        <a-table
          :columns="recordColumns"
          :data-source="records"
          :pagination="recordPagination"
          :loading="recordLoading"
          row-key="id"
          @change="onRecordPagChange"
        >
          <template #bodyCell="{ column, record: row }">
            <template v-if="column.key === 'status'">
              <a-tag
                :color="row.status === 'Sent' ? 'green' : row.status === 'Failed' ? 'red' : 'default'"
              >{{ row.status }}</a-tag>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="channels" :tab="t('lowcode.messageCenter.tabChannels')">
        <div class="tab-header">
          <span style="color: #999">{{ t("lowcode.messageCenter.channelHint") }}</span>
          <a-button type="primary" @click="handleCreateChannel">{{ t("lowcode.messageCenter.newChannel") }}</a-button>
        </div>
        <a-table
          :columns="channelColumns"
          :data-source="channels"
          :loading="channelLoading"
          row-key="id"
          :pagination="false"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'isEnabled'">
              <a-tag :color="record.isActive ? 'green' : 'default'">{{
                record.isActive ? t("lowcode.messageCenter.enabled") : t("lowcode.messageCenter.disabled")
              }}</a-tag>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-button type="link" @click="handleEditChannel(record)">{{ t("lowcode.messageCenter.edit") }}</a-button>
            </template>
          </template>
        </a-table>
      </a-tab-pane>
    </a-tabs>

    <a-modal
      v-model:open="templateModalVisible"
      :title="editingTemplateId ? t('lowcode.messageCenter.modalTplTitleEdit') : t('lowcode.messageCenter.modalTplTitleNew')"
      :ok-text="t('lowcode.messageCenter.ok')"
      :cancel-text="t('lowcode.messageCenter.cancel')"
      width="640px"
      @ok="submitTemplate"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('lowcode.messageCenter.labelTplName')" required>
          <a-input v-model:value="templateForm.name" />
        </a-form-item>
        <a-form-item :label="t('lowcode.messageCenter.labelChannel')" required>
          <a-select v-model:value="templateForm.channel">
            <a-select-option value="InApp">{{ t("lowcode.messageCenter.chInApp") }}</a-select-option>
            <a-select-option value="Email">{{ t("lowcode.messageCenter.chEmail") }}</a-select-option>
            <a-select-option value="Sms">{{ t("lowcode.messageCenter.chSms") }}</a-select-option>
            <a-select-option value="Webhook">{{ t("lowcode.messageCenter.chShortWebhook") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcode.messageCenter.labelEventType')">
          <a-input v-model:value="templateForm.eventType" :placeholder="t('lowcode.messageCenter.phEvent')" />
        </a-form-item>
        <a-form-item :label="t('lowcode.messageCenter.labelSubjectTpl')">
          <a-input v-model:value="templateForm.subjectTemplate" />
        </a-form-item>
        <a-form-item :label="t('lowcode.messageCenter.labelDesc')">
          <a-input v-model:value="templateForm.description" />
        </a-form-item>
        <a-form-item :label="t('lowcode.messageCenter.labelContentTpl')" required>
          <a-textarea
            v-model:value="templateForm.contentTemplate"
            :rows="5"
            :placeholder="t('lowcode.messageCenter.phContent')"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="channelModalVisible"
      :title="editingChannelId ? t('lowcode.messageCenter.modalChannelTitleEdit') : t('lowcode.messageCenter.modalChannelTitleNew')"
      :ok-text="t('lowcode.messageCenter.ok')"
      :cancel-text="t('lowcode.messageCenter.cancel')"
      width="640px"
      @ok="submitChannel"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('lowcode.messageCenter.labelChannelId')" required>
          <a-input v-model:value="channelForm.channel" :disabled="!!editingChannelId" />
        </a-form-item>
        <a-form-item :label="t('lowcode.messageCenter.labelChannelType')" required>
          <a-select v-model:value="channelForm.channel">
            <a-select-option value="InApp">{{ t("lowcode.messageCenter.chInApp") }}</a-select-option>
            <a-select-option value="Email">{{ t("lowcode.messageCenter.chEmail") }}</a-select-option>
            <a-select-option value="Sms">{{ t("lowcode.messageCenter.chSms") }}</a-select-option>
            <a-select-option value="Webhook">{{ t("lowcode.messageCenter.chShortWebhook") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('lowcode.messageCenter.labelConfigJson')">
          <a-textarea v-model:value="channelForm.configJson" :rows="6" placeholder='{"host":"smtp.example.com","port":465}' />
        </a-form-item>
        <a-form-item :label="t('lowcode.messageCenter.labelEnabled')">
          <a-switch v-model:checked="channelForm.isActive" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

const { t } = useI18n();

interface TemplateItem {
  id: string;
  name: string;
  channel: string;
  eventType?: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
}

interface TemplateDetail {
  id: string;
  name: string;
  channel: string;
  eventType?: string;
  contentTemplate: string;
  subjectTemplate?: string;
  description?: string;
}

interface RecordItem {
  id: string;
  channel: string;
  recipientAddress?: string;
  subject?: string;
  status: string;
  retryCount: number;
  createdAt: string;
  sentAt?: string;
}

interface ChannelItem {
  id: string;
  channel: string;
  configJson: string;
  isActive: boolean;
  updatedAt: string;
}

const isMounted = ref(false);
onUnmounted(() => {
  isMounted.value = false;
});

const activeTab = ref("templates");

const templateKeyword = ref("");
const templateLoading = ref(false);
const templates = ref<TemplateItem[]>([]);
const templatePagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 10, total: 0 });
const templateModalVisible = ref(false);
const editingTemplateId = ref<string | null>(null);
const templateForm = reactive({
  name: "",
  channel: "InApp",
  eventType: "",
  subjectTemplate: "",
  description: "",
  contentTemplate: ""
});

const templateColumns = computed(() => [
  { title: t("lowcode.messageCenter.colName"), dataIndex: "name", key: "name" },
  { title: t("lowcode.messageCenter.colEvent"), dataIndex: "eventType", key: "eventType", width: 160 },
  { title: t("lowcode.messageCenter.colChannel"), key: "channel", width: 120 },
  { title: t("lowcode.messageCenter.colCreated"), dataIndex: "createdAt", key: "createdAt", width: 180 },
  { title: t("lowcode.messageCenter.colActions"), key: "actions", width: 140 }
]);

const channelLabel = (ch: string) => {
  const map: Record<string, string> = {
    InApp: t("lowcode.messageCenter.chShortInApp"),
    Email: t("lowcode.messageCenter.chShortEmail"),
    Sms: t("lowcode.messageCenter.chShortSms"),
    Webhook: t("lowcode.messageCenter.chShortWebhook")
  };
  return map[ch] ?? ch;
};

const fetchTemplates = async () => {
  templateLoading.value = true;
  try {
    const q = new URLSearchParams({
      pageIndex: (templatePagination.current ?? 1).toString(),
      pageSize: (templatePagination.pageSize ?? 10).toString(),
      keyword: templateKeyword.value
    });
    const resp = await requestApi<ApiResponse<PagedResult<TemplateItem>>>(`/messages/templates?${q}`);

    if (!isMounted.value) return;
    if (resp.data) {
      templates.value = resp.data.items;
      templatePagination.total = resp.data.total;
    }
  } catch (error) {
    message.error((error as Error).message);
  } finally {
    templateLoading.value = false;
  }
};

const onTemplatePagChange = (pager: TablePaginationConfig) => {
  templatePagination.current = pager.current;
  templatePagination.pageSize = pager.pageSize;
  void fetchTemplates();
};

const handleCreateTemplate = () => {
  editingTemplateId.value = null;
  Object.assign(templateForm, {
    name: "",
    channel: "InApp",
    eventType: "",
    subjectTemplate: "",
    description: "",
    contentTemplate: ""
  });
  templateModalVisible.value = true;
};

const handleEditTemplate = async (record: TemplateItem) => {
  editingTemplateId.value = record.id;
  try {
    const resp = await requestApi<ApiResponse<TemplateDetail>>(`/messages/templates/${record.id}`);

    if (!isMounted.value) return;
    const detail = resp.data;
    if (!detail) {
      throw new Error(resp.message || t("lowcode.messageCenter.loadTplDetailFailed"));
    }

    Object.assign(templateForm, {
      name: detail.name,
      channel: detail.channel,
      eventType: detail.eventType ?? "",
      subjectTemplate: detail.subjectTemplate ?? "",
      description: detail.description ?? "",
      contentTemplate: detail.contentTemplate
    });
    templateModalVisible.value = true;
  } catch (error) {
    message.error((error as Error).message);
  }
};

const submitTemplate = async () => {
  if (!templateForm.name || !templateForm.contentTemplate) {
    message.warning(t("lowcode.messageCenter.warnRequired"));
    return;
  }

  const payload = {
    name: templateForm.name.trim(),
    channel: templateForm.channel,
    eventType: templateForm.eventType.trim() || null,
    contentTemplate: templateForm.contentTemplate,
    subjectTemplate: templateForm.subjectTemplate.trim() || null,
    description: templateForm.description.trim() || null
  };

  try {
    if (editingTemplateId.value) {
      await requestApi(`/messages/templates/${editingTemplateId.value}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      if (!isMounted.value) return;
    } else {
      await requestApi("/messages/templates", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      if (!isMounted.value) return;
    }
    templateModalVisible.value = false;
    message.success(t("lowcode.messageCenter.opOk"));
    await fetchTemplates();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message);
  }
};

const deleteTemplate = async (id: string) => {
  try {
    await requestApi(`/messages/templates/${id}`, { method: "DELETE" });

    if (!isMounted.value) return;
    message.success(t("lowcode.messageCenter.deleted"));
    await fetchTemplates();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message);
  }
};

const recordLoading = ref(false);
const records = ref<RecordItem[]>([]);
const recordPagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 10, total: 0 });
const recordColumns = computed(() => [
  { title: t("lowcode.messageCenter.colSubject"), dataIndex: "subject", key: "subject" },
  { title: t("lowcode.messageCenter.colChannel"), dataIndex: "channel", key: "channel", width: 100 },
  { title: t("lowcode.messageCenter.colRecipient"), dataIndex: "recipientAddress", key: "recipientAddress", width: 200 },
  { title: t("lowcode.messageCenter.colStatus"), key: "status", width: 100 },
  { title: t("lowcode.messageCenter.colSent"), dataIndex: "sentAt", key: "sentAt", width: 180 },
  { title: t("lowcode.messageCenter.colCreated"), dataIndex: "createdAt", key: "createdAt", width: 180 }
]);

const fetchRecords = async () => {
  recordLoading.value = true;
  try {
    const q = new URLSearchParams({
      pageIndex: (recordPagination.current ?? 1).toString(),
      pageSize: (recordPagination.pageSize ?? 10).toString()
    });
    const resp = await requestApi<ApiResponse<PagedResult<RecordItem>>>(`/messages/records?${q}`);

    if (!isMounted.value) return;
    if (resp.data) {
      records.value = resp.data.items;
      recordPagination.total = resp.data.total;
    }
  } catch (error) {
    message.error((error as Error).message);
  } finally {
    recordLoading.value = false;
  }
};

const onRecordPagChange = (pager: TablePaginationConfig) => {
  recordPagination.current = pager.current;
  recordPagination.pageSize = pager.pageSize;
  void fetchRecords();
};

const channelLoading = ref(false);
const channels = ref<ChannelItem[]>([]);
const channelModalVisible = ref(false);
const editingChannelId = ref<string | null>(null);
const channelForm = reactive({ channel: "InApp", configJson: "{}", isActive: true });
const channelColumns = computed(() => [
  { title: t("lowcode.messageCenter.colChannel"), dataIndex: "channel", key: "channel", width: 140 },
  { title: t("lowcode.messageCenter.colConfig"), dataIndex: "configJson", key: "configJson", ellipsis: true },
  { title: t("lowcode.messageCenter.colStatus"), key: "isEnabled", width: 100 },
  { title: t("lowcode.messageCenter.colUpdated"), dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: t("lowcode.messageCenter.colActions"), key: "actions", width: 100 }
]);

const fetchChannels = async () => {
  channelLoading.value = true;
  try {
    const resp = await requestApi<ApiResponse<ChannelItem[]>>("/messages/channels");

    if (!isMounted.value) return;
    if (resp.data) {
      channels.value = resp.data;
    }
  } catch (error) {
    message.error((error as Error).message);
  } finally {
    channelLoading.value = false;
  }
};

const handleCreateChannel = () => {
  editingChannelId.value = null;
  Object.assign(channelForm, { channel: "InApp", configJson: "{}", isActive: true });
  channelModalVisible.value = true;
};

const handleEditChannel = (record: ChannelItem) => {
  editingChannelId.value = record.channel;
  Object.assign(channelForm, {
    channel: record.channel,
    configJson: record.configJson,
    isActive: record.isActive
  });
  channelModalVisible.value = true;
};

const submitChannel = async () => {
  if (!channelForm.channel) {
    message.warning(t("lowcode.messageCenter.warnChannel"));
    return;
  }

  const channel = (editingChannelId.value ?? channelForm.channel).trim();
  if (!channel) {
    message.warning(t("lowcode.messageCenter.warnChannelEmpty"));
    return;
  }

  try {
    await requestApi(`/messages/channels/${encodeURIComponent(channel)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        configJson: channelForm.configJson,
        isActive: channelForm.isActive
      })
    });

    if (!isMounted.value) return;
    channelModalVisible.value = false;
    message.success(t("lowcode.messageCenter.opOk"));
    await fetchChannels();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message);
  }
};

onMounted(() => {
  isMounted.value = true;
  void fetchTemplates();
  void fetchRecords();
  void fetchChannels();
});
</script>

<style scoped>
.message-center-page {
  padding: 24px;
}
.tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
</style>
