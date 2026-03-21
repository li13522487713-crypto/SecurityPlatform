<template>
  <div class="message-center-page">
    <a-tabs v-model:activeKey="activeTab">
      <a-tab-pane key="templates" tab="消息模板">
        <div class="tab-header">
          <a-input-search v-model:value="templateKeyword" placeholder="搜索模板" allow-clear style="width: 260px" @search="fetchTemplates" />
          <a-button type="primary" @click="handleCreateTemplate">新建模板</a-button>
        </div>
        <a-table :columns="templateColumns" :data-source="templates" :pagination="templatePagination" :loading="templateLoading" row-key="id" @change="onTemplatePagChange">
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'channel'">
              <a-tag>{{ channelLabel(record.channel) }}</a-tag>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-space>
                <a-button type="link" @click="handleEditTemplate(record)">编辑</a-button>
                <a-popconfirm title="确认删除？" @confirm="deleteTemplate(record.id)"><a-button type="link" danger>删除</a-button></a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="records" tab="发送记录">
        <a-table :columns="recordColumns" :data-source="records" :pagination="recordPagination" :loading="recordLoading" row-key="id" @change="onRecordPagChange">
          <template #bodyCell="{ column, record: row }">
            <template v-if="column.key === 'status'">
              <a-tag :color="row.status === 'Sent' ? 'green' : row.status === 'Failed' ? 'red' : 'default'">{{ row.status }}</a-tag>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="channels" tab="渠道配置">
        <div class="tab-header">
          <span style="color: #999">配置各渠道的连接参数</span>
          <a-button type="primary" @click="handleCreateChannel">新建渠道</a-button>
        </div>
        <a-table :columns="channelColumns" :data-source="channels" :loading="channelLoading" row-key="id" :pagination="false">
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'isEnabled'">
              <a-tag :color="record.isActive ? 'green' : 'default'">{{ record.isActive ? '已启用' : '已禁用' }}</a-tag>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-button type="link" @click="handleEditChannel(record)">编辑</a-button>
            </template>
          </template>
        </a-table>
      </a-tab-pane>
    </a-tabs>

    <!-- Template Modal -->
    <a-modal v-model:open="templateModalVisible" :title="editingTemplateId ? '编辑模板' : '新建模板'" ok-text="确定" cancel-text="取消" @ok="submitTemplate" width="640px">
      <a-form layout="vertical">
        <a-form-item label="模板名称" required><a-input v-model:value="templateForm.name" /></a-form-item>
        <a-form-item label="渠道" required>
          <a-select v-model:value="templateForm.channel">
            <a-select-option value="InApp">站内消息</a-select-option>
            <a-select-option value="Email">邮件</a-select-option>
            <a-select-option value="Sms">短信</a-select-option>
            <a-select-option value="Webhook">Webhook</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="事件类型"><a-input v-model:value="templateForm.eventType" placeholder="例如：approval.created" /></a-form-item>
        <a-form-item label="标题模板"><a-input v-model:value="templateForm.subjectTemplate" /></a-form-item>
        <a-form-item label="描述"><a-input v-model:value="templateForm.description" /></a-form-item>
        <a-form-item label="内容模板" required><a-textarea v-model:value="templateForm.contentTemplate" :rows="5" placeholder="支持 {{variable}} 占位符" /></a-form-item>
      </a-form>
    </a-modal>

    <!-- Channel Modal -->
    <a-modal v-model:open="channelModalVisible" :title="editingChannelId ? '编辑渠道' : '新建渠道'" ok-text="确定" cancel-text="取消" @ok="submitChannel" width="640px">
      <a-form layout="vertical">
        <a-form-item label="渠道标识" required><a-input v-model:value="channelForm.channel" :disabled="!!editingChannelId" /></a-form-item>
        <a-form-item label="渠道类型" required>
          <a-select v-model:value="channelForm.channel">
            <a-select-option value="InApp">站内消息</a-select-option>
            <a-select-option value="Email">邮件</a-select-option>
            <a-select-option value="Sms">短信</a-select-option>
            <a-select-option value="Webhook">Webhook</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="配置 JSON"><a-textarea v-model:value="channelForm.configJson" :rows="6" placeholder='{"host":"smtp.example.com","port":465}' /></a-form-item>
        <a-form-item label="启用"><a-switch v-model:checked="channelForm.isActive" /></a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

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

const activeTab = ref("templates");

// ---- Templates ----
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

const templateColumns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "事件类型", dataIndex: "eventType", key: "eventType", width: 160 },
  { title: "渠道", key: "channel", width: 120 },
  { title: "创建时间", dataIndex: "createdAt", key: "createdAt", width: 180 },
  { title: "操作", key: "actions", width: 140 }
];

const channelLabel = (ch: string) => ({ InApp: "站内", Email: "邮件", Sms: "短信", Webhook: "Webhook" }[ch] ?? ch);

const fetchTemplates = async () => {
  templateLoading.value = true;
  try {
    const q = new URLSearchParams({
      pageIndex: (templatePagination.current ?? 1).toString(),
      pageSize: (templatePagination.pageSize ?? 10).toString(),
      keyword: templateKeyword.value
    });
    const resp  = await requestApi<ApiResponse<PagedResult<TemplateItem>>>(`/messages/templates?${q}`);

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
    const resp  = await requestApi<ApiResponse<TemplateDetail>>(`/messages/templates/${record.id}`);

    if (!isMounted.value) return;
    const detail = resp.data;
    if (!detail) {
      throw new Error(resp.message || "加载模板详情失败");
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
    message.warning("请填写必填项");
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
    message.success("操作成功");
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
    message.success("已删除");
    await fetchTemplates();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message);
  }
};

// ---- Records ----
const recordLoading = ref(false);
const records = ref<RecordItem[]>([]);
const recordPagination = reactive<TablePaginationConfig>({ current: 1, pageSize: 10, total: 0 });
const recordColumns = [
  { title: "主题", dataIndex: "subject", key: "subject" },
  { title: "渠道", dataIndex: "channel", key: "channel", width: 100 },
  { title: "收件人", dataIndex: "recipientAddress", key: "recipientAddress", width: 200 },
  { title: "状态", key: "status", width: 100 },
  { title: "发送时间", dataIndex: "sentAt", key: "sentAt", width: 180 },
  { title: "创建时间", dataIndex: "createdAt", key: "createdAt", width: 180 }
];

const fetchRecords = async () => {
  recordLoading.value = true;
  try {
    const q = new URLSearchParams({
      pageIndex: (recordPagination.current ?? 1).toString(),
      pageSize: (recordPagination.pageSize ?? 10).toString()
    });
    const resp  = await requestApi<ApiResponse<PagedResult<RecordItem>>>(`/messages/records?${q}`);

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

// ---- Channels ----
const channelLoading = ref(false);
const channels = ref<ChannelItem[]>([]);
const channelModalVisible = ref(false);
const editingChannelId = ref<string | null>(null); // 存储 channel 值
const channelForm = reactive({ channel: "InApp", configJson: "{}", isActive: true });
const channelColumns = [
  { title: "渠道", dataIndex: "channel", key: "channel", width: 140 },
  { title: "配置", dataIndex: "configJson", key: "configJson", ellipsis: true },
  { title: "状态", key: "isEnabled", width: 100 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: "操作", key: "actions", width: 100 }
];

const fetchChannels = async () => {
  channelLoading.value = true;
  try {
    const resp  = await requestApi<ApiResponse<ChannelItem[]>>("/messages/channels");

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
    message.warning("请填写渠道");
    return;
  }

  const channel = (editingChannelId.value ?? channelForm.channel).trim();
  if (!channel) {
    message.warning("渠道不能为空");
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
    message.success("操作成功");
    await fetchChannels();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message);
  }
};

onMounted(() => {
  void fetchTemplates();
  void fetchRecords();
  void fetchChannels();
});
</script>

<style scoped>
.message-center-page { padding: 24px; }
.tab-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
</style>
