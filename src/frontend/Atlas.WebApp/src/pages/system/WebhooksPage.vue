<template>
  <div class="webhooks-page">
    <a-page-header title="Webhook 管理" subtitle="注册外部回调，订阅平台事件" />

    <a-card :bordered="false">
      <template #extra>
        <a-button type="primary" @click="openCreate">
          <PlusOutlined />新建 Webhook
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
            <a-badge :status="record.isActive ? 'success' : 'default'" :text="record.isActive ? '启用' : '停用'" />
          </template>
          <template v-if="column.key === 'eventTypes'">
            <a-tag v-for="et in record.eventTypes" :key="et">{{ et }}</a-tag>
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button size="small" type="link" @click="openDeliveries(record)">投递日志</a-button>
              <a-button size="small" type="link" @click="handleTest(record.id)">测试</a-button>
              <a-button size="small" type="link" @click="openEdit(record)">编辑</a-button>
              <a-popconfirm title="确认删除?" @confirm="handleDelete(record.id)">
                <a-button size="small" type="link" danger>删除</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 创建/编辑 Modal -->
    <a-modal
      v-model:open="modalOpen"
      :title="editItem ? '编辑 Webhook' : '新建 Webhook'"
      :confirm-loading="saving"
      @ok="handleSave"
    >
      <a-form :model="form" layout="vertical">
        <a-form-item label="名称" required>
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="回调 URL" required>
          <a-input v-model:value="form.targetUrl" placeholder="https://..." />
        </a-form-item>
        <a-form-item label="签名密钥">
          <a-input v-model:value="form.secret" placeholder="HMAC-SHA256 密钥" />
        </a-form-item>
        <a-form-item label="订阅事件（多选）">
          <a-select
            v-model:value="form.eventTypes"
            mode="tags"
            placeholder="输入或选择事件类型，如 approval.completed"
          >
            <a-select-option value="approval.completed">approval.completed</a-select-option>
            <a-select-option value="approval.rejected">approval.rejected</a-select-option>
            <a-select-option value="approval.started">approval.started</a-select-option>
            <a-select-option value="*">* (全部)</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item v-if="editItem" label="状态">
          <a-switch v-model:checked="form.isActive" checked-children="启用" un-checked-children="停用" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 投递日志 Drawer -->
    <a-drawer
      v-if="deliveriesWebhook"
      :title="`投递日志 — ${deliveriesWebhook.name}`"
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
                  <a-tag :color="item.success ? 'green' : 'red'">{{ item.responseCode ?? 'ERR' }}</a-tag>
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
import { ref, onMounted, reactive, onUnmounted } from 'vue'

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { PlusOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import {
  getWebhooks,
  createWebhook,
  updateWebhook,
  deleteWebhook,
  getWebhookDeliveries,
  testWebhookDelivery,
} from '@/services/api-webhook'
import type { WebhookSubscription, WebhookDeliveryLog } from '@/services/api-webhook'

const loading = ref(false)
const webhooks = ref<WebhookSubscription[]>([])
const modalOpen = ref(false)
const saving = ref(false)
const editItem = ref<WebhookSubscription | null>(null)
const deliveriesWebhook = ref<WebhookSubscription | null>(null)
const deliveries = ref<WebhookDeliveryLog[]>([])
const deliveriesLoading = ref(false)

const form = reactive({
  name: '',
  targetUrl: '',
  secret: '',
  eventTypes: [] as string[],
  isActive: true,
})

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name' },
  { title: '回调 URL', dataIndex: 'targetUrl', key: 'targetUrl', ellipsis: true },
  { title: '订阅事件', key: 'eventTypes' },
  { title: '状态', key: 'isActive', width: 80 },
  { title: '最后触发', dataIndex: 'lastTriggeredAt', key: 'lastTriggeredAt', customRender: ({ value }: { value: string }) => value ? formatDate(value) : '—' },
  { title: '操作', key: 'actions', width: 200 },
]

async function fetchWebhooks() {
  loading.value = true
  try {
    const res = await getWebhooks()
    if (res.success) webhooks.value = res.data ?? []
  } catch (error) {
    webhooks.value = []
    const status = (error as { status?: number })?.status
    if (status !== 401) {
      message.error('加载 Webhook 列表失败')
    }
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editItem.value = null
  Object.assign(form, { name: '', targetUrl: '', secret: '', eventTypes: [], isActive: true })
  modalOpen.value = true
}

function openEdit(item: WebhookSubscription) {
  editItem.value = item
  Object.assign(form, {
    name: item.name,
    targetUrl: item.targetUrl,
    secret: item.secret,
    eventTypes: [...item.eventTypes],
    isActive: item.isActive,
  })
  modalOpen.value = true
}

async function handleSave() {
  saving.value = true
  try {
    if (editItem.value) {
      await updateWebhook(editItem.value.id, {
        name: form.name,
        targetUrl: form.targetUrl,
        eventTypes: form.eventTypes,
        isActive: form.isActive,
      })
    } else {
      await createWebhook({
        name: form.name,
        targetUrl: form.targetUrl,
        secret: form.secret,
        eventTypes: form.eventTypes,
      })
    }
    message.success('保存成功')
    modalOpen.value = false
    fetchWebhooks()
  } finally {
    saving.value = false
  }
}

async function handleDelete(id: number) {
  await deleteWebhook(id)
  message.success('已删除')
  fetchWebhooks()
}

async function handleTest(id: number) {
  await testWebhookDelivery(id)
  message.success('测试请求已发送')
}

async function openDeliveries(webhook: WebhookSubscription) {
  deliveriesWebhook.value = webhook
  deliveriesLoading.value = true
  try {
    const res = await getWebhookDeliveries(webhook.id)
    if (res.success) deliveries.value = res.data ?? []
  } finally {
    deliveriesLoading.value = false
  }
}

function formatDate(d: string) {
  return new Date(d).toLocaleString('zh-CN')
}

onMounted(fetchWebhooks)
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
