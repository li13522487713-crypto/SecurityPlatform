<template>
  <div class="resource-governance">
    <a-page-header :title="t('logicFlow.governance.title')" @back="$router.back()" />

    <a-card :bordered="false">
      <a-tabs v-model:activeKey="activeTab" @change="onTabChange">
        <a-tab-pane key="quotas" :tab="t('logicFlow.governance.tabs.quotas')">
          <a-space style="margin-bottom: 12px">
            <a-button @click="loadQuotas">{{ t('batchProcess.common.refresh') }}</a-button>
          </a-space>
          <a-table
            :columns="quotaColumns"
            :data-source="quotas"
            :loading="quotasLoading"
            row-key="resourceType"
            :pagination="false"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'action'">
                <a-button type="link" size="small" @click="openConsume(record)">
                  {{ t('logicFlow.governance.consume') }}
                </a-button>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="canary" :tab="t('logicFlow.governance.tabs.canary')">
          <a-space style="margin-bottom: 12px">
            <a-button @click="loadCanary">{{ t('batchProcess.common.refresh') }}</a-button>
          </a-space>
          <a-table :columns="canaryColumns" :data-source="canaryRows" :loading="canaryLoading" row-key="featureKey" :pagination="false">
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'pct'">
                <a-slider
                  :value="record.rolloutPercentage"
                  :min="0"
                  :max="100"
                  style="min-width: 160px"
                  @afterChange="(v: number) => onCanaryAfterChange(record.featureKey, v)"
                />
              </template>
              <template v-if="column.key === 'active'">
                <a-tag :color="record.isActive ? 'green' : 'default'">
                  {{ record.isActive ? t('logicFlow.governance.active') : t('logicFlow.governance.inactive') }}
                </a-tag>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="freeze" :tab="t('logicFlow.governance.tabs.freeze')">
          <a-form layout="vertical" class="freeze-form" @submit.prevent="submitFreeze">
            <a-row :gutter="16">
              <a-col :span="8">
                <a-form-item :label="t('logicFlow.governance.resourceType')">
                  <a-input v-model:value="freezeForm.resourceType" />
                </a-form-item>
              </a-col>
              <a-col :span="8">
                <a-form-item :label="t('logicFlow.governance.resourceId')">
                  <a-input-number v-model:value="freezeForm.resourceId" :min="1" style="width: 100%" />
                </a-form-item>
              </a-col>
              <a-col :span="8">
                <a-form-item :label="t('logicFlow.governance.reason')">
                  <a-input v-model:value="freezeForm.reason" />
                </a-form-item>
              </a-col>
            </a-row>
            <a-button type="primary" html-type="submit" :loading="freezeSubmitting">
              {{ t('logicFlow.governance.freeze') }}
            </a-button>
          </a-form>
          <a-divider />
          <a-space style="margin-bottom: 12px">
            <a-button @click="loadFreezes">{{ t('batchProcess.common.refresh') }}</a-button>
          </a-space>
          <a-table :columns="freezeColumns" :data-source="freezes" :loading="freezesLoading" row-key="rowKey" :pagination="false">
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'action'">
                <a-button type="link" danger size="small" @click="onUnfreeze(record)">
                  {{ t('logicFlow.governance.unfreeze') }}
                </a-button>
              </template>
            </template>
          </a-table>
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <a-modal
      v-model:open="consumeOpen"
      :title="t('logicFlow.governance.consumeTitle')"
      :confirm-loading="consumeLoading"
      @ok="confirmConsume"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('logicFlow.governance.amount')">
          <a-input-number v-model:value="consumeAmount" :min="1" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { message } from 'ant-design-vue'
import {
  consumeQuota,
  getCanaryReleases,
  getQuotaInfo,
  getVersionFreezes,
  freezeVersion,
  setCanaryRollout,
  unfreezeVersion,
  type QuotaInfoDto,
  type CanaryReleaseInfoDto,
  type VersionFreezeInfoDto,
} from '@/services/api-logic-flow'

const { t } = useI18n()

const activeTab = ref('quotas')
const quotas = ref<QuotaInfoDto[]>([])
const quotasLoading = ref(false)
const canaryRows = ref<CanaryReleaseInfoDto[]>([])
const canaryLoading = ref(false)
const freezes = ref<(VersionFreezeInfoDto & { rowKey: string })[]>([])
const freezesLoading = ref(false)

const consumeOpen = ref(false)
const consumeLoading = ref(false)
const consumeTarget = ref<QuotaInfoDto | null>(null)
const consumeAmount = ref(1)

const freezeForm = ref({ resourceType: '', resourceId: 1, reason: '' })
const freezeSubmitting = ref(false)

const quotaColumns = computed(() => [
  { title: t('logicFlow.governance.resourceType'), dataIndex: 'resourceType', key: 'resourceType' },
  { title: t('logicFlow.governance.limit'), dataIndex: 'limit', key: 'limit', width: 100 },
  { title: t('logicFlow.governance.used'), dataIndex: 'used', key: 'used', width: 100 },
  { title: t('logicFlow.governance.remaining'), dataIndex: 'remaining', key: 'remaining', width: 100 },
  { title: t('batchProcess.common.action'), key: 'action', width: 120 },
])

const canaryColumns = computed(() => [
  { title: t('logicFlow.governance.featureKey'), dataIndex: 'featureKey', key: 'featureKey' },
  { title: t('logicFlow.governance.rollout'), key: 'pct', width: 280 },
  { title: t('logicFlow.governance.activation'), key: 'active', width: 120 },
  { title: t('logicFlow.governance.activatedAt'), dataIndex: 'activatedAt', key: 'activatedAt' },
])

const freezeColumns = computed(() => [
  { title: t('logicFlow.governance.resourceType'), dataIndex: 'resourceType', key: 'resourceType' },
  { title: t('logicFlow.governance.resourceId'), dataIndex: 'resourceId', key: 'resourceId', width: 120 },
  { title: t('logicFlow.governance.reason'), dataIndex: 'reason', key: 'reason' },
  { title: t('logicFlow.governance.frozenBy'), dataIndex: 'frozenBy', key: 'frozenBy' },
  { title: t('logicFlow.governance.frozenAt'), dataIndex: 'frozenAt', key: 'frozenAt' },
  { title: t('batchProcess.common.action'), key: 'action', width: 120 },
])

async function loadQuotas() {
  quotasLoading.value = true
  try {
    const res = await getQuotaInfo()
    const d = res?.data
    if (Array.isArray(d)) quotas.value = d
    else if (d) quotas.value = [d as QuotaInfoDto]
    else quotas.value = []
  } finally {
    quotasLoading.value = false
  }
}

async function loadCanary() {
  canaryLoading.value = true
  try {
    const res = await getCanaryReleases()
    canaryRows.value = res?.data ?? []
  } finally {
    canaryLoading.value = false
  }
}

async function loadFreezes() {
  freezesLoading.value = true
  try {
    const res = await getVersionFreezes()
    const rows = res?.data ?? []
    freezes.value = rows.map((r) => ({
      ...r,
      rowKey: `${r.resourceType}:${r.resourceId}`,
    }))
  } finally {
    freezesLoading.value = false
  }
}

function onTabChange(key: string | number) {
  const k = String(key)
  if (k === 'quotas') loadQuotas()
  if (k === 'canary') loadCanary()
  if (k === 'freeze') loadFreezes()
}

function openConsume(record: QuotaInfoDto) {
  consumeTarget.value = record
  consumeAmount.value = 1
  consumeOpen.value = true
}

async function confirmConsume() {
  if (!consumeTarget.value) return
  consumeLoading.value = true
  try {
    const res = await consumeQuota(consumeTarget.value.resourceType, consumeAmount.value)
    if (res?.data?.success) message.success(t('logicFlow.governance.consumeOk'))
    else message.warning(t('logicFlow.governance.consumeFail'))
    consumeOpen.value = false
    loadQuotas()
  } finally {
    consumeLoading.value = false
  }
}

async function onCanaryAfterChange(featureKey: string, value: number) {
  try {
    await setCanaryRollout(featureKey, value)
    message.success(t('logicFlow.governance.canarySaved'))
    loadCanary()
  } catch {
    loadCanary()
  }
}

async function submitFreeze() {
  const f = freezeForm.value
  if (!f.resourceType.trim()) {
    message.warning(t('logicFlow.governance.resourceTypeRequired'))
    return
  }
  freezeSubmitting.value = true
  try {
    await freezeVersion({
      resourceType: f.resourceType.trim(),
      resourceId: f.resourceId,
      reason: f.reason.trim() || t('logicFlow.governance.defaultFreezeReason'),
    })
    message.success(t('logicFlow.governance.freezeOk'))
    loadFreezes()
  } finally {
    freezeSubmitting.value = false
  }
}

async function onUnfreeze(record: VersionFreezeInfoDto) {
  await unfreezeVersion(record.resourceType, record.resourceId)
  message.success(t('logicFlow.governance.unfreezeOk'))
  loadFreezes()
}

onMounted(() => {
  loadQuotas()
})
</script>

<style scoped>
.resource-governance {
  padding: 0 0 24px;
}
.freeze-form {
  max-width: 960px;
}
</style>
